namespace Lab.AspNetCore.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// B6 八组 junction link/unlink。语义镜像 springboot InspectionJunctionService：
/// link = upsert（同 PK 重复不报错覆盖）；unlink 幂等 204（REQ-2026-001）。
/// role 在 PK 内的两组：object-standard / report-name-standard。
/// </summary>
public sealed class JunctionService(IJunctionStore store)
{
    private static string RoleKey(InspectionStandardRole role) => role.ToString().ToUpperInvariant();

    // === specialty-object（M06.F02.I05/I06） ===

    public void LinkSpecialtyObject(SpecialtyObjectLink body) => store.SaveSpecialtyObject(body);

    public void UnlinkSpecialtyObject(SpecialtyObjectLink body)
    {
        // 幂等 204（Task 2.6 推广 REQ-2026-001 四方一致：msw/nextjs/springboot 未命中也 204，
        // 契约 unlink = void；KeyNotFound→404 是「资源不存在」语义，不适用于幂等 unlink）
        store.DeleteSpecialtyObject(body.InspectionSpecialtyCode, body.InspectionObjectCode);
    }

    public IReadOnlyList<SpecialtyObjectLink> ListSpecialtyObjectLinks(string? inspectionSpecialtyCode) =>
        store.ListSpecialtyObject(inspectionSpecialtyCode);

    // === object-parameter（M06.F02.I07/I08） ===

    public void LinkObjectParameter(ObjectParameterLink body)
    {
        body.QualificationLevel = body.QualificationLevel == default ? QualificationLevel.QUALIFIED : body.QualificationLevel;
        store.SaveObjectParameter(body);
    }

    public void UnlinkObjectParameter(string objectCode, string parameterCode)
    {
        // 幂等 204（Task 2.6 推广 REQ-2026-001 语义，同 UnlinkSpecialtyObject 注）
        store.DeleteObjectParameter(objectCode, parameterCode);
    }

    public IReadOnlyList<ObjectParameterLink> ListObjectParameterLinks(string? inspectionObjectCode, string? inspectionParameterCode) =>
        store.ListObjectParameter(inspectionObjectCode, inspectionParameterCode);

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
        // 幂等 204（Task 2.6 推广 REQ-2026-001 语义，同 UnlinkSpecialtyObject 注）
        store.DeleteObjectStandard(objectCode, standardCode, RoleKey(role));
    }

    public IReadOnlyList<ObjectStandardLink> ListObjectStandardLinks(string? inspectionObjectCode, InspectionStandardRole? role) =>
        store.ListObjectStandard(inspectionObjectCode, role);

    // === standard-parameter（M06.F03.I05/I06） ===

    public void LinkStandardParameter(StandardParameterLink body) => store.SaveStandardParameter(body);

    public void UnlinkStandardParameter(StandardParameterLink body)
    {
        // 幂等 204（Task 2.6 推广 REQ-2026-001 语义，同 UnlinkSpecialtyObject 注）
        store.DeleteStandardParameter(body.InspectionStandardCode, body.InspectionParameterCode);
    }

    public IReadOnlyList<StandardParameterLink> ListStandardParameterLinks(string? inspectionStandardCode, string? inspectionParameterCode) =>
        store.ListStandardParameter(inspectionStandardCode, inspectionParameterCode);

    // === report-name-object（M06.F07.I06 link / M06.F04.I05 unlink） ===

    public void LinkObjectReportName(ObjectReportNameLink body) => store.SaveObjectReportName(body);

    public void UnlinkObjectReportName(string objectCode, string reportNameCode)
    {
        // 幂等 204（Task 2.6 推广 REQ-2026-001 语义，同 UnlinkSpecialtyObject 注）
        store.DeleteObjectReportName(objectCode, reportNameCode);
    }

    public IReadOnlyList<ObjectReportNameLink> ListObjectReportNameLinks(string? inspectionObjectCode, string? reportNameCode) =>
        store.ListObjectReportName(inspectionObjectCode, reportNameCode);

    // === report-name-standard（M06.F07.I07+F04.I07 link / unlink，role 在 PK） ===

    public void LinkReportNameStandard(ReportNameStandardLink body)
    {
        // 同 LinkObjectStandard：role 枚举无 null 态，默认即 TESTING
        store.SaveReportNameStandard(body);
    }

    public void UnlinkReportNameStandard(string reportNameCode, string standardCode, InspectionStandardRole role)
    {
        // 幂等 204（Task 2.6 推广 REQ-2026-001 语义，同 UnlinkSpecialtyObject 注）
        store.DeleteReportNameStandard(reportNameCode, standardCode, RoleKey(role));
    }

    public IReadOnlyList<ReportNameStandardLink> ListReportNameStandardLinks(string? reportNameCode, InspectionStandardRole? role) =>
        store.ListReportNameStandard(reportNameCode, role);

    // === report-name-parameter（M06.F07.I08+F03.I07 link / M06.F04.I06 unlink） ===

    public void LinkReportNameParameter(ReportNameParameterLink body) => store.SaveReportNameParameter(body);

    public void UnlinkReportNameParameter(string reportNameCode, string parameterCode)
    {
        // 幂等 204（Task 2.6 推广 REQ-2026-001 语义，同 UnlinkSpecialtyObject 注）
        store.DeleteReportNameParameter(reportNameCode, parameterCode);
    }

    public IReadOnlyList<ReportNameParameterLink> ListReportNameParameterLinks(string? reportNameCode, string? inspectionParameterCode) =>
        store.ListReportNameParameter(reportNameCode, inspectionParameterCode);

    // === param-interface-parameter（M06.F08.I06 link / M06.F03.I07 unlink） ===

    public void LinkParamInterface(ParamInterfaceLink body) => store.SaveParamInterface(body);

    public void UnlinkParamInterface(string parameterCode, string interfaceCode)
    {
        // 幂等 204（REQ-2026-001 四方一致：msw/nextjs/springboot 未命中也 204，
        // 契约 unlink = void；KeyNotFound→404 是「资源不存在」语义，不适用于幂等 unlink）
        store.DeleteParamInterface(parameterCode, interfaceCode);
    }

    public IReadOnlyList<ParamInterfaceLink> ListParamInterfaceLinks(string? inspectionParameterCode, string? paramInterfaceCode) =>
        store.ListParamInterface(inspectionParameterCode, paramInterfaceCode);
}
