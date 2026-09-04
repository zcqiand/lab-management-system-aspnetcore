namespace Lab.AspNetCore.Tests.Harness;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// EF 查询翻译性测试：Ef*Store 全部列表/过滤查询必须能翻译成 SQL。
/// store 的 Filter* 拆出 internal BuildXxxQuery（返回未物化 IQueryable），
/// 本测试对其逐个 ToQueryString —— 编译抛 InvalidOperationException = 翻译失败。
/// 无需连库，CI 可跑；真库语义验证在 *PgTest（lab_test 直连）。
///
/// v0.2.26 教训：Kw 普通 C# 方法在 Where lambda 里被引用，EF 不可翻译，
/// memory 分支测试全绿、prod 首请求 500。本测试把「首请求才爆」提前到「测试就爆」。
///
/// 不挂 [Trait("Fn", ...)]：脚手架级（LabDbContextModelTest 同约定）。
/// 维护注意：store 新增 BuildXxxQuery 须同步加断言（漏了不报错，但覆盖出洞）。
/// </summary>
public class EfQueryTranslatabilityTest
{
    private static LabDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<LabDbContext>()
            .UseNpgsql("Host=localhost;Database=lab_dev;Username=probe;Password=probe")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new LabDbContext(options);
    }

    private static string Sql<T>(IQueryable<T> q) where T : class => q.ToQueryString();

    [Fact]
    public void FlowStore_queries_translate()
    {
        using var db = NewContext();

        _ = Sql(EfFlowStore.BuildFilterContractsQuery(db, "TENANT-X", "kw", null));
        _ = Sql(EfFlowStore.BuildFilterReceiptsQuery(db, "TENANT-X", "CTR-1", null, "kw"));
        _ = Sql(EfFlowStore.BuildSummaryQuery(db, "TENANT-X", "ALL", "", ""));
        _ = Sql(EfFlowStore.BuildFlowQueueQuery(db, "TENANT-X", FlowStatus.Receiving, 50));
        _ = Sql(EfFlowStore.BuildFilterSamplesQuery(db, "TENANT-X", "RCP-1", "kw"));
        _ = Sql(EfFlowStore.BuildFilterRecordsQuery(db, "TENANT-X", "SMP-1"));
    }

    [Fact]
    public void CatalogStore_queries_translate()
    {
        using var db = NewContext();

        _ = Sql(EfCatalogStore.BuildFilterModelsQuery(db, "TENANT-X", "OBJ-1", "kw"));
        _ = Sql(EfCatalogStore.BuildFilterSpecsQuery(db, "TENANT-X", "OBJ-1", "kw"));
        _ = Sql(EfCatalogStore.BuildFilterGradesQuery(db, "TENANT-X", "OBJ-1", "kw"));
        _ = Sql(EfCatalogStore.BuildFilterBrandsQuery(db, "TENANT-X", "OBJ-1", "kw"));
    }

    [Fact]
    public void DictionaryStore_queries_translate()
    {
        using var db = NewContext();

        _ = Sql(EfDictionaryStore.BuildFilterSpecialtiesQuery(db, "kw"));
        _ = Sql(EfDictionaryStore.BuildFilterParametersQuery(db, "kw", null));
        _ = Sql(EfDictionaryStore.BuildFilterStandardsQuery(db, "kw", null));
        _ = Sql(EfDictionaryStore.BuildFilterReportNamesQuery(db, "kw"));
        _ = Sql(EfDictionaryStore.BuildFilterInterfacesQuery(db, "kw"));
        _ = Sql(EfDictionaryStore.BuildFilterObjectsQuery(db, "SP-1", "kw"));
    }

    [Fact]
    public void MethodRequirementStore_queries_translate()
    {
        using var db = NewContext();

        _ = Sql(EfMethodStore.BuildFilterQuery(db, "OBJ-1", "PRM-1"));
        _ = Sql(EfRequirementStore.BuildFilterQuery(db, "TENANT-X", "OBJ-1", "PRM-1", "STD-1", null));
    }
}
