namespace Lab.AspNetCore.Persistence;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Microsoft.EntityFrameworkCore;

public sealed class EfDictionaryStore(LabDbContext db) : IDictionaryStore
{
    private static bool Kw(string? code, string? name, string? keyword) =>
        keyword == null || keyword == ""
        || (code != null && code.ToLower().Contains(keyword.ToLower()))
        || (name != null && name.ToLower().Contains(keyword.ToLower()));

    // === 专项 M06.F01 ===

    public IReadOnlyList<InspectionSpecialty> FilterSpecialties(string? keyword) =>
        db.InspectionSpecialties
            .Where(s => Kw(s.Code, s.Name, keyword))
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Code)
            .ToList();

    public InspectionSpecialty? FindSpecialty(string code) => db.InspectionSpecialties.Find(code);

    public void SaveSpecialty(InspectionSpecialty s) =>
        EfStoreOps.Upsert(db, db.InspectionSpecialties, s, x => x.Code == s.Code);

    public bool DeleteSpecialty(string code)
    {
        var existing = db.InspectionSpecialties.Find(code);
        if (existing is null)
        {
            return false;
        }

        db.InspectionSpecialties.Remove(existing); // DB RESTRICT 拦被引用删除
        db.SaveChanges();
        return true;
    }

    public bool SpecialtyExists(string code) => db.InspectionSpecialties.Any(s => s.Code == code);

    // === 参数 M06.F03 ===

    public IReadOnlyList<InspectionParameter> FilterParameters(string? keyword, InspectionParameterSourceType? sourceType) =>
        db.InspectionParameters
            .Where(p => sourceType == null || p.SourceType == sourceType)
            .Where(p => Kw(p.Code, p.Name, keyword))
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Code)
            .ToList();

    public InspectionParameter? FindParameter(string code) => db.InspectionParameters.Find(code);

    public void SaveParameter(InspectionParameter p) =>
        EfStoreOps.Upsert(db, db.InspectionParameters, p, x => x.Code == p.Code);

    public bool DeleteParameter(string code)
    {
        var existing = db.InspectionParameters.Find(code);
        if (existing is null)
        {
            return false;
        }

        db.InspectionParameters.Remove(existing);
        db.SaveChanges();
        return true;
    }

    // === 标准 M06.F04 ===

    public IReadOnlyList<InspectionStandard> FilterStandards(string? keyword, InspectionStandardStatus? status) =>
        db.InspectionStandards
            .Where(s => status == null || s.Status == status)
            .Where(s => Kw(s.Code, s.Name, keyword))
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Code)
            .ToList();

    public InspectionStandard? FindStandard(string code) => db.InspectionStandards.Find(code);

    public void SaveStandard(InspectionStandard s) =>
        EfStoreOps.Upsert(db, db.InspectionStandards, s, x => x.Code == s.Code);

    public bool DeleteStandard(string code)
    {
        var existing = db.InspectionStandards.Find(code);
        if (existing is null)
        {
            return false;
        }

        db.InspectionStandards.Remove(existing);
        db.SaveChanges();
        return true;
    }

    // === 报告名称 M06.F07 ===

    public IReadOnlyList<InspectionReportName> FilterReportNames(string? keyword) =>
        db.InspectionReportNames
            .Where(r => Kw(r.Code, r.Name, keyword))
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Code)
            .ToList();

    public InspectionReportName? FindReportName(string code) => db.InspectionReportNames.Find(code);

    public void SaveReportName(InspectionReportName r) =>
        EfStoreOps.Upsert(db, db.InspectionReportNames, r, x => x.Code == r.Code);

    public bool DeleteReportName(string code)
    {
        var existing = db.InspectionReportNames.Find(code);
        if (existing is null)
        {
            return false;
        }

        db.InspectionReportNames.Remove(existing);
        db.SaveChanges();
        return true;
    }

    // === 参数界面 M06.F08 ===

    public IReadOnlyList<ParamInterface> FilterInterfaces(string? keyword) =>
        db.ParamInterfaces
            .Where(i => Kw(i.Code, i.Name, keyword))
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Code)
            .ToList();

    public ParamInterface? FindInterface(string code) => db.ParamInterfaces.Find(code);

    public void SaveInterface(ParamInterface i) =>
        EfStoreOps.Upsert(db, db.ParamInterfaces, i, x => x.Code == i.Code);

    public bool DeleteInterface(string code)
    {
        var existing = db.ParamInterfaces.Find(code);
        if (existing is null)
        {
            return false;
        }

        db.ParamInterfaces.Remove(existing);
        db.SaveChanges();
        return true;
    }

    // === 项目 M06.F02 ===

    public IReadOnlyList<InspectionObject> FilterObjects(string? specialtyCode, string? keyword) =>
        db.InspectionObjects
            .Where(o => specialtyCode == null || specialtyCode == "" || o.InspectionSpecialtyCode == specialtyCode)
            .Where(o => Kw(o.Code, o.Name, keyword))
            .OrderBy(o => o.SortOrder).ThenBy(o => o.Code)
            .ToList();

    public InspectionObject? FindObject(string code) => db.InspectionObjects.Find(code);

    public void SaveObject(InspectionObject o) =>
        EfStoreOps.Upsert(db, db.InspectionObjects, o, x => x.Code == o.Code);

    public bool DeleteObject(string code)
    {
        var existing = db.InspectionObjects.Find(code);
        if (existing is null)
        {
            return false;
        }

        db.InspectionObjects.Remove(existing);
        db.SaveChanges();
        return true;
    }
}

