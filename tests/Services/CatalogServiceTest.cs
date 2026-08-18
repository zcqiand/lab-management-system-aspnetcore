namespace Lab.AspNetCore.Tests.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Lab.AspNetCore.Services;
using Xunit;

/// <summary>
/// M04.F06/F07/F08/F09 码表 fnTest（B2）。语义基准：lab-springboot CatalogServiceTest
/// （tenant 过滤 / objectCode 精确 / keyword 大小写不敏包含 / sortOrder+code 排序 / PATCH / 404）。
/// </summary>
public class CatalogServiceTest
{
    private const string Tenant = "TENANT-001";

    private static InspectionModel Model(string code, string name, int sortOrder = 0, string obj = "OBJ-CONCRETE") => new()
    {
        Code = code, TenantId = Tenant, InspectionObjectCode = obj, Name = name,
        SortOrder = sortOrder, CreatedAt = "t", UpdatedAt = "t",
    };

    // === M04.F06 型号 ===

    [Fact]
    [Trait("Fn", "M04.F06.I01")]
    public void ListModels_objectCodeFiltersExactly()
    {
        var store = new InMemoryCatalogStore();
        store.SaveModel(Model("M-1", "型号一", obj: "OBJ-CONCRETE"));
        store.SaveModel(Model("M-2", "型号二", obj: "OBJ-STEEL"));
        var service = new CatalogService(store);

        var outList = service.ListModels(Tenant, "OBJ-STEEL", null);

        Assert.Single(outList);
        Assert.Equal("M-2", outList[0].Code);
    }

    [Fact]
    [Trait("Fn", "M04.F06.I01")]
    public void ListModels_keywordMatchesNameCaseInsensitive_sortedBySortOrderThenCode()
    {
        var store = new InMemoryCatalogStore();
        store.SaveModel(Model("M-2", "Fire-型号", 1));
        store.SaveModel(Model("M-1", "FIRE-型号", 1));
        store.SaveModel(Model("M-0", "无关", 0));
        var service = new CatalogService(store);

        var outList = service.ListModels(Tenant, null, "fire");

        Assert.Equal(2, outList.Count);
        Assert.Equal("M-1", outList[0].Code); // 同 sortOrder 按 code
        Assert.Equal("M-2", outList[1].Code);
    }

    [Fact]
    [Trait("Fn", "M04.F06.I01")]
    public void ListModels_tenantIsolation()
    {
        var store = new InMemoryCatalogStore();
        var other = Model("M-X", "他租户");
        other.TenantId = "TENANT-002";
        store.SaveModel(other);
        store.SaveModel(Model("M-1", "本租户"));
        var service = new CatalogService(store);

        Assert.Single(service.ListModels(Tenant, null, null));
    }

    [Fact]
    [Trait("Fn", "M04.F06.I02")]
    public void CreateModel_setsTimestampsEqual()
    {
        var service = new CatalogService(new InMemoryCatalogStore());

        var m = service.CreateModel(Tenant, new CreateCatalogEntryRequest
        {
            Code = "M-NEW", Name = "新型号", InspectionObjectCode = "OBJ-CONCRETE", SortOrder = 5,
        });

        Assert.Equal(Tenant, m.TenantId);
        Assert.Equal("M-NEW", m.Code);
        Assert.Equal(m.CreatedAt, m.UpdatedAt);
    }

    [Fact]
    [Trait("Fn", "M04.F06.I03")]
    public void UpdateModel_patchKeepsUnsetFields()
    {
        var store = new InMemoryCatalogStore();
        store.SaveModel(Model("M-1", "旧名", sortOrder: 3));
        var service = new CatalogService(store);

        var m = service.UpdateModel(Tenant, "M-1", new UpdateCatalogEntryRequest { Name = "新名" });

        Assert.Equal("新名", m.Name);
        Assert.Equal("OBJ-CONCRETE", m.InspectionObjectCode); // 未传保留
        Assert.Equal(3, m.SortOrder); // 未传保留
        Assert.NotEqual(m.CreatedAt, m.UpdatedAt);
    }

