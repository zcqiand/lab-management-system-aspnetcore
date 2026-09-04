namespace Lab.AspNetCore.Tests.Harness;

using Lab.AspNetCore.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// lab_test 真库测试基建。硬依赖共享 PG（100.79.128.25 / lab_test）——
/// 连不上直接失败，不 skip：EF 分支的行为必须被真 SQL 验证（v0.2.26 教训：
/// memory 分支测试全绿、prod 首请求炸 Kw 翻译错误）。
///
/// 连接串读 LAB_TEST_DATABASE_URL（Npgsql 格式），缺省回落 dev 同款真库
/// （appsettings.Development.json 的 Lab:Data:ConnectionString 是同一个共享 PG）。
/// 每个 fixture 用独立 tenant_id 隔离数据，dispose 时按 tenant 清理。
/// </summary>
public static class TestDb
{
    public const string DefaultUrl =
        "Host=100.79.128.25;Port=5432;Database=lab_test;Username=postgres;Password=qiand68+++";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("LAB_TEST_DATABASE_URL") ?? DefaultUrl;

    /// <summary>建 DbContext（EF 只镜像不 Migrate，表结构由 shared SQL SSOT 管）。</summary>
    public static LabDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LabDbContext>()
            .UseLabNpgsql(ConnectionString)
            .Options;

        return new LabDbContext(options);
    }

    /// <summary>连接可用性硬断言：连不上即测试失败（不是 skip）。</summary>
    public static void RequireReachable()
    {
        using var ctx = CreateContext();
        ctx.Database.OpenConnection();
    }
}