/// <summary>
/// B6 八组 junction。link 前置 FK 校验（DB 真实 FK 生效：V008/V009/V010/V011），
/// 幽灵 code 转 ArgumentException（全局异常映射 400），避免裸 23503 变 500。
/// unlink miss 返回 false（service 层转 404），upsert 语义与内存版一致。
/// </summary>
public sealed class EfJunctionStore(LabDbContext db) : IJunctionStore
{
    private void Require(bool exists, string what, string code)
    {
        if (!exists)
        {
            throw new ArgumentException($"{what} {code} not found");
        }
    }

    public void SaveSpecialtyObject(SpecialtyObjectLink l)
    {
        Require(db.InspectionSpecialties.Any(s => s.Code == l.InspectionSpecialtyCode), "specialty", l.InspectionSpecialtyCode);
        Require(db.InspectionObjects.Any(o => o.Code == l.InspectionObjectCode), "object", l.InspectionObjectCode);
        EfStoreOps.Upsert(db, db.SpecialtyObjectLinks, l,
            x => x.InspectionSpecialtyCode == l.InspectionSpecialtyCode
                && x.InspectionObjectCode == l.InspectionObjectCode);
    }

    public bool DeleteSpecialtyObject(string spec, string obj) =>
        db.SpecialtyObjectLinks
            .Where(x => x.InspectionSpecialtyCode == spec && x.InspectionObjectCode == obj)
            .ExecuteDelete() > 0;

    public IReadOnlyList<SpecialtyObjectLink> ListSpecialtyObject(string? spec) =>
        string.IsNullOrWhiteSpace(spec)
            ? db.SpecialtyObjectLinks.AsNoTracking().ToList()
            : db.SpecialtyObjectLinks.AsNoTracking().Where(x => x.InspectionSpecialtyCode == spec).ToList();

    public void SaveObjectParameter(ObjectParameterLink l)
    {
        Require(db.InspectionObjects.Any(o => o.Code == l.InspectionObjectCode), "object", l.InspectionObjectCode);
        Require(db.InspectionParameters.Any(p => p.Code == l.InspectionParameterCode), "parameter", l.InspectionParameterCode);
        EfStoreOps.Upsert(db, db.ObjectParameterLinks, l,
            x => x.InspectionObjectCode == l.InspectionObjectCode
                && x.InspectionParameterCode == l.InspectionParameterCode);
    }

    public bool DeleteObjectParameter(string obj, string param) =>
        db.ObjectParameterLinks
            .Where(x => x.InspectionObjectCode == obj && x.InspectionParameterCode == param)
            .ExecuteDelete() > 0;

    public IReadOnlyList<ObjectParameterLink> ListObjectParameter(string? obj, string? param) =>
        db.ObjectParameterLinks.AsNoTracking()
            .Where(x => obj == null || x.InspectionObjectCode == obj)
            .Where(x => param == null || x.InspectionParameterCode == param)
            .ToList();

    public void SaveObjectStandard(ObjectStandardLink l)
    {
        Require(db.InspectionObjects.Any(o => o.Code == l.InspectionObjectCode), "object", l.InspectionObjectCode);
        Require(db.InspectionStandards.Any(s => s.Code == l.InspectionStandardCode), "standard", l.InspectionStandardCode);
        EfStoreOps.Upsert(db, db.ObjectStandardLinks, l,
            x => x.InspectionObjectCode == l.InspectionObjectCode
                && x.InspectionStandardCode == l.InspectionStandardCode
                && x.Role == l.Role);
    }

    public bool DeleteObjectStandard(string obj, string std, string role) =>
        db.ObjectStandardLinks
            .Where(x => x.InspectionObjectCode == obj && x.InspectionStandardCode == std && x.Role == ParseRole(role))
            .ExecuteDelete() > 0;

