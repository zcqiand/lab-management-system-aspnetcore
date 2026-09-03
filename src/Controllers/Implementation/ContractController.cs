namespace Lab.AspNetCore.Controllers.Implementation;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Security;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>M02.F01 合同 CRUD（B3，5 端点）。分页回显（page/pageSize 未参与过滤）。</summary>
[ApiController]
[Authorize]
public sealed class ContractsController(ContractService service, ITenantContext tenantContext)
    : ContractsControllerBase
{
    private readonly ContractService _service = service;
    private readonly ITenantContext _tenantContext = tenantContext;

    public override Task<Response5> ListContracts(
        [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromQuery] string? keyword, [FromQuery] ContractStatus? status)
    {
        var items = _service.List(_tenantContext.TenantId, keyword, status).ToList();
        return Task.FromResult(new Response5
        {
            Items = items,
            Page = page ?? 1,
            PageSize = pageSize ?? items.Count,
            Total = items.Count,
        });
    }

    public override Task<Contract> CreateContract([FromBody] CreateContractRequest body) =>
        Task.FromResult(_service.Create(_tenantContext.TenantId, body));

    public override Task<Contract> GetContract(string id) =>
        Task.FromResult(_service.Get(_tenantContext.TenantId, id));

    public override Task<Contract> UpdateContract(string id, [FromBody] UpdateContractRequest body) =>
        Task.FromResult(_service.Update(_tenantContext.TenantId, id, body));

    public override Task DeleteContract(string id)
    {
        _service.Delete(_tenantContext.TenantId, id);
        return Task.CompletedTask;
    }
}

