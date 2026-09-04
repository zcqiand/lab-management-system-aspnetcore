namespace Lab.AspNetCore.Persistence;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// 测试套件 Npgsql options chain。prod 走 NpgsqlDataSourceBuilder.EnableDynamicJson (jsonb),
/// EF Core 8 AddDbContext&lt;TContext&gt; 给的是 non-generic DbContextOptionsBuilder,
/// 没法在 prod 复用本 helper, prod 在 Program.cs 直接 chain。
///
/// UseSnakeCaseNamingConvention() 强制 PascalCase → snake_case column 名,
/// 与 shared/sql/migrations V001-V014 (snake_case) 列对齐。
/// 不挂 = 42703 column s."Id" does not exist (PascalCase 列 vs snake_case DB),
/// 历史:2026-09-04 prod 首查 SampleReceipts incident。
/// L4 测试 LabDbContextConfigTest 锁住列名映射(prod 漏挂测试立刻红)。
/// </summary>
public static class LabDbContextConfig
{
    public static DbContextOptionsBuilder<LabDbContext> UseLabNpgsql(
        this DbContextOptionsBuilder<LabDbContext> builder,
        string connectionString)
        => builder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}