    public IReadOnlyList<ObjectStandardLink> ListObjectStandard(string? obj, InspectionStandardRole? role) =>
        db.ObjectStandardLinks.AsNoTracking()
            .Where(x => obj == null || x.InspectionObjectCode == obj)
            .Where(x => role == null || x.Role == role)
            .ToList();

    public void SaveStandardParameter(StandardParameterLink l)
    {
        Require(db.InspectionStandards.Any(s => s.Code == l.InspectionStandardCode), "standard", l.InspectionStandardCode);
        Require(db.InspectionParameters.Any(p => p.Code == l.InspectionParameterCode), "parameter", l.InspectionParameterCode);
        EfStoreOps.Upsert(db, db.StandardParameterLinks, l,
            x => x.InspectionStandardCode == l.InspectionStandardCode
                && x.InspectionParameterCode == l.InspectionParameterCode);
    }

    public bool DeleteStandardParameter(string std, string param) =>
        db.StandardParameterLinks
            .Where(x => x.InspectionStandardCode == std && x.InspectionParameterCode == param)
            .ExecuteDelete() > 0;

    public IReadOnlyList<StandardParameterLink> ListStandardParameter(string? std, string? param) =>
        db.StandardParameterLinks.AsNoTracking()
            .Where(x => std == null || x.InspectionStandardCode == std)
            .Where(x => param == null || x.InspectionParameterCode == param)
            .ToList();

    public void SaveObjectReportName(ObjectReportNameLink l)
    {
        Require(db.InspectionObjects.Any(o => o.Code == l.InspectionObjectCode), "object", l.InspectionObjectCode);
        Require(db.InspectionReportNames.Any(r => r.Code == l.ReportNameCode), "report-name", l.ReportNameCode);
        EfStoreOps.Upsert(db, db.ObjectReportNameLinks, l,
            x => x.InspectionObjectCode == l.InspectionObjectCode
                && x.ReportNameCode == l.ReportNameCode);
    }

    public bool DeleteObjectReportName(string obj, string report) =>
        db.ObjectReportNameLinks
            .Where(x => x.InspectionObjectCode == obj && x.ReportNameCode == report)
            .ExecuteDelete() > 0;

    public void SaveReportNameStandard(ReportNameStandardLink l)
    {
        Require(db.InspectionReportNames.Any(r => r.Code == l.ReportNameCode), "report-name", l.ReportNameCode);
        Require(db.InspectionStandards.Any(s => s.Code == l.InspectionStandardCode), "standard", l.InspectionStandardCode);
        EfStoreOps.Upsert(db, db.ReportNameStandardLinks, l,
            x => x.ReportNameCode == l.ReportNameCode
                && x.InspectionStandardCode == l.InspectionStandardCode
                && x.Role == l.Role);
    }

    public bool DeleteReportNameStandard(string report, string std, string role) =>
        db.ReportNameStandardLinks
            .Where(x => x.ReportNameCode == report && x.InspectionStandardCode == std && x.Role == ParseRole(role))
            .ExecuteDelete() > 0;

    public void SaveReportNameParameter(ReportNameParameterLink l)
    {
        Require(db.InspectionReportNames.Any(r => r.Code == l.ReportNameCode), "report-name", l.ReportNameCode);
        Require(db.InspectionParameters.Any(p => p.Code == l.InspectionParameterCode), "parameter", l.InspectionParameterCode);
        EfStoreOps.Upsert(db, db.ReportNameParameterLinks, l,
            x => x.ReportNameCode == l.ReportNameCode
                && x.InspectionParameterCode == l.InspectionParameterCode);
    }

    public bool DeleteReportNameParameter(string report, string param) =>
        db.ReportNameParameterLinks
            .Where(x => x.ReportNameCode == report && x.InspectionParameterCode == param)
            .ExecuteDelete() > 0;

    public void SaveParamInterface(ParamInterfaceLink l)
    {
        Require(db.InspectionParameters.Any(p => p.Code == l.InspectionParameterCode), "parameter", l.InspectionParameterCode);
        Require(db.ParamInterfaces.Any(i => i.Code == l.ParamInterfaceCode), "param-interface", l.ParamInterfaceCode);
        EfStoreOps.Upsert(db, db.ParamInterfaceLinks, l,
            x => x.InspectionParameterCode == l.InspectionParameterCode
                && x.ParamInterfaceCode == l.ParamInterfaceCode);
    }

    private static InspectionStandardRole ParseRole(string role) =>
        Enum.Parse<InspectionStandardRole>(role, true); // wire 值 = 成员名（TESTING/JUDGMENT）

    public bool DeleteParamInterface(string param, string iface) =>
        db.ParamInterfaceLinks
            .Where(x => x.InspectionParameterCode == param && x.ParamInterfaceCode == iface)
            .ExecuteDelete() > 0;
}
