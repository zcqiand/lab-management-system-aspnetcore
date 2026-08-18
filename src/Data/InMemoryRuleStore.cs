namespace Lab.AspNetCore.Data;

using System.Collections.Concurrent;
using Lab.AspNetCore.Controllers.Generated;

/// <summary>
/// M06.F05 计算规则内存存储。复合主键 = (inspectionObjectCode, inspectionParameterCode)，
/// 平台级无 tenant（镜像 springboot inspection_calculation_rules V009）。
/// list 两个可选过滤（空串不过滤），排序 sortOrder asc。
/// </summary>
public sealed class InMemoryRuleStore
{
    private readonly ConcurrentDictionary<(string Obj, string Param), CalculationRule> _rules = new();

    private static string N(string? s) => s ?? "";

    public IReadOnlyList<CalculationRule> Filter(string? objectCode, string? parameterCode) =>
        _rules.Values
            .Where(r => N(objectCode) == "" || r.InspectionObjectCode == N(objectCode))
            .Where(r => N(parameterCode) == "" || r.InspectionParameterCode == N(parameterCode))
            .OrderBy(r => r.SortOrder)
            .ToList();

    public CalculationRule? Find(string objectCode, string parameterCode) =>
        _rules.TryGetValue((objectCode, parameterCode), out var r) ? r : null;

    public void Save(CalculationRule r) => _rules[(r.InspectionObjectCode, r.InspectionParameterCode)] = r;

    public bool Delete(string objectCode, string parameterCode) =>
        _rules.TryRemove((objectCode, parameterCode), out _);
}

/// <summary>
/// M06.F06 技术要求内存存储。业务三键 = (object, parameter, judgmentStandard) + tenant 过滤
/// （镜像 springboot inspection_technical_requirements V005/V012）。
/// list 四过滤（null=不过滤），排序 sortOrder, objectCode, parameterCode, standardCode。
/// brand 删除时四维度引用 SET NULL（经 InMemoryCatalogStore.BrandDeleted 事件）。
/// </summary>
public sealed class InMemoryRequirementStore
{
    private readonly ConcurrentDictionary<(string Tenant, string Obj, string Param, string Std), TechnicalRequirement> _rows = new();

    public IReadOnlyList<TechnicalRequirement> Filter(
        string tenantId, string? objectCode, string? parameterCode, string? standardCode,
        RequirementVerificationStatus? status)
    {
        static string N(string? s) => s ?? "";
        return _rows.Values
            .Where(t => t.TenantId == tenantId)
            .Where(t => N(objectCode) == "" || t.InspectionObjectCode == N(objectCode))
            .Where(t => N(parameterCode) == "" || t.InspectionParameterCode == N(parameterCode))
            .Where(t => N(standardCode) == "" || t.JudgmentStandardCode == N(standardCode))
            .Where(t => status is null || t.VerificationStatus == status)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.InspectionObjectCode)
            .ThenBy(t => t.InspectionParameterCode)
            .ThenBy(t => t.JudgmentStandardCode)
            .ToList();
    }

    public TechnicalRequirement? Find(string tenantId, string objectCode, string parameterCode, string standardCode) =>
        _rows.TryGetValue((tenantId, objectCode, parameterCode, standardCode), out var t) ? t : null;

    public void Save(TechnicalRequirement t) =>
        _rows[(t.TenantId, t.InspectionObjectCode, t.InspectionParameterCode, t.JudgmentStandardCode)] = t;

    public bool Delete(string tenantId, string objectCode, string parameterCode, string standardCode) =>
        _rows.TryRemove((tenantId, objectCode, parameterCode, standardCode), out _);

    /// <summary>品牌码删除时，把引用该 brand 的技术要求行 brand 列置空（FK SET NULL 语义）。</summary>
    public void OnBrandDeleted(string brandCode)
    {
        foreach (var key in _rows.Keys.Where(k => _rows[k].Brand == brandCode).ToList())
        {
            _rows[key].Brand = "";
        }
    }
}