    [Fact]
    [Trait("Fn", "M04.F06.I03")]
    public void UpdateModel_missing_throws404()
    {
        var service = new CatalogService(new InMemoryCatalogStore());
        Assert.Throws<KeyNotFoundException>(
            () => service.UpdateModel(Tenant, "GHOST", new UpdateCatalogEntryRequest { Name = "x" }));
    }

    [Fact]
    [Trait("Fn", "M04.F06.I04")]
    public void DeleteModel_removes_thenMissing()
    {
        var store = new InMemoryCatalogStore();
        store.SaveModel(Model("M-1", "型号"));
        var service = new CatalogService(store);

        service.DeleteModel(Tenant, "M-1");
        Assert.Empty(service.ListModels(Tenant, null, null));
        Assert.Throws<KeyNotFoundException>(() => service.DeleteModel(Tenant, "M-1"));
    }

    // === M04.F07 规格（同构，抽代表路径） ===

    [Fact]
    [Trait("Fn", "M04.F07.I01")]
    public void ListSpecs_keywordFiltersByName()
    {
        var store = new InMemoryCatalogStore();
        store.SaveSpec(new InspectionSpec { Code = "S-1", TenantId = Tenant, Name = "Fire-A", CreatedAt = "t", UpdatedAt = "t" });
        store.SaveSpec(new InspectionSpec { Code = "S-2", TenantId = Tenant, Name = "Cold-B", CreatedAt = "t", UpdatedAt = "t" });
        var service = new CatalogService(store);

        Assert.Single(service.ListSpecs(Tenant, null, "fire"));
    }

    [Fact]
    [Trait("Fn", "M04.F07.I02")]
    public void CreateSpec_mapsAllFields()
    {
        var service = new CatalogService(new InMemoryCatalogStore());

        var s = service.CreateSpec(Tenant, new CreateCatalogEntryRequest { Code = "S-NEW", Name = "新规格" });

        Assert.Equal("S-NEW", s.Code);
        Assert.Equal(Tenant, s.TenantId);
    }

    [Fact]
    [Trait("Fn", "M04.F07.I03")]
    public void UpdateSpec_patchSemantics()
    {
        var store = new InMemoryCatalogStore();
        store.SaveSpec(new InspectionSpec { Code = "S-1", TenantId = Tenant, Name = "旧", CreatedAt = "t", UpdatedAt = "t" });
        var service = new CatalogService(store);

        var s = service.UpdateSpec(Tenant, "S-1", new UpdateCatalogEntryRequest { Remark = "备注" });

        Assert.Equal("旧", s.Name); // 未传保留
        Assert.Equal("备注", s.Remark);
    }

    [Fact]
    [Trait("Fn", "M04.F07.I04")]
    public void DeleteSpec_missing_throws404()
    {
        var service = new CatalogService(new InMemoryCatalogStore());
        Assert.Throws<KeyNotFoundException>(() => service.DeleteSpec(Tenant, "GHOST"));
    }

    // === M04.F08 等级（同构） ===

    [Fact]
    [Trait("Fn", "M04.F08.I01")]
    public void ListGrades_emptyWhenNoData()
    {
        var service = new CatalogService(new InMemoryCatalogStore());
        Assert.Empty(service.ListGrades(Tenant, null, null));
    }

    [Fact]
    [Trait("Fn", "M04.F08.I01")]
    public void ListGrades_objectCodeFilter()
    {
        var store = new InMemoryCatalogStore();
        store.SaveGrade(new InspectionGrade { Code = "G-1", TenantId = Tenant, InspectionObjectCode = "OBJ-A", Name = "一级", CreatedAt = "t", UpdatedAt = "t" });
        store.SaveGrade(new InspectionGrade { Code = "G-2", TenantId = Tenant, InspectionObjectCode = "OBJ-B", Name = "二级", CreatedAt = "t", UpdatedAt = "t" });
        var service = new CatalogService(store);

        Assert.Single(service.ListGrades(Tenant, "OBJ-A", null));
    }

