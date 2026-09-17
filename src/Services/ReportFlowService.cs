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
public sealed class ReportFlowService(IFlowStore store)
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

    // === §1 lab-shared b114f34 拆端点 — late 4 stage 共用 act 端点（submit + return，
    // body.action 区分）。Withdraw 在 late stage 拒绝。语义镜像 shared/tsp/routes/
    // report-flow.tsp @route("/review/act|approve/act|issuance/act|archived/act")。
    // 接受 action 范围因 stage 而异：review/approve/issuance {submit, return}，
    // archived 仅 {submit}（archived 自转移写 history，对应原 SubmitAction action=Submit 行为）。

    /// <summary>M03.F05.I03 review 态共用 act — submit 推进 / return 退回。</summary>
    public ICollection<FlowActionResult> ActFlowReview(string tenantId, FlowActionRequest body) =>
        ActForStage(tenantId, body, FlowStatus.Review, new[] { FlowAction.Submit, FlowAction.Return });

    /// <summary>M03.F06.I03 approval 态共用 act — submit 推进 / return 退回。</summary>
    public ICollection<FlowActionResult> ActFlowApprove(string tenantId, FlowActionRequest body) =>
        ActForStage(tenantId, body, FlowStatus.Approval, new[] { FlowAction.Submit, FlowAction.Return });

    /// <summary>M03.F07.I03 issuance 态共用 act — submit 推进 / return 退回。</summary>
    public ICollection<FlowActionResult> ActFlowIssuance(string tenantId, FlowActionRequest body) =>
        ActForStage(tenantId, body, FlowStatus.Issuance, new[] { FlowAction.Submit, FlowAction.Return });

    /// <summary>M03.F08.I03 archived 态共用 act — submit 写 history 当 audit，return/withdraw 拒绝。
    /// 不走 TryTransition：archived 终态无 next/prev，由本方法直接 append FlowHistoryEntry。
    /// 业务解读：shared .tsp 把 /archived/act 列为 "submit/return 共用端点"，submit 当
    /// "归档后补操作" audit，return 因 archived 无 prev 拒。</summary>
    public ICollection<FlowActionResult> ActFlowArchived(string tenantId, FlowActionRequest body)
    {
        var results = new List<FlowActionResult>();
        foreach (var id in body.Ids)
        {
            var r = store.FindReceipt(tenantId, id);
            if (r is null)
            {
                results.Add(new FlowActionResult { Id = id, Ok = false, Message = "Receipt not found" });
                continue;
            }
            if (r.FlowStatus != FlowStatus.Archived)
            {
                results.Add(new FlowActionResult
                {
                    Id = id,
                    Ok = false,
                    Message = $"Stage mismatch: endpoint requires archived but receipt is {Snake(r.FlowStatus)}",
                });
                continue;
            }
            if (body.Action != FlowAction.Submit)
            {
                results.Add(new FlowActionResult
                {
                    Id = id,
                    Ok = false,
                    Message = $"Action not allowed: archived stage accepts only submit but got {Snake(body.Action)}",
                });
                continue;
            }
            // 写 history 当 audit，状态保持 archived
            r.FlowHistory.Add(new FlowHistoryEntry
            {
                Action = FlowAction.Submit,
                From = FlowStatus.Archived,
                To = FlowStatus.Archived,
                Operator = body.Operator ?? "",
                At = Now(),
                Reason = body.Reason ?? "archived: post-archive audit",
            });
            r.UpdatedAt = Now();
            store.SaveReceipt(r);
            results.Add(new FlowActionResult { Id = id, Ok = true, FlowStatus = FlowStatus.Archived });
        }
        return results;
    }

    private ICollection<FlowActionResult> ActForStage(
        string tenantId, FlowActionRequest body, FlowStatus requiredStage, IReadOnlyCollection<FlowAction> allowed)
    {
        var results = new List<FlowActionResult>();
        foreach (var id in body.Ids)
        {
            var r = store.FindReceipt(tenantId, id);
            if (r is null)
            {
                results.Add(new FlowActionResult { Id = id, Ok = false, Message = "Receipt not found" });
                continue;
            }
            if (r.FlowStatus != requiredStage)
            {
                results.Add(new FlowActionResult
                {
                    Id = id,
                    Ok = false,
                    Message = $"Stage mismatch: endpoint requires {Snake(requiredStage)} but receipt is {Snake(r.FlowStatus)}",
                });
                continue;
            }
            if (!allowed.Contains(body.Action))
            {
                results.Add(new FlowActionResult
                {
                    Id = id,
                    Ok = false,
                    Message = $"Action not allowed: {Snake(requiredStage)} stage accepts only [{string.Join(",", allowed.Select(Snake))}] but got {Snake(body.Action)}",
                });
                continue;
            }
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
