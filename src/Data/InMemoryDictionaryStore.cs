namespace Lab.AspNetCore.Data;

using System.Collections.Concurrent;
using Lab.AspNetCore.Controllers.Generated;

/// <summary>
/// B5 检测能力字典内存存储（专项/参数/标准/报告名称/参数界面，全平台级无 tenant）。
/// 语义镜像 springboot：keyword 模糊 code/name；aliases/extFields/config 三个 jsonb
/// 在生成 DTO 侧直接是 List/Dictionary（非 jsonb 字符串）。
/// </summary>
public sealed class InMemoryDictionaryStore : IDictionaryStore
{
    private readonly ConcurrentDictionary<string, InspectionSpecialty> _specialties = new();
    private readonly ConcurrentDictionary<string, InspectionParameter> _parameters = new();
    private readonly ConcurrentDictionary<string, InspectionStandard> _standards = new();
    private readonly ConcurrentDictionary<string, InspectionReportName> _reportNames = new();
    private readonly ConcurrentDictionary<string, ParamInterface> _interfaces = new();
    private readonly ConcurrentDictionary<string, InspectionObject> _objects = new();

    internal static bool Kw(string? code, string? name, string? keyword)
    {
        var kw = (keyword ?? "").ToLowerInvariant();
        if (kw == "")
        {
            return true;
        }

        return (code ?? "").ToLowerInvariant().Contains(kw)
            || (name ?? "").ToLowerInvariant().Contains(kw);
    }

    // === 专项 M06.F01 ===

    public IReadOnlyList<InspectionSpecialty> FilterSpecialties(string? keyword) =>
        _specialties.Values.Where(s => Kw(s.Code, s.Name, keyword)).OrderBy(s => s.SortOrder).ThenBy(s => s.Code).ToList();

    public InspectionSpecialty? FindSpecialty(string code) => _specialties.TryGetValue(code, out var s) ? s : null;

    public void SaveSpecialty(InspectionSpecialty s) => _specialties[s.Code] = s;

    public bool DeleteSpecialty(string code) => _specialties.TryRemove(code, out _);

    public bool SpecialtyExists(string code) => _specialties.ContainsKey(code);

    // === 参数 M06.F03 ===

    public IReadOnlyList<InspectionParameter> FilterParameters(string? keyword, InspectionParameterSourceType? sourceType) =>
        _parameters.Values
            .Where(p => Kw(p.Code, p.Name, keyword))
            .Where(p => sourceType is null || p.SourceType == sourceType)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Code)
            .ToList();

    public InspectionParameter? FindParameter(string code) => _parameters.TryGetValue(code, out var p) ? p : null;

    public void SaveParameter(InspectionParameter p) => _parameters[p.Code] = p;

    public bool DeleteParameter(string code) => _parameters.TryRemove(code, out _);

    // === 标准 M06.F04 ===

    public IReadOnlyList<InspectionStandard> FilterStandards(string? keyword, InspectionStandardStatus? status) =>
        _standards.Values
            .Where(s => Kw(s.Code, s.Name, keyword))
            .Where(s => status is null || s.Status == status)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Code)
            .ToList();

    public InspectionStandard? FindStandard(string code) => _standards.TryGetValue(code, out var s) ? s : null;

    public void SaveStandard(InspectionStandard s) => _standards[s.Code] = s;

    public bool DeleteStandard(string code) => _standards.TryRemove(code, out _);

    // === 报告名称 M06.F07 ===

    public IReadOnlyList<InspectionReportName> FilterReportNames(string? keyword) =>
        _reportNames.Values.Where(r => Kw(r.Code, r.Name, keyword)).OrderBy(r => r.SortOrder).ThenBy(r => r.Code).ToList();

    public InspectionReportName? FindReportName(string code) => _reportNames.TryGetValue(code, out var r) ? r : null;

    public void SaveReportName(InspectionReportName r) => _reportNames[r.Code] = r;

    public bool DeleteReportName(string code) => _reportNames.TryRemove(code, out _);

    // === 参数界面 M06.F08 ===

    public IReadOnlyList<ParamInterface> FilterInterfaces(string? keyword) =>
        _interfaces.Values.Where(i => Kw(i.Code, i.Name, keyword)).OrderBy(i => i.SortOrder).ThenBy(i => i.Code).ToList();

    public ParamInterface? FindInterface(string code) => _interfaces.TryGetValue(code, out var i) ? i : null;

    public void SaveInterface(ParamInterface i) => _interfaces[i.Code] = i;

    public bool DeleteInterface(string code) => _interfaces.TryRemove(code, out _);

    // === 检测项目 objects M06.F02 ===

    public IReadOnlyList<InspectionObject> FilterObjects(string? specialtyCode, string? keyword) =>
        _objects.Values
            .Where(o => string.IsNullOrEmpty(specialtyCode) || o.InspectionSpecialtyCode == specialtyCode)
            .Where(o => Kw(o.Code, o.Name, keyword))
            .OrderBy(o => o.SortOrder).ThenBy(o => o.Code)
            .ToList();

    public InspectionObject? FindObject(string code) => _objects.TryGetValue(code, out var o) ? o : null;

    public void SaveObject(InspectionObject o) => _objects[o.Code] = o;

    public bool DeleteObject(string code) => _objects.TryRemove(code, out _);
}

