namespace Lab.AspNetCore.Persistence;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Microsoft.EntityFrameworkCore;

// EF Core 仓储实现（lab_dev 共库）。语义逐条镜像 InMemory*Store：
// 过滤/排序/tenant 收口相同；upsert = 先查后 Add/SetValues；删除返回是否命中。
// 与内存版的刻意分叉（真实约束生效，镜像 springboot JPA）：
// - junction link 前置 FK 校验（内存版无校验，DB 会 23503 硬炸 -> 这里转 ArgumentException=400）
// - 牌号删除 SET NULL 由 DB 的 ON DELETE SET NULL 承担（内存版走事件钩子）
// - 接样删除的样品/记录级联由 DB CASCADE 承担

public sealed class EfCatalogStore(LabDbContext db) : ICatalogStore
{
    // === 型号 M04.F06 ===

    public IReadOnlyList<InspectionModel> FilterModels(string tenantId, string? objectCode, string? keyword) =>
        BuildFilterModelsQuery(db, tenantId, objectCode, keyword).ToList();

    public InspectionModel? FindModel(string tenantId, string code) =>
        db.InspectionModels.FirstOrDefault(m => m.TenantId == tenantId && m.Code == code);

    public void SaveModel(InspectionModel m) =>
        EfStoreOps.Upsert(db, db.InspectionModels, m, x => x.TenantId == m.TenantId && x.Code == m.Code);

    public bool DeleteModel(string tenantId, string code) =>
        db.InspectionModels.Where(m => m.TenantId == tenantId && m.Code == code).ExecuteDelete() > 0;

    // === 规格 M04.F07 ===

    public IReadOnlyList<InspectionSpec> FilterSpecs(string tenantId, string? objectCode, string? keyword) =>
        BuildFilterSpecsQuery(db, tenantId, objectCode, keyword).ToList();

    public InspectionSpec? FindSpec(string tenantId, string code) =>
        db.InspectionSpecs.FirstOrDefault(s => s.TenantId == tenantId && s.Code == code);

    public void SaveSpec(InspectionSpec s) =>
        EfStoreOps.Upsert(db, db.InspectionSpecs, s, x => x.TenantId == s.TenantId && x.Code == s.Code);

    public bool DeleteSpec(string tenantId, string code) =>
        db.InspectionSpecs.Where(s => s.TenantId == tenantId && s.Code == code).ExecuteDelete() > 0;

    // === 等级 M04.F08 ===

    public IReadOnlyList<InspectionGrade> FilterGrades(string tenantId, string? objectCode, string? keyword) =>
        BuildFilterGradesQuery(db, tenantId, objectCode, keyword).ToList();

    public InspectionGrade? FindGrade(string tenantId, string code) =>
        db.InspectionGrades.FirstOrDefault(g => g.TenantId == tenantId && g.Code == code);

    public void SaveGrade(InspectionGrade g) =>
        EfStoreOps.Upsert(db, db.InspectionGrades, g, x => x.TenantId == g.TenantId && x.Code == g.Code);

    public bool DeleteGrade(string tenantId, string code) =>
        db.InspectionGrades.Where(g => g.TenantId == tenantId && g.Code == code).ExecuteDelete() > 0;

    // === 牌号 M04.F09（SET NULL 语义由 DB ON DELETE SET NULL 承担） ===

    public IReadOnlyList<InspectionBrand> FilterBrands(string tenantId, string? objectCode, string? keyword) =>
        BuildFilterBrandsQuery(db, tenantId, objectCode, keyword).ToList();

    public InspectionBrand? FindBrand(string tenantId, string code) =>
        db.InspectionBrands.FirstOrDefault(b => b.TenantId == tenantId && b.Code == code);

    public void SaveBrand(InspectionBrand b) =>
        EfStoreOps.Upsert(db, db.InspectionBrands, b, x => x.TenantId == b.TenantId && x.Code == b.Code);

    public bool DeleteBrand(string tenantId, string code) =>
        db.InspectionBrands.Where(b => b.TenantId == tenantId && b.Code == code).ExecuteDelete() > 0;

    // === 查询构建器：internal 供翻译性测试（EfQueryTranslatabilityTest）逐个 ToQueryString ===

    internal static IQueryable<InspectionModel> BuildFilterModelsQuery(
        LabDbContext db, string tenantId, string? objectCode, string? keyword) =>
        db.InspectionModels
            .Where(m => m.TenantId == tenantId)
            .Where(m => objectCode == null || objectCode == "" || m.InspectionObjectCode == objectCode)
            .WhereKw(m => m.Code, m => m.Name, keyword)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Code);

    internal static IQueryable<InspectionSpec> BuildFilterSpecsQuery(
        LabDbContext db, string tenantId, string? objectCode, string? keyword) =>
        db.InspectionSpecs
            .Where(s => s.TenantId == tenantId)
            .Where(s => objectCode == null || objectCode == "" || s.InspectionObjectCode == objectCode)
            .WhereKw(s => s.Code, s => s.Name, keyword)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Code);

    internal static IQueryable<InspectionGrade> BuildFilterGradesQuery(
        LabDbContext db, string tenantId, string? objectCode, string? keyword) =>
        db.InspectionGrades
            .Where(g => g.TenantId == tenantId)
            .Where(g => objectCode == null || objectCode == "" || g.InspectionObjectCode == objectCode)
            .WhereKw(g => g.Code, g => g.Name, keyword)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Code);

    internal static IQueryable<InspectionBrand> BuildFilterBrandsQuery(
        LabDbContext db, string tenantId, string? objectCode, string? keyword) =>
        db.InspectionBrands
            .Where(b => b.TenantId == tenantId)
            .Where(b => objectCode == null || objectCode == "" || b.InspectionObjectCode == objectCode)
            .WhereKw(b => b.Code, b => b.Name, keyword)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Code);
}

/// <summary>EF 仓储共用：upsert（先查后 Add/SetValues）+ keyword 谓词。</summary>
internal static class EfStoreOps
{
    public static void Upsert<T>(LabDbContext db, DbSet<T> set, T entity,
        System.Linq.Expressions.Expression<Func<T, bool>> match) where T : class
    {
        var existing = set.FirstOrDefault(match);
        if (existing is null)
        {
            set.Add(entity);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(entity);
        }

        db.SaveChanges();
    }
}
