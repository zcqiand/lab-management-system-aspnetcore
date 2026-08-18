namespace Lab.AspNetCore.Data;

using System.Collections.Concurrent;
using Lab.AspNetCore.Controllers.Generated;

/// <summary>
/// B2 内存存储。语义镜像 lab-springboot B2 的 JPA repository（filter JPQL）：
/// tenant 精确 + objectCode 精确（空串不过滤）+ keyword 大小写不敏包含 code/name，
/// 排序 sortOrder asc, code asc。
///
/// B2 阶段无 DB（与 B1 ConfigUserDirectory 同哲学）；接口抽象保持 filter/find/save/delete
/// 语义，后续换 EF Core 仓储时 service/controller/fnTest 不动。
/// </summary>
public sealed class InMemoryCatalogStore : ICatalogStore
{
    private readonly ConcurrentDictionary<(string TenantId, string Code), InspectionModel> _models = new();
    private readonly ConcurrentDictionary<(string TenantId, string Code), InspectionSpec> _specs = new();
    private readonly ConcurrentDictionary<(string TenantId, string Code), InspectionGrade> _grades = new();
    private readonly ConcurrentDictionary<(string TenantId, string Code), InspectionBrand> _brands = new();

    private static string N(string? s) => s ?? "";

    // === 型号 M04.F06 ===

    public IReadOnlyList<InspectionModel> FilterModels(string tenantId, string? objectCode, string? keyword) =>
        _models.Values
            .Where(m => m.TenantId == tenantId)
            .Where(m => N(objectCode) == "" || m.InspectionObjectCode == N(objectCode))
            .Where(m => Kw(m.Code, m.Name, keyword))
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Code)
            .ToList();

    public InspectionModel? FindModel(string tenantId, string code) =>
        _models.TryGetValue((tenantId, code), out var m) ? m : null;

    public void SaveModel(InspectionModel m) => _models[(m.TenantId, m.Code)] = m;

    public bool DeleteModel(string tenantId, string code) => _models.TryRemove((tenantId, code), out _);

    // === 规格 M04.F07 ===

    public IReadOnlyList<InspectionSpec> FilterSpecs(string tenantId, string? objectCode, string? keyword) =>
        _specs.Values
            .Where(s => s.TenantId == tenantId)
            .Where(s => N(objectCode) == "" || s.InspectionObjectCode == N(objectCode))
            .Where(s => Kw(s.Code, s.Name, keyword))
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Code)
            .ToList();

    public InspectionSpec? FindSpec(string tenantId, string code) =>
        _specs.TryGetValue((tenantId, code), out var s) ? s : null;

    public void SaveSpec(InspectionSpec s) => _specs[(s.TenantId, s.Code)] = s;

    public bool DeleteSpec(string tenantId, string code) => _specs.TryRemove((tenantId, code), out _);

    // === 等级 M04.F08 ===

    public IReadOnlyList<InspectionGrade> FilterGrades(string tenantId, string? objectCode, string? keyword) =>
        _grades.Values
            .Where(g => g.TenantId == tenantId)
            .Where(g => N(objectCode) == "" || g.InspectionObjectCode == N(objectCode))
            .Where(g => Kw(g.Code, g.Name, keyword))
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Code)
            .ToList();

    public InspectionGrade? FindGrade(string tenantId, string code) =>
        _grades.TryGetValue((tenantId, code), out var g) ? g : null;

    public void SaveGrade(InspectionGrade g) => _grades[(g.TenantId, g.Code)] = g;

    public bool DeleteGrade(string tenantId, string code) => _grades.TryRemove((tenantId, code), out _);

    // === 牌号 M04.F09 ===

    /// <summary>DELETE brands 时若被 technical_requirements 引用 → SET NULL（V011 FK 语义）。</summary>
    public event Action<string>? BrandDeleted;

    public IReadOnlyList<InspectionBrand> FilterBrands(string tenantId, string? objectCode, string? keyword) =>
        _brands.Values
            .Where(b => b.TenantId == tenantId)
            .Where(b => N(objectCode) == "" || b.InspectionObjectCode == N(objectCode))
            .Where(b => Kw(b.Code, b.Name, keyword))
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Code)
            .ToList();

    public InspectionBrand? FindBrand(string tenantId, string code) =>
        _brands.TryGetValue((tenantId, code), out var b) ? b : null;

    public void SaveBrand(InspectionBrand b) => _brands[(b.TenantId, b.Code)] = b;

    public bool DeleteBrand(string tenantId, string code)
    {
        var removed = _brands.TryRemove((tenantId, code), out _);
        if (removed)
        {
            BrandDeleted?.Invoke(code);
        }
        return removed;
    }

    // === keyword 共用：大小写不敏包含 code 或 name（空串不过滤） ===

    internal static bool Kw(string? code, string? name, string? keyword)
    {
        var kw = N(keyword).ToLowerInvariant();
        if (kw == "")
        {
            return true;
        }

        return (code ?? "").ToLowerInvariant().Contains(kw)
            || (name ?? "").ToLowerInvariant().Contains(kw);
    }
}
