namespace Lab.AspNetCore.Controllers.Implementation;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// B5/B6：检测能力字典（专项/参数/标准/objects CRUD）+ 4 组 inspection junction。
/// 全平台级无 tenant（镜像 springboot）。
/// </summary>
[ApiController]
[Authorize]
public sealed class InspectionDictionaryController(DictionaryService service, JunctionService junction)
    : InspectionDictionaryControllerBase
{
    private readonly DictionaryService _service = service;
    private readonly JunctionService _junction = junction;

    // === M06.F01 专项 ===

    public override Task<System.Collections.Generic.ICollection<InspectionSpecialty>> ListSpecialties(
        [FromQuery] string keyword) =>
        Task.FromResult<System.Collections.Generic.ICollection<InspectionSpecialty>>(
            _service.ListSpecialties(keyword).ToList());

    public override Task<InspectionSpecialty> CreateSpecialty([FromBody] CreateInspectionSpecialtyRequest body) =>
        Task.FromResult(_service.CreateSpecialty(body));

    public override Task<InspectionSpecialty> UpdateSpecialty(string code, [FromBody] UpdateInspectionSpecialtyRequest body) =>
        Task.FromResult(_service.UpdateSpecialty(code, body));

    public override Task DeleteSpecialty(string code)
    {
        _service.DeleteSpecialty(code);
        return Task.CompletedTask;
    }

    // === M06.F03 参数 ===

    public override Task<System.Collections.Generic.ICollection<InspectionParameter>> ListParameters(
        [FromQuery] string keyword, [FromQuery] InspectionParameterSourceType? sourceType) =>
        Task.FromResult<System.Collections.Generic.ICollection<InspectionParameter>>(
            _service.ListParameters(keyword, sourceType).ToList());

    public override Task<InspectionParameter> CreateParameter([FromBody] CreateInspectionParameterRequest body) =>
        Task.FromResult(_service.CreateParameter(body));

    public override Task<InspectionParameter> UpdateParameter(string code, [FromBody] UpdateInspectionParameterRequest body) =>
        Task.FromResult(_service.UpdateParameter(code, body));

    public override Task DeleteParameter(string code)
    {
        _service.DeleteParameter(code);
        return Task.CompletedTask;
    }

    // === M06.F04 标准 ===

    public override Task<System.Collections.Generic.ICollection<InspectionStandard>> ListStandards(
        [FromQuery] string keyword, [FromQuery] InspectionStandardStatus? status) =>
        Task.FromResult<System.Collections.Generic.ICollection<InspectionStandard>>(
            _service.ListStandards(keyword, status).ToList());

    public override Task<InspectionStandard> CreateStandard([FromBody] CreateInspectionStandardRequest body) =>
        Task.FromResult(_service.CreateStandard(body));

    public override Task<InspectionStandard> UpdateStandard(string code, [FromBody] UpdateInspectionStandardRequest body) =>
        Task.FromResult(_service.UpdateStandard(code, body));

    public override Task DeleteStandard(string code)
    {
        _service.DeleteStandard(code);
        return Task.CompletedTask;
    }

    // === M06.F02 objects ===

    public override Task<System.Collections.Generic.ICollection<InspectionObject>> ListObjects(
        [FromQuery] string inspectionSpecialtyCode, [FromQuery] string keyword) =>
        Task.FromResult<System.Collections.Generic.ICollection<InspectionObject>>(
            _service.ListObjects(inspectionSpecialtyCode, keyword).ToList());

    public override Task<InspectionObject> CreateObject([FromBody] CreateInspectionObjectRequest body) =>
        Task.FromResult(_service.CreateObject(body));

    public override Task<InspectionObject> UpdateObject(string code, [FromBody] UpdateInspectionObjectRequest body) =>
        Task.FromResult(_service.UpdateObject(code, body));

    public override Task DeleteObject(string code)
    {
        _service.DeleteObject(code);
        return Task.CompletedTask;
    }

    // === junction：specialty-object / object-parameter / object-standard / standard-parameter ===

    public override Task LinkSpecialtyObject([FromBody] SpecialtyObjectLink body)
    {
        _junction.LinkSpecialtyObject(body);
        return Task.CompletedTask;
    }

    public override Task UnlinkSpecialtyObject([FromBody] SpecialtyObjectLink body)
    {
        _junction.UnlinkSpecialtyObject(body);
        return Task.CompletedTask;
    }

    public override Task LinkObjectParameter([FromBody] ObjectParameterLink body)
    {
        _junction.LinkObjectParameter(body);
        return Task.CompletedTask;
    }

    public override Task UnlinkObjectParameter([FromBody] Body2 body)
    {
        _junction.UnlinkObjectParameter(body.InspectionObjectCode, body.InspectionParameterCode);
        return Task.CompletedTask;
    }

    public override Task LinkObjectStandard([FromBody] ObjectStandardLink body)
    {
        _junction.LinkObjectStandard(body);
        return Task.CompletedTask;
    }

    public override Task UnlinkObjectStandard([FromBody] Body3 body)
    {
        _junction.UnlinkObjectStandard(body.InspectionObjectCode, body.InspectionStandardCode, body.Role);
        return Task.CompletedTask;
    }

    public override Task LinkStandardParameter([FromBody] StandardParameterLink body)
    {
        _junction.LinkStandardParameter(body);
        return Task.CompletedTask;
    }

    public override Task UnlinkStandardParameter([FromBody] StandardParameterLink body)
    {
        _junction.UnlinkStandardParameter(body);
        return Task.CompletedTask;
    }
}

