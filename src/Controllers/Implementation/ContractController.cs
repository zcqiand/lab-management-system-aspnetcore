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
            PageSize = pageSize ?? 20,
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
            PageSize = pageSize ?? 20,
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

    // === §1 lab-shared b114f34 拆端点 = 不豁免替代：原 SubmitFlowAction / ListFlowQueue
    // 共享端点已删除，替换为 19 个独立 flow action / queue 端点（见下方 NotImplementedException
    // 占位）。原 .NET service.SubmitAction / .FlowQueue 仍保留（其他 impl 可能引用），后续
    // 端点实现后即可下线。 ===

    // === §1 lab-shared b114f34 新拆端点 1:1 命中 — 待实现占位（NotImplementedException）===
    // 见 docs/conventions/codegen-impl-drift.md §5.1 + gen-shared.sh 注释。
    // 修复纪律：保持签名与 abstract 一致，业务逻辑后续 PR 逐个补 + 同 commit 加 test。
    public override Task<ICollection<FlowActionResult>> ActFlowApprove([FromBody] FlowActionRequest body) =>
        Task.FromResult<ICollection<FlowActionResult>>(_service.ActFlowApprove(_tenantContext.TenantId, body));
    public override Task<ICollection<FlowActionResult>> ActFlowArchived([FromBody] FlowActionRequest body) =>
        Task.FromResult<ICollection<FlowActionResult>>(_service.ActFlowArchived(_tenantContext.TenantId, body));
    public override Task<ICollection<FlowActionResult>> ActFlowIssuance([FromBody] FlowActionRequest body) =>
        Task.FromResult<ICollection<FlowActionResult>>(_service.ActFlowIssuance(_tenantContext.TenantId, body));
    public override Task<ICollection<FlowActionResult>> ActFlowReview([FromBody] FlowActionRequest body) =>
        Task.FromResult<ICollection<FlowActionResult>>(_service.ActFlowReview(_tenantContext.TenantId, body));
    public override Task<ICollection<FlowActionResult>> WithdrawFlowAssigning([FromBody] FlowActionRequest body) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.WithdrawFlowAssigning 实现");
    public override Task<ICollection<FlowActionResult>> WithdrawFlowDataEntry([FromBody] FlowActionRequest body) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.WithdrawFlowDataEntry 实现");
    public override Task<ICollection<FlowActionResult>> WithdrawFlowReceiving([FromBody] FlowActionRequest body) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.WithdrawFlowReceiving 实现");
    public override Task<ICollection<FlowActionResult>> ReturnFlowAssigning([FromBody] FlowActionRequest body) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.ReturnFlowAssigning 实现");
    public override Task<ICollection<FlowActionResult>> ReturnFlowDataEntry([FromBody] FlowActionRequest body) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.ReturnFlowDataEntry 实现");
    public override Task<ICollection<FlowActionResult>> ReturnFlowReceiving([FromBody] FlowActionRequest body) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.ReturnFlowReceiving 实现");
    public override Task<ICollection<FlowActionResult>> SubmitFlowAssigningSubmit([FromBody] FlowActionRequest body) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.SubmitFlowAssigningSubmit 实现");
    public override Task<ICollection<FlowActionResult>> SubmitFlowDataEntrySubmit([FromBody] FlowActionRequest body) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.SubmitFlowDataEntrySubmit 实现");
    public override Task<ICollection<FlowActionResult>> SubmitFlowReceivingSubmit([FromBody] FlowActionRequest body) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.SubmitFlowReceivingSubmit 实现");
    public override Task<ICollection<FlowActionResult>> BatchReturnFlowReview([FromBody] FlowActionRequest body) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.BatchReturnFlowReview 实现");
    public override Task<ICollection<FlowActionResult>> BatchSubmitFlowReview([FromBody] FlowActionRequest body) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.BatchSubmitFlowReview 实现");
    public override Task<Response20> ListReviewQueue([FromQuery] int? page, [FromQuery] int? pageSize) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.ListReviewQueue 实现");
    public override Task<Response17> ListApproveQueue([FromQuery] int? page, [FromQuery] int? pageSize) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.ListApproveQueue 实现");
    public override Task<Response19> ListIssuanceQueue([FromQuery] int? page, [FromQuery] int? pageSize) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.ListIssuanceQueue 实现");
    public override Task<Response18> ListArchivedQueue([FromQuery] int? page, [FromQuery] int? pageSize) =>
        throw new NotImplementedException("§1 b114f34: 待 ReportFlowService.ListArchivedQueue 实现");
}

/// <summary>M03.F03.I01-I05 样品 CRUD（B3，5 端点）。</summary>
[ApiController]
[Authorize]
public sealed class SamplesController(SampleService service, ITenantContext tenantContext)
    : SamplesControllerBase
{
    private readonly SampleService _service = service;
    private readonly ITenantContext _tenantContext = tenantContext;

    public override Task<Response25> ListSamples(
        [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromQuery] string? receiptId, [FromQuery] string? keyword)
    {
        var items = _service.List(_tenantContext.TenantId, receiptId, keyword).ToList();
        return Task.FromResult(new Response25
        {
            Items = items,
            Page = page ?? 1,
            PageSize = pageSize ?? 20,
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

    // === §1 lab-shared b114f34 新拆端点 — 待实现占位 ===
    public override Task<Sample> UpdateSampleExt(string id, [FromBody] UpdateSampleExtRequest body) =>
        throw new NotImplementedException("§1 b114f34: 待 SampleService.UpdateSampleExt 实现");
}

/// <summary>M03.F03.I06-I11 检测记录 CRUD + 改判（B3，6 端点；verdict 是 PATCH）。</summary>
[ApiController]
[Authorize]
public sealed class TestRecordsController(TestRecordService service, ITenantContext tenantContext)
    : TestRecordsControllerBase
{
    private readonly TestRecordService _service = service;
    private readonly ITenantContext _tenantContext = tenantContext;

    public override Task<Response26> ListTestRecords(
        [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromQuery] string? sampleId, [FromQuery] string? parameterCode)
    {
        // parameterCode 接收未用（镜像 springboot：list 只按 tenant+sampleId 过滤）
        var items = _service.List(_tenantContext.TenantId, sampleId).ToList();
        return Task.FromResult(new Response26
        {
            Items = items,
            Page = page ?? 1,
            PageSize = pageSize ?? 20,
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
