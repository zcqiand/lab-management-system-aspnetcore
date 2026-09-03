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

    public override Task<Response12> ListSpecialties(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? keyword)
    {
        var items = _service.ListSpecialties(keyword).ToList();
        return Task.FromResult(Wrap12(items, page, pageSize));
    }

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

    public override Task<Response11> ListParameters(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? keyword,
        [FromQuery] InspectionParameterSourceType? sourceType)
    {
        var items = _service.ListParameters(keyword, sourceType).ToList();
        return Task.FromResult(Wrap11(items, page, pageSize));
    }

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

    public override Task<Response13> ListStandards(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? keyword,
        [FromQuery] InspectionStandardStatus? status)
    {
        var items = _service.ListStandards(keyword, status).ToList();
        return Task.FromResult(Wrap13(items, page, pageSize));
    }

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

    public override Task<Response10> ListObjects(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? inspectionSpecialtyCode,
        [FromQuery] string? keyword)
    {
        var items = _service.ListObjects(inspectionSpecialtyCode, keyword).ToList();
        return Task.FromResult(Wrap10(items, page, pageSize));
    }

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

    public override Task<Response8> ListSpecialtyObjectLinks([FromQuery] string? inspectionSpecialtyCode)
    {
        var items = _junction.ListSpecialtyObjectLinks(inspectionSpecialtyCode).ToList();
        return Task.FromResult(WrapShort(items));
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

    public override Task<Response6> ListObjectParameterLinks(
        [FromQuery] string? inspectionObjectCode,
        [FromQuery] string? inspectionParameterCode)
    {
        var items = _junction.ListObjectParameterLinks(inspectionObjectCode, inspectionParameterCode).ToList();
        return Task.FromResult(WrapShort(items));
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

    public override Task<Response7> ListObjectStandardLinks(
        [FromQuery] string? inspectionObjectCode,
        [FromQuery] InspectionStandardRole? role)
    {
        var items = _junction.ListObjectStandardLinks(inspectionObjectCode, role).ToList();
        return Task.FromResult(WrapShort(items));
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

    public override Task<Response9> ListStandardParameterLinks(
        [FromQuery] string? inspectionStandardCode,
        [FromQuery] string? inspectionParameterCode)
    {
        var items = _junction.ListStandardParameterLinks(inspectionStandardCode, inspectionParameterCode).ToList();
        return Task.FromResult(WrapShort(items));
    }

    // === Page<T> 包裹 helper（4 字段 items/page/pageSize/total）===

    private static Response12 Wrap12(IReadOnlyList<InspectionSpecialty> items, int? page, int? pageSize)
    {
        int count = items.Count;
        return new Response12
        {
            Items = items.ToList(),
            Page = page ?? 1,
            PageSize = pageSize ?? count,
            Total = count,
        };
    }

    private static Response11 Wrap11(IReadOnlyList<InspectionParameter> items, int? page, int? pageSize)
    {
        int count = items.Count;
        return new Response11
        {
            Items = items.ToList(),
            Page = page ?? 1,
            PageSize = pageSize ?? count,
            Total = count,
        };
    }

    private static Response13 Wrap13(IReadOnlyList<InspectionStandard> items, int? page, int? pageSize)
    {
        int count = items.Count;
        return new Response13
        {
            Items = items.ToList(),
            Page = page ?? 1,
            PageSize = pageSize ?? count,
            Total = count,
        };
    }

    private static Response10 Wrap10(IReadOnlyList<InspectionObject> items, int? page, int? pageSize)
    {
        int count = items.Count;
        return new Response10
        {
            Items = items.ToList(),
            Page = page ?? 1,
            PageSize = pageSize ?? count,
            Total = count,
        };
    }

    // 短 envelope（junction GET 不带分页，按 TypeSpec Page<T> 但 pageSize=items.length）
    private static Response8 WrapShort(IReadOnlyList<SpecialtyObjectLink> items)
    {
        int count = items.Count;
        return new Response8
        {
            Items = items.ToList(),
            Page = 1,
            PageSize = count,
            Total = count,
        };
    }

    private static Response6 WrapShort(IReadOnlyList<ObjectParameterLink> items)
    {
        int count = items.Count;
        return new Response6
        {
            Items = items.ToList(),
            Page = 1,
            PageSize = count,
            Total = count,
        };
    }

    private static Response7 WrapShort(IReadOnlyList<ObjectStandardLink> items)
    {
        int count = items.Count;
        return new Response7
        {
            Items = items.ToList(),
            Page = 1,
            PageSize = count,
            Total = count,
        };
    }

    private static Response9 WrapShort(IReadOnlyList<StandardParameterLink> items)
    {
        int count = items.Count;
        return new Response9
        {
            Items = items.ToList(),
            Page = 1,
            PageSize = count,
            Total = count,
        };
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

    public override Task<Response18> ListReportNames(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? keyword)
    {
        var items = _service.ListReportNames(keyword).ToList();
        int count = items.Count;
        return Task.FromResult(new Response18
        {
            Items = items,
            Page = page ?? 1,
            PageSize = pageSize ?? count,
            Total = count,
        });
    }

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

    // === junction GET（Page<T> 短 envelope，shared 契约补齐）===

    public override Task<Response19> ListObjectReportNameLinks(
        [FromQuery] string? inspectionObjectCode,
        [FromQuery] string? reportNameCode)
    {
        var items = _junction.ListObjectReportNameLinks(inspectionObjectCode, reportNameCode).ToList();
        int count = items.Count;
        return Task.FromResult(new Response19
        {
            Items = items,
            Page = 1,
            PageSize = count,
            Total = count,
        });
    }

    public override Task<Response21> ListReportNameStandardLinks(
        [FromQuery] string? reportNameCode,
        [FromQuery] InspectionStandardRole? role)
    {
        var items = _junction.ListReportNameStandardLinks(reportNameCode, role).ToList();
        int count = items.Count;
        return Task.FromResult(new Response21
        {
            Items = items,
            Page = 1,
            PageSize = count,
            Total = count,
        });
    }

    public override Task<Response20> ListReportNameParameterLinks(
        [FromQuery] string? reportNameCode,
        [FromQuery] string? inspectionParameterCode)
    {
        var items = _junction.ListReportNameParameterLinks(reportNameCode, inspectionParameterCode).ToList();
        int count = items.Count;
        return Task.FromResult(new Response20
        {
            Items = items,
            Page = 1,
            PageSize = count,
            Total = count,
        });
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

    public override Task<Response14> ListParamInterfaces(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? keyword)
    {
        var items = _service.ListInterfaces(keyword).ToList();
        int count = items.Count;
        return Task.FromResult(new Response14
        {
            Items = items,
            Page = page ?? 1,
            PageSize = pageSize ?? count,
            Total = count,
        });
    }

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

    public override Task<Response15> ListParamInterfaceLinks(
        [FromQuery] string? inspectionParameterCode,
        [FromQuery] string? paramInterfaceCode)
    {
        var items = _junction.ListParamInterfaceLinks(inspectionParameterCode, paramInterfaceCode).ToList();
        int count = items.Count;
        return Task.FromResult(new Response15
        {
            Items = items,
            Page = 1,
            PageSize = count,
            Total = count,
        });
    }
}