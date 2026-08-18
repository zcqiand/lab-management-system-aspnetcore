namespace Lab.AspNetCore.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// M03.F05-F08 报告流程状态机（B3）。语义镜像 springboot ReportFlowService：
///
///   SUBMIT  : receiving→task_assignment→data_entry→review→approval→issuance→archived（archived 无 next）
///   RETURN  : task_assignment→receiving, data_entry→task_assignment, review→data_entry,
///             approval→review, issuance→approval, archived→issuance（receiving 无 prev）
///   WITHDRAW: 仅 receiving 自转移；其他态 invalid
///
/// POST /api/receipts/flow 批量：单条失败不炸整批，进 FlowActionResult{ok=false,message}。
/// 每次转移 append FlowHistoryEntry 到 flow_history。
/// </summary>
public sealed class ReportFlowService(InMemoryFlowStore store)
{
    private static readonly IReadOnlyDictionary<FlowStatus, FlowStatus> Next = new Dictionary<FlowStatus, FlowStatus>
    {
        [FlowStatus.Receiving] = FlowStatus.Task_assignment,
        [FlowStatus.Task_assignment] = FlowStatus.Data_entry,
        [FlowStatus.Data_entry] = FlowStatus.Review,
        [FlowStatus.Review] = FlowStatus.Approval,
        [FlowStatus.Approval] = FlowStatus.Issuance,
        [FlowStatus.Issuance] = FlowStatus.Archived,
    };

    private static readonly IReadOnlyDictionary<FlowStatus, FlowStatus> Prev = new Dictionary<FlowStatus, FlowStatus>
    {
        [FlowStatus.Task_assignment] = FlowStatus.Receiving,
        [FlowStatus.Data_entry] = FlowStatus.Task_assignment,
        [FlowStatus.Review] = FlowStatus.Data_entry,
        [FlowStatus.Approval] = FlowStatus.Review,
        [FlowStatus.Issuance] = FlowStatus.Approval,
        [FlowStatus.Archived] = FlowStatus.Issuance,
    };

    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    /// <summary>M03.F05.I01 队列：stage 精确 + tenant 收口，pageSize 默认 50 cap 200。</summary>
    public IReadOnlyList<SampleReceipt> FlowQueue(string tenantId, FlowStatus stage, int? pageSize) =>
        store.FlowQueue(tenantId, stage, pageSize ?? 50);

    /// <summary>M03.F05.I03/F06.I01-I03/F07.I03/F08.I03 批量推进。单条失败容错。</summary>
    public IReadOnlyList<FlowActionResult> SubmitAction(string tenantId, FlowActionRequest body)
    {
        var results = new List<FlowActionResult>();
        foreach (var id in body.Ids)
        {
            results.Add(TryTransition(tenantId, id, body.Action, body.Operator ?? "", body.Reason ?? ""));
        }
        return results;
    }

    private FlowActionResult TryTransition(string tenantId, string id, FlowAction action, string op, string reason)
    {
        var r = store.FindReceipt(tenantId, id);
        if (r is null)
        {
            return new FlowActionResult { Id = id, Ok = false, Message = "Receipt not found" };
        }

        FlowStatus to;
        switch (action)
        {
            case FlowAction.Submit when Next.TryGetValue(r.FlowStatus, out var next):
                to = next;
                break;
            case FlowAction.Return when Prev.TryGetValue(r.FlowStatus, out var prev):
                to = prev;
                break;
            case FlowAction.Withdraw when r.FlowStatus == FlowStatus.Receiving:
                to = FlowStatus.Receiving; // 自转移（no-op 效果但写 history）
                break;
            default:
                return new FlowActionResult
                {
                    Id = id,
                    Ok = false,
                    Message = $"Invalid transition: {Snake(r.FlowStatus)} does not accept {Snake(action)}",
                };
        }

        var from = r.FlowStatus;
        r.FlowStatus = to;
        r.FlowHistory.Add(new FlowHistoryEntry
        {
            Action = action,
            From = from,
            To = to,
            Operator = op,
            At = Now(),
            Reason = reason,
        });
        r.UpdatedAt = Now();
        store.SaveReceipt(r);
        return new FlowActionResult { Id = id, Ok = true, FlowStatus = to };
    }

    private static string Snake<T>(T e) where T : struct, Enum
    {
        var name = e.ToString();
        return name.Length == 0 ? "" : char.ToLowerInvariant(name[0]) + name[1..];
    }
}
