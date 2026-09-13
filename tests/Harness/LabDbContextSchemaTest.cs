namespace Lab.AspNetCore.Tests.Harness;

using System.Data.Common;
using Lab.AspNetCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// DB-First schema 漂移测试（ADR-0025/ADR-0033）：EF 全模型 ↔ 真库 information_schema 逐列对照。
///
/// 背景：lab 仓的运行时 EF 实体 = NSwag DTO（LabDbContext 手写映射、DTO==entity 零转换层），
/// 不走 dotnet ef dbcontext scaffold（scaffold 产的是另一套 string 属性实体，会毁掉
/// Wire 枚举转换器与 DTO 复用——ADR-0033 对本仓的字面方案在此不可行，偏差已记录）。
/// 因此漂移防线不用「scaffold 镜像 git diff」，而是直接对照**运行时模型**与
/// **共享 PG 物化态**（lab_test = shared src/db/schema.ts 的 migrate 产物）：
/// - DB 真演进（shared schema.ts 改了、lab_test 已 migrate）→ 本测试红 = 标准工作流信号
/// - 本仓映射漂移（漏 Ignore / 漏 ToTable / 列名错）→ 本测试红 = 42703 类事故提前到测试期
///
/// 对照规则（逐 entity）：
/// 1. 模型列 ⊆ 库列（多出来的模型列 prod 首查 42703）
/// 2. 库 NOT NULL 且无 default 的列 ⊆ 模型列（缺了 insert 必炸）
/// 3. PK 列集合全等（复合主键顺序敏感）
///
/// 连不上库即失败（TestDb 同约定，不 skip）。scripts/sync-db.sh 跑本测试 + 写
/// ADR-0026 marker。不挂 [Trait("Fn", ...)]：脚手架级（LabDbContextModelTest 同约定）。
/// </summary>
public class LabDbContextSchemaTest
{
    [Fact]
    public void EfModel_matches_materialized_shared_schema()
    {
        var options = new DbContextOptionsBuilder<LabDbContext>()
            .UseLabNpgsql(TestDb.ConnectionString)
            .Options;

        using var ctx = new LabDbContext(options);
        var entities = ctx.Model.GetEntityTypes().ToList();
        Assert.True(
            entities.Count >= 24,
            $"EF 模型只映射了 {entities.Count} 个实体（应 ≥24）——LabDbContext 缺 DbSet/映射？");

        var (dbColumns, dbPrimaryKeys) = ReadDbSchema();

        var errors = new List<string>();
        foreach (var entity in entities)
        {
            var table = entity.GetTableName();
            if (table is null)
            {
                errors.Add($"{entity.DisplayName()} 未映射表（缺 ToTable？）");
                continue;
            }

            if (!dbColumns.TryGetValue(table, out var dbCols))
            {
                errors.Add($"{table}: 模型映射了该表，但库里不存在（42P01 类）");
                continue;
            }

            // 规则 1：模型列必须都存在于库
            foreach (var prop in entity.GetProperties())
            {
                var column = prop.GetColumnName();
                if (column is not null && !dbCols.ContainsKey(column))
                {
                    errors.Add($"{table}.{column}: 模型有此列，库里没有（42703 类——列名映射漂移）");
                }
            }

            // 规则 2：库 NOT NULL 无 default 的列必须有模型映射（否则 insert 缺值必炸）
            foreach (var (column, (nullable, hasDefault)) in dbCols)
            {
                if (nullable == "NO" && !hasDefault
                    && entity.GetProperties().All(p => p.GetColumnName() != column))
                {
                    errors.Add($"{table}.{column}: 库 NOT NULL 且无 default，模型未映射（insert 必炸）");
                }
            }

            // 规则 3：PK 列集合全等（列序不比——EF 按 HasKey 定义序、库按 DDL 序，序差非漂移）
            var modelPk = entity.FindPrimaryKey()?.Properties
                .Select(p => p.GetColumnName())
                .Where(c => c is not null)
                .Select(c => c!)
                .ToList() ?? [];
            dbPrimaryKeys.TryGetValue(table, out var dbPk);
            var dbPkList = dbPk ?? [];
            if (modelPk.Count != dbPkList.Count || modelPk.Except(dbPkList).Any())
            {
                errors.Add(
                    $"{table}: PK 不一致——模型 [{string.Join(", ", modelPk)}] vs 库 [{string.Join(", ", dbPkList)}]");
            }
        }

        Assert.True(
            errors.Count == 0,
            "EF 模型与共享库物化 schema 漂移（DB 没改时出现本失败 = 本仓映射漂移；"
            + "shared schema.ts 刚演进 = 标准工作流，核对后同步 LabDbContext 映射）:\n"
            + string.Join("\n", errors));
    }

    private static (Dictionary<string, Dictionary<string, (string, bool)>>, Dictionary<string, List<string>>)
        ReadDbSchema()
    {
        var columns = new Dictionary<string, Dictionary<string, (string, bool)>>();
        var primaryKeys = new Dictionary<string, List<string>>();

        using var conn = TestDb.CreateConnection();
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT table_name, column_name, is_nullable, column_default IS NOT NULL
                FROM information_schema.columns
                WHERE table_schema = 'public'
                """;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var table = reader.GetString(0);
                var column = reader.GetString(1);
                var nullable = reader.GetString(2);
                var hasDefault = !reader.IsDBNull(3) && reader.GetBoolean(3);
                if (!columns.TryGetValue(table, out var cols))
                {
                    cols = [];
                    columns[table] = cols;
                }

                cols[column] = (nullable, hasDefault);
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT tc.table_name, kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                  ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                WHERE tc.constraint_type = 'PRIMARY KEY' AND tc.table_schema = 'public'
                ORDER BY tc.table_name, kcu.ordinal_position
                """;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var table = reader.GetString(0);
                var column = reader.GetString(1);
                if (!primaryKeys.TryGetValue(table, out var pk))
                {
                    pk = [];
                    primaryKeys[table] = pk;
                }

                pk.Add(column);
            }
        }

        return (columns, primaryKeys);
    }
}