/// <summary>
/// B6 八组 junction 内存存储。语义镜像 springboot InspectionJunctionService：
/// link = upsert（同 PK 重复不报错，覆盖更新）；unlink miss → 404。
/// role 在 PK 内的两组（object-standard / report-name-standard）：同 code 对不同 role 是两行。
/// </summary>
public sealed class InMemoryJunctionStore : IJunctionStore
{
    private readonly ConcurrentDictionary<(string Spec, string Obj), SpecialtyObjectLink> _specialtyObject = new();
    private readonly ConcurrentDictionary<(string Obj, string Param), ObjectParameterLink> _objectParameter = new();
    private readonly ConcurrentDictionary<(string Obj, string Std, string Role), ObjectStandardLink> _objectStandard = new();
    private readonly ConcurrentDictionary<(string Std, string Param), StandardParameterLink> _standardParameter = new();
    private readonly ConcurrentDictionary<(string Obj, string Report), ObjectReportNameLink> _objectReportName = new();
    private readonly ConcurrentDictionary<(string Report, string Std, string Role), ReportNameStandardLink> _reportNameStandard = new();
    private readonly ConcurrentDictionary<(string Report, string Param), ReportNameParameterLink> _reportNameParameter = new();
    private readonly ConcurrentDictionary<(string Param, string Interface), ParamInterfaceLink> _paramInterface = new();

    // 每组暴露 Save / Exists / Delete。upsert 语义由 ConcurrentDictionary 索引赋值天然提供。

    public void SaveSpecialtyObject(SpecialtyObjectLink l) => _specialtyObject[(l.InspectionSpecialtyCode, l.InspectionObjectCode)] = l;
    public bool DeleteSpecialtyObject(string spec, string obj) => _specialtyObject.TryRemove((spec, obj), out _);
    public IReadOnlyList<SpecialtyObjectLink> ListSpecialtyObject(string? spec) =>
        string.IsNullOrWhiteSpace(spec)
            ? _specialtyObject.Values.ToList()
            : _specialtyObject.Values.Where(l => l.InspectionSpecialtyCode == spec).ToList();

    public void SaveObjectParameter(ObjectParameterLink l) => _objectParameter[(l.InspectionObjectCode, l.InspectionParameterCode)] = l;
    public bool DeleteObjectParameter(string obj, string param) => _objectParameter.TryRemove((obj, param), out _);
    public IReadOnlyList<ObjectParameterLink> ListObjectParameter(string? obj, string? param) =>
        _objectParameter.Values
            .Where(l => obj == null || l.InspectionObjectCode == obj)
            .Where(l => param == null || l.InspectionParameterCode == param)
            .ToList();

    public void SaveObjectStandard(ObjectStandardLink l) => _objectStandard[(l.InspectionObjectCode, l.InspectionStandardCode, l.Role.ToString())] = l;
    public bool DeleteObjectStandard(string obj, string std, string role) => _objectStandard.TryRemove((obj, std, role), out _);
    public IReadOnlyList<ObjectStandardLink> ListObjectStandard(string? obj, InspectionStandardRole? role) =>
        _objectStandard.Values
            .Where(l => obj == null || l.InspectionObjectCode == obj)
            .Where(l => role == null || l.Role == role)
            .ToList();

    public void SaveStandardParameter(StandardParameterLink l) => _standardParameter[(l.InspectionStandardCode, l.InspectionParameterCode)] = l;
    public bool DeleteStandardParameter(string std, string param) => _standardParameter.TryRemove((std, param), out _);
    public IReadOnlyList<StandardParameterLink> ListStandardParameter(string? std, string? param) =>
        _standardParameter.Values
            .Where(l => std == null || l.InspectionStandardCode == std)
            .Where(l => param == null || l.InspectionParameterCode == param)
            .ToList();

    public void SaveObjectReportName(ObjectReportNameLink l) => _objectReportName[(l.InspectionObjectCode, l.ReportNameCode)] = l;
    public bool DeleteObjectReportName(string obj, string report) => _objectReportName.TryRemove((obj, report), out _);
    public IReadOnlyList<ObjectReportNameLink> ListObjectReportName(string? obj, string? report) =>
        _objectReportName.Values
            .Where(l => obj == null || l.InspectionObjectCode == obj)
            .Where(l => report == null || l.ReportNameCode == report)
            .ToList();

    public void SaveReportNameStandard(ReportNameStandardLink l) => _reportNameStandard[(l.ReportNameCode, l.InspectionStandardCode, l.Role.ToString())] = l;
    public bool DeleteReportNameStandard(string report, string std, string role) => _reportNameStandard.TryRemove((report, std, role), out _);
    public IReadOnlyList<ReportNameStandardLink> ListReportNameStandard(string? report, InspectionStandardRole? role) =>
        _reportNameStandard.Values
            .Where(l => report == null || l.ReportNameCode == report)
            .Where(l => role == null || l.Role == role)
            .ToList();

    public void SaveReportNameParameter(ReportNameParameterLink l) => _reportNameParameter[(l.ReportNameCode, l.InspectionParameterCode)] = l;
    public bool DeleteReportNameParameter(string report, string param) => _reportNameParameter.TryRemove((report, param), out _);
    public IReadOnlyList<ReportNameParameterLink> ListReportNameParameter(string? report, string? param) =>
        _reportNameParameter.Values
            .Where(l => report == null || l.ReportNameCode == report)
            .Where(l => param == null || l.InspectionParameterCode == param)
            .ToList();

    public void SaveParamInterface(ParamInterfaceLink l) => _paramInterface[(l.InspectionParameterCode, l.ParamInterfaceCode)] = l;
    public bool DeleteParamInterface(string param, string iface) => _paramInterface.TryRemove((param, iface), out _);
    public IReadOnlyList<ParamInterfaceLink> ListParamInterface(string? param, string? iface) =>
        _paramInterface.Values
            .Where(l => param == null || l.InspectionParameterCode == param)
            .Where(l => iface == null || l.ParamInterfaceCode == iface)
            .ToList();
}
