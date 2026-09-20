namespace Lab.AspNetCore.Tests.Harness;

using System.Data.Common;
using Lab.AspNetCore.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// lab_test 真库测试基建。硬依赖共享 PG（100.79.128.25 / lab_test）——
/// 连不上直接失败，不 skip：EF 分支的行为必须被真 SQL 验证（v0.2.26 教训：
/// memory 分支测试全绿、prod 首请求炸 Kw 翻译错误）。
///
/// 连接串读 LAB_TEST_DATABASE_URL（Npgsql 格式），缺省回落 dev 同款真库
/// （.env.local / .env.example 的 DATABASE_URL 是同一个共享 PG）。
/// 每个 fixture 用独立 tenant_id 隔离数据，dispose 时按 tenant 清理。
/// </summary>
public static class TestDb
{
    public const string DefaultUrl =
        "Host=100.79.128.25;Port=5432;Database=lab_test;Username=postgres;Password=qiand68+++";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("LAB_TEST_DATABASE_URL") ?? DefaultUrl;

    /// <summary>连接串解析出的库名（LabDbContextSchemaTest 报错信息用）。</summary>
    public static string DatabaseName
    {
        get
        {
            var url = ConnectionString;
            var key = "Database=";
            var at = url.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            var end = at < 0 ? -1 : url.IndexOf(';', at);
            return at < 0 ? "?" : url[(at + key.Length)..(end < 0 ? url.Length : end)];
        }
    }

    /// <summary>建 DbContext（EF 只镜像不 Migrate；表结构 = shared src/db/schema.ts 的 migrate 产物，ADR-0025/0033）。</summary>
    public static LabDbContext CreateContext()
    {
        // 5.75 起 jsonb List 写路径（flow_history 等）进真库断言，测试 context 必须与 prod
        // 同源：prod 走 NpgsqlDataSourceBuilder.EnableDynamicJson（Program.cs），此处同款挂上。
        // 缺它 = 写 List<FlowHistoryEntry> 直接 NotSupportedException（dynamic JSON 未 opt-in）。
        var dataSource = new Npgsql.NpgsqlDataSourceBuilder(ConnectionString).EnableDynamicJson().Build();
        var options = new DbContextOptionsBuilder<LabDbContext>()
            .UseNpgsql(dataSource)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new LabDbContext(options);
    }

    /// <summary>裸 ADO.NET 连接（LabDbContextSchemaTest 查 information_schema 用）。</summary>
    public static DbConnection CreateConnection() => new Npgsql.NpgsqlConnection(ConnectionString);

    /// <summary>连接可用性硬断言：连不上即测试失败（不是 skip）。</summary>
    public static void RequireReachable()
    {
        using var ctx = CreateContext();
        ctx.Database.OpenConnection();
    }
}
