namespace Lab.AspNetCore.Tests.Harness;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// lab_test 真库语义测试：EF 路径的真 SQL 行为（ILIKE 匹配 / tenant 隔离 / 排序 / 级联）。
/// 硬依赖共享 PG —— 连不上即失败（TestDb.RequireReachable），不 skip：
/// memory 分支测试掩盖 prod 问题是 v0.2.25/26 两起事故的根因。
///
/// 分层（CI=翻译性 / gate=真库）：[Trait("Category", "RealDb")] 标记本测试集，
/// ci.yml 用 --filter 排除（GitHub runner 够不到内网 PG）；suite gate L4 全量跑。
/// 数据隔离：独立 tenant_id 前缀（PG-T-），全部用例走该前缀；fixture 末尾按前缀清理。
/// 不挂 [Trait("Fn", ...)]：脚手架级（LabDbContextModelTest 同约定）。
/// </summary>
[Trait("Category", "RealDb")]
public sealed class EfStorePgTest : IDisposable
{
    private const string Tenant = "PG-T-EFSTORE";
    private const string ObjCode = "OBJ-PG-T";

    private readonly LabDbContext db = TestDb.CreateContext();

    public EfStorePgTest()
    {
        TestDb.RequireReachable();
        Cleanup();
        SeedParents();
    }

    public void Dispose() => Cleanup();

    private void Cleanup()
    {
        db.SampleReceipts.Where(r => r.TenantId == Tenant).ExecuteDelete();
        db.Samples.Where(s => s.TenantId == Tenant).ExecuteDelete();
        db.TestRecords.Where(t => t.TenantId == Tenant).ExecuteDelete();
        db.Contracts.Where(c => c.TenantId == Tenant).ExecuteDelete();
        db.InspectionBrands.Where(b => b.TenantId == Tenant).ExecuteDelete();
        db.InspectionModels.Where(m => m.TenantId == Tenant).ExecuteDelete();
        // 父行（objects/specialties 是平台级字典，同 code 复用；清业务行即够）
        db.SaveChanges();
    }

    /// <summary>真库 FK：inspection_models.inspection_object_code → inspection_objects.code，
    /// 测试数据须先种父行（这正是 memory 分支测不出的约束之一）。</summary>
    private void SeedParents()
    {
        if (!db.InspectionSpecialties.Any(s => s.Code == "SP-SMK-001"))
        {
            db.InspectionSpecialties.Add(new InspectionSpecialty
            {
                Code = "SP-SMK-001",
                OfficialNo = "OFFICIAL-PG-T",
                Name = "PG 测试专项",
                SortOrder = 0,
                CreatedAt = "2026-01-01T00:00:00Z",
                UpdatedAt = "2026-01-01T00:00:00Z",
            });
        }

        if (!db.InspectionObjects.Any(o => o.Code == ObjCode))
        {
            db.InspectionObjects.Add(new InspectionObject
            {
                Code = ObjCode,
                InspectionSpecialtyCode = "SP-SMK-001",
                SourceProjectNo = "SRC-PG-T",
                SourceProjectName = "PG 测试项目",
                Name = "PG 测试对象",
                SortOrder = 0,
                CreatedAt = "2026-01-01T00:00:00Z",
                UpdatedAt = "2026-01-01T00:00:00Z",
            });
        }

        db.SaveChanges();
    }

    private static InspectionModel Model(string code, string name, int sortOrder = 0) => new()
    {
        Code = code,
        TenantId = Tenant,
        InspectionObjectCode = ObjCode,
        Name = name,
        SortOrder = sortOrder,
        CreatedAt = "2026-01-01T00:00:00Z",
        UpdatedAt = "2026-01-01T00:00:00Z",
    };

    [Fact]
    public void FilterModels_keywordIsCaseInsensitiveLike()
    {
        db.InspectionModels.Add(Model("M-1", "Fire-型号"));
        db.InspectionModels.Add(Model("M-2", "FIRE-型号"));
        db.InspectionModels.Add(Model("M-0", "无关"));
        db.SaveChanges();

        var outList = new EfCatalogStore(db).FilterModels(Tenant, null, "fire");

        Assert.Equal(2, outList.Count);
        Assert.Contains(outList, m => m.Code == "M-1");
        Assert.Contains(outList, m => m.Code == "M-2");
    }

    [Fact]
    public void FilterModels_isolatedByTenant()
    {
        db.InspectionModels.Add(Model("M-ISO", "隔离验证"));
        db.SaveChanges();

        // 另一租户查不到本租户数据（真 SQL WHERE tenant_id 生效）
        var other = new EfCatalogStore(db).FilterModels("PG-T-OTHER", null, null);
        Assert.DoesNotContain(other, m => m.Code == "M-ISO");
    }

    [Fact]
    public void FilterModels_sortedBySortOrderThenCode()
    {
        db.InspectionModels.Add(Model("M-B", "b", 1));
        db.InspectionModels.Add(Model("M-A", "a", 1));
        db.InspectionModels.Add(Model("M-Z", "z", 0));
        db.SaveChanges();

        var outList = new EfCatalogStore(db).FilterModels(Tenant, null, null);

        Assert.Equal(new[] { "M-Z", "M-A", "M-B" }, outList.Select(m => m.Code).ToArray());
    }

    [Fact]
    public void SaveModel_upsertSameKeyUpdates()
    {
        var store = new EfCatalogStore(db);
        store.SaveModel(Model("M-UP", "v1"));
        store.SaveModel(Model("M-UP", "v2")); // 同 tenant+code → SetValues 更新

        var all = store.FilterModels(Tenant, null, "v2");
        Assert.Single(all);
        Assert.Equal("v2", all[0].Name);
    }
}