/// <summary>M03.F01 接样 + M03.F02 任务分配（B3，7 端点）。</summary>
[ApiController]
[Authorize]
public sealed class ReceiptsController(SampleReceiptService service, ITenantContext tenantContext)
    : ReceiptsControllerBase
{
    private readonly SampleReceiptService _service = service;
    private readonly ITenantContext _tenantContext = tenantContext;

    public override Task<Response16> ListReceipts(
        [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? keyword,
        [FromQuery] string? contractId, [FromQuery] FlowStatus? flowStatus)
    {
        var items = _service.List(_tenantContext.TenantId, contractId, flowStatus, keyword).ToList();
        return Task.FromResult(new Response16
        {
            Items = items,
            Page = page ?? 1,
            PageSize = pageSize ?? items.Count,
            Total = items.Count,
        });
    }

    public override Task<SampleReceipt> CreateReceipt([FromBody] CreateSampleReceiptRequest body) =>
        Task.FromResult(_service.Create(_tenantContext.TenantId, body));

    public override Task<SampleReceipt> GetReceipt(string id) =>
        Task.FromResult(_service.Get(_tenantContext.TenantId, id));

    public override Task<SampleReceipt> UpdateReceipt(string id, [FromBody] UpdateSampleReceiptRequest body) =>
        Task.FromResult(_service.Update(_tenantContext.TenantId, id, body));

    public override Task DeleteReceipt(string id)
    {
        _service.Delete(_tenantContext.TenantId, id);
        return Task.CompletedTask;
    }

    // M03.F01.I06 流程历史
    public override Task<System.Collections.Generic.ICollection<FlowHistoryEntry>> GetReceiptHistory(string id) =>
        Task.FromResult<System.Collections.Generic.ICollection<FlowHistoryEntry>>(
            _service.History(_tenantContext.TenantId, id).ToList());

    // M03.F02.I01 任务分配
    public override Task<SampleReceipt> AssignTask(string id, [FromBody] AssignTaskRequest body) =>
        Task.FromResult(_service.AssignTask(_tenantContext.TenantId, id, body));
}

/// <summary>M03.F05-F08 流程队列 + 批量推进（B3，2 端点，F05-F08 共 12 个 I 级复用）。</summary>
[ApiController]
[Authorize]
public sealed class ReportFlowController(ReportFlowService service, ITenantContext tenantContext)
    : ReportFlowControllerBase
{
    private readonly ReportFlowService _service = service;
    private readonly ITenantContext _tenantContext = tenantContext;

    public override Task<System.Collections.Generic.ICollection<FlowActionResult>> SubmitFlowAction(
        [FromBody] FlowActionRequest body) =>
        Task.FromResult<System.Collections.Generic.ICollection<FlowActionResult>>(
            _service.SubmitAction(_tenantContext.TenantId, body).ToList());

    public override Task<Response17> ListFlowQueue([FromQuery] FlowStatus stage, [FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var items = _service.FlowQueue(_tenantContext.TenantId, stage, pageSize).ToList();
        return Task.FromResult(new Response17
        {
            Items = items,
            Page = page ?? 1,
            PageSize = pageSize ?? 50,
            Total = items.Count,
        });
    }
}

/// <summary>M03.F03.I01-I05 样品 CRUD（B3，5 端点）。</summary>
[ApiController]
[Authorize]
public sealed class SamplesController(SampleService service, ITenantContext tenantContext)
    : SamplesControllerBase
{
    private readonly SampleService _service = service;
    private readonly ITenantContext _tenantContext = tenantContext;

    public override Task<Response22> ListSamples(
        [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromQuery] string? receiptId, [FromQuery] string? keyword)
    {
        var items = _service.List(_tenantContext.TenantId, receiptId, keyword).ToList();
        return Task.FromResult(new Response22
        {
            Items = items,
            Page = page ?? 1,
            PageSize = pageSize ?? items.Count,
            Total = items.Count,
        });
    }

    public override Task<Sample> CreateSample([FromBody] CreateSampleRequest body) =>
        Task.FromResult(_service.Create(_tenantContext.TenantId, body));

    public override Task<Sample> GetSample(string id) =>
        Task.FromResult(_service.Get(_tenantContext.TenantId, id));

    public override Task<Sample> UpdateSample(string id, [FromBody] UpdateSampleRequest body) =>
        Task.FromResult(_service.Update(_tenantContext.TenantId, id, body));

    public override Task DeleteSample(string id)
    {
        _service.Delete(_tenantContext.TenantId, id);
        return Task.CompletedTask;
    }
}

/// <summary>M03.F03.I06-I11 检测记录 CRUD + 改判（B3，6 端点；verdict 是 PATCH）。</summary>
[ApiController]
[Authorize]
public sealed class TestRecordsController(TestRecordService service, ITenantContext tenantContext)
    : TestRecordsControllerBase
{
    private readonly TestRecordService _service = service;
    private readonly ITenantContext _tenantContext = tenantContext;

    public override Task<Response23> ListTestRecords(
        [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromQuery] string? sampleId, [FromQuery] string? parameterCode)
    {
        // parameterCode 接收未用（镜像 springboot：list 只按 tenant+sampleId 过滤）
        var items = _service.List(_tenantContext.TenantId, sampleId).ToList();
        return Task.FromResult(new Response23
        {
            Items = items,
            Page = page ?? 1,
            PageSize = pageSize ?? items.Count,
            Total = items.Count,
        });
    }

    public override Task<TestRecord> CreateTestRecord([FromBody] CreateTestRecordRequest body) =>
        Task.FromResult(_service.Create(_tenantContext.TenantId, body));

    public override Task<TestRecord> GetTestRecord(string id) =>
        Task.FromResult(_service.Get(_tenantContext.TenantId, id));

    public override Task<TestRecord> UpdateTestRecord(string id, [FromBody] UpdateTestRecordRequest body) =>
        Task.FromResult(_service.Update(_tenantContext.TenantId, id, body));

    public override Task DeleteTestRecord(string id)
    {
        _service.Delete(_tenantContext.TenantId, id);
        return Task.CompletedTask;
    }

    // M03.F03.I11 改判（生成基类是 PATCH；springboot 侧契约是 PUT —— 以生成为准）
    public override Task<TestRecord> SetVerdict(string id, [FromBody] Body8 body) =>
        Task.FromResult(_service.SetVerdict(_tenantContext.TenantId, id, body.Verdict ?? ""));
}