/// <summary>M06.F07 报告名称 CRUD + 3 组 report-name junction（B5/B6）。</summary>
[ApiController]
[Authorize]
public sealed class ReportNamesController(DictionaryService service, JunctionService junction)
    : ReportNamesControllerBase
{
    private readonly DictionaryService _service = service;
    private readonly JunctionService _junction = junction;

    public override Task<System.Collections.Generic.ICollection<InspectionReportName>> ListReportNames(
        [FromQuery] string keyword) =>
        Task.FromResult<System.Collections.Generic.ICollection<InspectionReportName>>(
            _service.ListReportNames(keyword).ToList());

    public override Task<InspectionReportName> CreateReportName([FromBody] CreateInspectionReportNameRequest body) =>
        Task.FromResult(_service.CreateReportName(body));

    public override Task<InspectionReportName> GetReportName(string code) =>
        Task.FromResult(_service.GetReportName(code));

    public override Task<InspectionReportName> UpdateReportName(string code, [FromBody] UpdateInspectionReportNameRequest body) =>
        Task.FromResult(_service.UpdateReportName(code, body));

    public override Task DeleteReportName(string code)
    {
        _service.DeleteReportName(code);
        return Task.CompletedTask;
    }

    public override Task LinkObjectReportName([FromBody] ObjectReportNameLink body)
    {
        _junction.LinkObjectReportName(body);
        return Task.CompletedTask;
    }

    public override Task UnlinkObjectReportName([FromBody] Body5 body)
    {
        _junction.UnlinkObjectReportName(body.InspectionObjectCode, body.ReportNameCode);
        return Task.CompletedTask;
    }

    public override Task LinkReportNameParameter([FromBody] ReportNameParameterLink body)
    {
        _junction.LinkReportNameParameter(body);
        return Task.CompletedTask;
    }

    public override Task UnlinkReportNameParameter([FromBody] Body6 body)
    {
        _junction.UnlinkReportNameParameter(body.ReportNameCode, body.InspectionParameterCode);
        return Task.CompletedTask;
    }

    public override Task LinkReportNameStandard([FromBody] ReportNameStandardLink body)
    {
        _junction.LinkReportNameStandard(body);
        return Task.CompletedTask;
    }

    public override Task UnlinkReportNameStandard([FromBody] Body7 body)
    {
        _junction.UnlinkReportNameStandard(body.ReportNameCode, body.InspectionStandardCode, body.Role);
        return Task.CompletedTask;
    }
}

/// <summary>M06.F08 参数界面 CRUD + param-interface junction（B5/B6）。</summary>
[ApiController]
[Authorize]
public sealed class ParamInterfacesController(DictionaryService service, JunctionService junction)
    : ParamInterfacesControllerBase
{
    private readonly DictionaryService _service = service;
    private readonly JunctionService _junction = junction;

    public override Task<System.Collections.Generic.ICollection<ParamInterface>> ListParamInterfaces(
        [FromQuery] string keyword) =>
        Task.FromResult<System.Collections.Generic.ICollection<ParamInterface>>(
            _service.ListInterfaces(keyword).ToList());

    public override Task<ParamInterface> CreateParamInterface([FromBody] CreateParamInterfaceRequest body) =>
        Task.FromResult(_service.CreateInterface(body));

    public override Task<ParamInterface> GetParamInterface(string code) =>
        Task.FromResult(_service.GetInterface(code));

    public override Task<ParamInterface> UpdateParamInterface(string code, [FromBody] UpdateParamInterfaceRequest body) =>
        Task.FromResult(_service.UpdateInterface(code, body));

    public override Task DeleteParamInterface(string code)
    {
        _service.DeleteInterface(code);
        return Task.CompletedTask;
    }

    public override Task LinkParamInterface([FromBody] ParamInterfaceLink body)
    {
        _junction.LinkParamInterface(body);
        return Task.CompletedTask;
    }

    public override Task UnlinkParamInterface([FromBody] Body4 body)
    {
        _junction.UnlinkParamInterface(body.InspectionParameterCode, body.ParamInterfaceCode);
        return Task.CompletedTask;
    }
}
