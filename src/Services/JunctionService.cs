namespace Lab.AspNetCore.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// B6 八组 junction link/unlink。语义镜像 springboot InspectionJunctionService：
/// link = upsert（同 PK 重复不报错覆盖）；unlink miss → 404。
/// role 在 PK 内的两组：object-standard / report-name-standard。
/// </summary>
public sealed class JunctionService(InMemoryJunctionStore store)
{
    private static string RoleKey(InspectionStandardRole role) => role.ToString().ToUpperInvariant();

    // === specialty-object（M06.F02.I05/I06） ===

    public void LinkSpecialtyObject(SpecialtyObjectLink body) => store.SaveSpecialtyObject(body);

    public void UnlinkSpecialtyObject(SpecialtyObjectLink body)
    {
        if (!store.DeleteSpecialtyObject(body.InspectionSpecialtyCode, body.InspectionObjectCode))
        {
            throw new KeyNotFoundException("specialty-object link not found");
        }
    }

    // === object-parameter（M06.F02.I07/I08） ===

    public void LinkObjectParameter(ObjectParameterLink body)
    {
        body.QualificationLevel = body.QualificationLevel == default ? QualificationLevel.QUALIFIED : body.QualificationLevel;
        store.SaveObjectParameter(body);
    }

    public void UnlinkObjectParameter(string objectCode, string parameterCode)
    {
        if (!store.DeleteObjectParameter(objectCode, parameterCode))
        {
            throw new KeyNotFoundException("object-parameter link not found");
        }
    }

    // === object-standard（M06.F01.I05/I06，role 在 PK） ===

    public void LinkObjectStandard(ObjectStandardLink body)
    {
        // 注意：TESTING=0 是枚举默认值，生成 DTO 无 null 态 —— C# 侧无法区分
        // 「未传」和「显式传 TESTING」，与 springboot（可选 role 校验）在此分叉：
        // 默认即 TESTING，不再抛 ArgumentException。
        store.SaveObjectStandard(body);
    }

    public void UnlinkObjectStandard(string objectCode, string standardCode, InspectionStandardRole role)
    {
        if (!store.DeleteObjectStandard(objectCode, standardCode, RoleKey(role)))
        {
            throw new KeyNotFoundException("object-standard link not found");
        }
    }

    // === standard-parameter（M06.F03.I05/I06） ===

    public void LinkStandardParameter(StandardParameterLink body) => store.SaveStandardParameter(body);

    public void UnlinkStandardParameter(StandardParameterLink body)
    {
        if (!store.DeleteStandardParameter(body.InspectionStandardCode, body.InspectionParameterCode))
        {
            throw new KeyNotFoundException("standard-parameter link not found");
        }
    }

    // === report-name-object（M06.F07.I06 link / M06.F04.I05 unlink） ===

    public void LinkObjectReportName(ObjectReportNameLink body) => store.SaveObjectReportName(body);

    public void UnlinkObjectReportName(string objectCode, string reportNameCode)
    {
        if (!store.DeleteObjectReportName(objectCode, reportNameCode))
        {
            throw new KeyNotFoundException("object-report-name link not found");
        }
    }

    // === report-name-standard（M06.F07.I07+F04.I07 link / unlink，role 在 PK） ===

    public void LinkReportNameStandard(ReportNameStandardLink body)
    {
        // 同 LinkObjectStandard：role 枚举无 null 态，默认即 TESTING
        store.SaveReportNameStandard(body);
    }

    public void UnlinkReportNameStandard(string reportNameCode, string standardCode, InspectionStandardRole role)
    {
        if (!store.DeleteReportNameStandard(reportNameCode, standardCode, RoleKey(role)))
        {
            throw new KeyNotFoundException("report-name-standard link not found");
        }
    }

    // === report-name-parameter（M06.F07.I08+F03.I07 link / M06.F04.I06 unlink） ===

    public void LinkReportNameParameter(ReportNameParameterLink body) => store.SaveReportNameParameter(body);

    public void UnlinkReportNameParameter(string reportNameCode, string parameterCode)
    {
        if (!store.DeleteReportNameParameter(reportNameCode, parameterCode))
        {
            throw new KeyNotFoundException("report-name-parameter link not found");
        }
    }

    // === param-interface-parameter（M06.F08.I06 link / M06.F03.I07 unlink） ===

    public void LinkParamInterface(ParamInterfaceLink body) => store.SaveParamInterface(body);

    public void UnlinkParamInterface(string parameterCode, string interfaceCode)
    {
        if (!store.DeleteParamInterface(parameterCode, interfaceCode))
        {
            throw new KeyNotFoundException("param-interface link not found");
        }
    }
}
