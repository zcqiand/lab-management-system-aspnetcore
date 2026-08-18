namespace Lab.AspNetCore.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// M03.F01 接样单 + M03.F02 任务分配（B3）。
/// 语义镜像 springboot SampleReceiptService：创建 flow_status=receiving 起步、
/// flow_history=[]、contract FK 必存在；assignTask 在 receiving 态自动推进到
/// task_assignment 并写 history，其他态只更新字段。
/// </summary>
public sealed class SampleReceiptService(InMemoryFlowStore store)
{
    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public IReadOnlyList<SampleReceipt> List(string tenantId, string? contractId, FlowStatus? flowStatus, string? keyword) =>
        store.FilterReceipts(tenantId, contractId, flowStatus, keyword);

    public SampleReceipt Get(string tenantId, string id) =>
        store.FindReceipt(tenantId, id) ?? throw new KeyNotFoundException($"receipt {id} not found");

    public SampleReceipt Create(string tenantId, CreateSampleReceiptRequest body)
    {
        if (!store.ContractExists(body.ContractId))
        {
            throw new KeyNotFoundException($"contract {body.ContractId} not found"); // FK 校验
        }

        var now = Now();
        var r = new SampleReceipt
        {
            Id = "R-" + Guid.NewGuid().ToString("N")[..12],
            TenantId = tenantId,
            ContractId = body.ContractId,
            CommissionCode = body.CommissionCode ?? "",
            CommissionDate = body.CommissionDate ?? "",
            CommissionRegisterCode = body.CommissionRegisterCode ?? "",
            CommissionRegisterDate = body.CommissionRegisterDate ?? "",
            CategoryCode = body.CategoryCode ?? "",
            ProjectName = body.ProjectName ?? "",
            ClientUnit = body.ClientUnit ?? "",
            BuildingUnit = body.BuildingUnit ?? "",
            SupervisorUnit = body.SupervisorUnit ?? "",
            ConstructionUnit = body.ConstructionUnit ?? "",
            WitnessUnit = body.WitnessUnit ?? "",
            SamplingLocation = body.SamplingLocation ?? "",
            Witness = body.Witness ?? "",
            WitnessPhone = body.WitnessPhone ?? "",
            Inspector = body.Inspector ?? "",
            InspectorPhone = body.InspectorPhone ?? "",
            ReceivedBy = body.ReceivedBy ?? "",
            SampleSource = body.SampleSource ?? "",
            TestCategory = body.TestCategory ?? "",
            TestEnvironment = body.TestEnvironment ?? "",
            MainEquipment = body.MainEquipment ?? "",
            TestOperator = body.TestOperator ?? "",
            TestStartDate = body.TestStartDate ?? "",
            TestEndDate = body.TestEndDate ?? "",
            OriginalRecordNo = body.OriginalRecordNo ?? "",
            Remark = body.Remark ?? "",
            JudgmentBasis = body.JudgmentBasis?.ToList() ?? new List<string>(),
            TestingBasis = body.TestingBasis?.ToList() ?? new List<string>(),
            TestParameters = body.TestParameters?.ToList() ?? new List<string>(),
            FlowStatus = FlowStatus.Receiving, // 起步态
            FlowHistory = new List<FlowHistoryEntry>(), // "[]"
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveReceipt(r);
        return r;
    }

    public SampleReceipt Update(string tenantId, string id, UpdateSampleReceiptRequest body)
    {
        var r = Get(tenantId, id);
        if (body.CommissionCode is not null) r.CommissionCode = body.CommissionCode;
        if (body.CommissionDate is not null) r.CommissionDate = body.CommissionDate;
        if (body.CommissionRegisterCode is not null) r.CommissionRegisterCode = body.CommissionRegisterCode;
        if (body.CommissionRegisterDate is not null) r.CommissionRegisterDate = body.CommissionRegisterDate;
        if (body.CategoryCode is not null) r.CategoryCode = body.CategoryCode;
        if (body.ProjectName is not null) r.ProjectName = body.ProjectName;
        if (body.ClientUnit is not null) r.ClientUnit = body.ClientUnit;
        if (body.BuildingUnit is not null) r.BuildingUnit = body.BuildingUnit;
        if (body.SupervisorUnit is not null) r.SupervisorUnit = body.SupervisorUnit;
        if (body.ConstructionUnit is not null) r.ConstructionUnit = body.ConstructionUnit;
        if (body.WitnessUnit is not null) r.WitnessUnit = body.WitnessUnit;
        if (body.SamplingLocation is not null) r.SamplingLocation = body.SamplingLocation;
        if (body.Witness is not null) r.Witness = body.Witness;
        if (body.WitnessPhone is not null) r.WitnessPhone = body.WitnessPhone;
        if (body.Inspector is not null) r.Inspector = body.Inspector;
        if (body.InspectorPhone is not null) r.InspectorPhone = body.InspectorPhone;
        if (body.ReceivedBy is not null) r.ReceivedBy = body.ReceivedBy;
        if (body.SampleSource is not null) r.SampleSource = body.SampleSource;
        if (body.TestCategory is not null) r.TestCategory = body.TestCategory;
        if (body.TestEnvironment is not null) r.TestEnvironment = body.TestEnvironment;
        if (body.MainEquipment is not null) r.MainEquipment = body.MainEquipment;
        if (body.TestOperator is not null) r.TestOperator = body.TestOperator;
        if (body.TestStartDate is not null) r.TestStartDate = body.TestStartDate;
        if (body.TestEndDate is not null) r.TestEndDate = body.TestEndDate;
        if (body.OriginalRecordNo is not null) r.OriginalRecordNo = body.OriginalRecordNo;
        if (body.Remark is not null) r.Remark = body.Remark;
        if (body.JudgmentBasis is not null) r.JudgmentBasis = body.JudgmentBasis.ToList();
        if (body.TestingBasis is not null) r.TestingBasis = body.TestingBasis.ToList();
        if (body.TestParameters is not null) r.TestParameters = body.TestParameters.ToList();
        r.UpdatedAt = Now();
        store.SaveReceipt(r);
        return r;
    }

    public int Delete(string tenantId, string id) =>
        store.DeleteReceipt(tenantId, id, out var cascaded)
            ? cascaded
            : throw new KeyNotFoundException($"receipt {id} not found");

    public IReadOnlyList<FlowHistoryEntry> History(string tenantId, string id) => Get(tenantId, id).FlowHistory;

    // === M03.F02 任务分配 ===

    public SampleReceipt AssignTask(string tenantId, string id, AssignTaskRequest body)
    {
        var r = Get(tenantId, id);
        if (body.AssigneeId is not null) r.AssigneeId = body.AssigneeId;
        if (body.AssigneeName is not null) r.AssigneeName = body.AssigneeName;
        if (body.PlannedTestDate is not null) r.PlannedTestDate = body.PlannedTestDate;

        // 唯一副作用：receiving 态自动推进 task_assignment（镜像 springboot）
        if (r.FlowStatus == FlowStatus.Receiving)
        {
            r.FlowStatus = FlowStatus.Task_assignment;
            r.FlowHistory.Add(new FlowHistoryEntry
            {
                Action = FlowAction.Submit,
                From = FlowStatus.Receiving,
                To = FlowStatus.Task_assignment,
                Operator = body.AssigneeName ?? "",
                At = Now(),
                Reason = "M03.F02 任务分配",
            });
        }

        r.UpdatedAt = Now();
        store.SaveReceipt(r);
        return r;
    }
}
