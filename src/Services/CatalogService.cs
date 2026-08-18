namespace Lab.AspNetCore.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// M04.F06/F07/F08/F09 — 型号/规格/等级/牌号码表 CRUD（B2）。
/// 4 组同构：list（tenant + objectCode + keyword 过滤）/ create / update（PATCH）/ delete。
/// 语义镜像 springboot CatalogService：miss → KeyNotFoundException(404)；
/// 时间戳 ISO UTC 字符串，create 时 createdAt==updatedAt。
/// </summary>
public sealed class CatalogService(InMemoryCatalogStore store)
{
    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    // === M04.F06 型号 ===

    public IReadOnlyList<InspectionModel> ListModels(string tenantId, string? objectCode, string? keyword) =>
        store.FilterModels(tenantId, objectCode, keyword);

    public InspectionModel CreateModel(string tenantId, CreateCatalogEntryRequest body)
    {
        var now = Now();
        var m = new InspectionModel
        {
            Code = body.Code,
            TenantId = tenantId,
            InspectionObjectCode = body.InspectionObjectCode ?? "",
            Name = body.Name,
            Remark = body.Remark ?? "",
            SortOrder = body.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveModel(m);
        return m;
    }

    public InspectionModel UpdateModel(string tenantId, string code, UpdateCatalogEntryRequest body)
    {
        var m = store.FindModel(tenantId, code) ?? throw new KeyNotFoundException($"model {code} not found");
        if (body.InspectionObjectCode is not null) m.InspectionObjectCode = body.InspectionObjectCode;
        if (body.Name is not null) m.Name = body.Name;
        if (body.Remark is not null) m.Remark = body.Remark;
        m.SortOrder = body.SortOrder != 0 ? body.SortOrder : m.SortOrder;
        m.UpdatedAt = Now();
        store.SaveModel(m);
        return m;
    }

    public void DeleteModel(string tenantId, string code)
    {
        if (!store.DeleteModel(tenantId, code))
        {
            throw new KeyNotFoundException($"model {code} not found");
        }
    }

    // === M04.F07 规格 ===

    public IReadOnlyList<InspectionSpec> ListSpecs(string tenantId, string? objectCode, string? keyword) =>
        store.FilterSpecs(tenantId, objectCode, keyword);

    public InspectionSpec CreateSpec(string tenantId, CreateCatalogEntryRequest body)
    {
        var now = Now();
        var s = new InspectionSpec
        {
            Code = body.Code,
            TenantId = tenantId,
            InspectionObjectCode = body.InspectionObjectCode ?? "",
            Name = body.Name,
            Remark = body.Remark ?? "",
            SortOrder = body.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveSpec(s);
        return s;
    }

    public InspectionSpec UpdateSpec(string tenantId, string code, UpdateCatalogEntryRequest body)
    {
        var s = store.FindSpec(tenantId, code) ?? throw new KeyNotFoundException($"spec {code} not found");
        if (body.InspectionObjectCode is not null) s.InspectionObjectCode = body.InspectionObjectCode;
        if (body.Name is not null) s.Name = body.Name;
        if (body.Remark is not null) s.Remark = body.Remark;
        s.SortOrder = body.SortOrder != 0 ? body.SortOrder : s.SortOrder;
        s.UpdatedAt = Now();
        store.SaveSpec(s);
        return s;
    }

    public void DeleteSpec(string tenantId, string code)
    {
        if (!store.DeleteSpec(tenantId, code))
        {
            throw new KeyNotFoundException($"spec {code} not found");
        }
    }

    // === M04.F08 等级 ===

    public IReadOnlyList<InspectionGrade> ListGrades(string tenantId, string? objectCode, string? keyword) =>
        store.FilterGrades(tenantId, objectCode, keyword);

    public InspectionGrade CreateGrade(string tenantId, CreateCatalogEntryRequest body)
    {
        var now = Now();
        var g = new InspectionGrade
        {
            Code = body.Code,
            TenantId = tenantId,
            InspectionObjectCode = body.InspectionObjectCode ?? "",
            Name = body.Name,
            Remark = body.Remark ?? "",
            SortOrder = body.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveGrade(g);
        return g;
    }

    public InspectionGrade UpdateGrade(string tenantId, string code, UpdateCatalogEntryRequest body)
    {
        var g = store.FindGrade(tenantId, code) ?? throw new KeyNotFoundException($"grade {code} not found");
        if (body.InspectionObjectCode is not null) g.InspectionObjectCode = body.InspectionObjectCode;
        if (body.Name is not null) g.Name = body.Name;
        if (body.Remark is not null) g.Remark = body.Remark;
        g.SortOrder = body.SortOrder != 0 ? body.SortOrder : g.SortOrder;
        g.UpdatedAt = Now();
        store.SaveGrade(g);
        return g;
    }

    public void DeleteGrade(string tenantId, string code)
    {
        if (!store.DeleteGrade(tenantId, code))
        {
            throw new KeyNotFoundException($"grade {code} not found");
        }
    }

    // === M04.F09 牌号（删除时 FK SET NULL 由 store 事件联动 RequirementStore） ===

    public IReadOnlyList<InspectionBrand> ListBrands(string tenantId, string? objectCode, string? keyword) =>
        store.FilterBrands(tenantId, objectCode, keyword);

    public InspectionBrand CreateBrand(string tenantId, CreateCatalogEntryRequest body)
    {
        var now = Now();
        var b = new InspectionBrand
        {
            Code = body.Code,
            TenantId = tenantId,
            InspectionObjectCode = body.InspectionObjectCode ?? "",
            Name = body.Name,
            Remark = body.Remark ?? "",
            SortOrder = body.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveBrand(b);
        return b;
    }

    public InspectionBrand UpdateBrand(string tenantId, string code, UpdateCatalogEntryRequest body)
    {
        var b = store.FindBrand(tenantId, code) ?? throw new KeyNotFoundException($"brand {code} not found");
        if (body.InspectionObjectCode is not null) b.InspectionObjectCode = body.InspectionObjectCode;
        if (body.Name is not null) b.Name = body.Name;
        if (body.Remark is not null) b.Remark = body.Remark;
        b.SortOrder = body.SortOrder != 0 ? body.SortOrder : b.SortOrder;
        b.UpdatedAt = Now();
        store.SaveBrand(b);
        return b;
    }

    public void DeleteBrand(string tenantId, string code)
    {
        if (!store.DeleteBrand(tenantId, code))
        {
            throw new KeyNotFoundException($"brand {code} not found");
        }
    }
}