    [Fact]
    [Trait("Fn", "M04.F08.I02")]
    public void CreateGrade_defaults()
    {
        var service = new CatalogService(new InMemoryCatalogStore());

        var g = service.CreateGrade(Tenant, new CreateCatalogEntryRequest { Code = "G-NEW", Name = "新等级" });

        Assert.Equal(0, g.SortOrder);
        Assert.Equal(string.Empty, g.Remark);
    }

    [Fact]
    [Trait("Fn", "M04.F08.I03")]
    public void UpdateGrade_rename()
    {
        var store = new InMemoryCatalogStore();
        store.SaveGrade(new InspectionGrade { Code = "G-1", TenantId = Tenant, Name = "旧", CreatedAt = "t", UpdatedAt = "t" });
        var service = new CatalogService(store);

        Assert.Equal("新", service.UpdateGrade(Tenant, "G-1", new UpdateCatalogEntryRequest { Name = "新" }).Name);
    }

    [Fact]
    [Trait("Fn", "M04.F08.I04")]
    public void DeleteGrade_removes()
    {
        var store = new InMemoryCatalogStore();
        store.SaveGrade(new InspectionGrade { Code = "G-1", TenantId = Tenant, Name = "一级", CreatedAt = "t", UpdatedAt = "t" });
        var service = new CatalogService(store);

        service.DeleteGrade(Tenant, "G-1");
        Assert.Empty(service.ListGrades(Tenant, null, null));
    }

    // === M04.F09 牌号（含 FK SET NULL 联动） ===

    [Fact]
    [Trait("Fn", "M04.F09.I01")]
    public void ListBrands_keywordFiltersByCode()
    {
        var store = new InMemoryCatalogStore();
        store.SaveBrand(new InspectionBrand { Code = "HRB400", TenantId = Tenant, Name = "热轧带肋", CreatedAt = "t", UpdatedAt = "t" });
        store.SaveBrand(new InspectionBrand { Code = "Q235", TenantId = Tenant, Name = "碳素结构", CreatedAt = "t", UpdatedAt = "t" });
        var service = new CatalogService(store);

        var outList = service.ListBrands(Tenant, null, "hrb");

        Assert.Single(outList);
        Assert.Equal("HRB400", outList[0].Code);
    }

    [Fact]
    [Trait("Fn", "M04.F09.I02")]
    public void CreateBrand_mapsFields()
    {
        var service = new CatalogService(new InMemoryCatalogStore());

        var b = service.CreateBrand(Tenant, new CreateCatalogEntryRequest { Code = "B-NEW", Name = "新牌号", SortOrder = 2 });

        Assert.Equal(2, b.SortOrder);
        Assert.Equal(Tenant, b.TenantId);
    }

    [Fact]
    [Trait("Fn", "M04.F09.I03")]
    public void UpdateBrand_missing_throws404()
    {
        var service = new CatalogService(new InMemoryCatalogStore());
        Assert.Throws<KeyNotFoundException>(() => service.UpdateBrand(Tenant, "GHOST", new UpdateCatalogEntryRequest()));
    }

    [Fact]
    [Trait("Fn", "M04.F09.I04")]
    public void DeleteBrand_setsNullOnReferencingRequirements()
    {
        var catalog = new InMemoryCatalogStore();
        var requirements = new InMemoryRequirementStore();
        catalog.BrandDeleted += requirements.OnBrandDeleted;
        catalog.SaveBrand(new InspectionBrand { Code = "HRB400", TenantId = Tenant, Name = "热轧带肋", CreatedAt = "t", UpdatedAt = "t" });
        requirements.Save(new TechnicalRequirement
        {
            TenantId = Tenant, InspectionObjectCode = "OBJ", InspectionParameterCode = "PARAM",
            JudgmentStandardCode = "STD", Brand = "HRB400", CreatedAt = "t", UpdatedAt = "t",
        });
        var service = new CatalogService(catalog);

        service.DeleteBrand(Tenant, "HRB400");

        var row = requirements.Find(Tenant, "OBJ", "PARAM", "STD");
        Assert.NotNull(row);
        Assert.Equal(string.Empty, row!.Brand); // FK SET NULL
    }
}
