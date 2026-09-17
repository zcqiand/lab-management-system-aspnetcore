namespace Lab.AspNetCore.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// M03.F01-F08 流程动作状态机（B3）。语义镜像 springboot ReportFlowService：
///
///   SUBMIT  : receiving→task_assignment→data_entry→review→approval→issuance→archived（archived 无 next）
///   RETURN  : task_assignment→receiving, data_entry→task_assignment, review→data_entry,
///             approval→review, issuance→approval, archived→issuance（receiving 无 prev）
///   WITHDRAW: 仅 receiving 自转移；其他态 invalid
///
/// 2026-09-17 重整（lab-shared commit 13122e9）：原 7 阶段 × {submit/return/withdraw} = 21 op +
/// 4 list*queue + 2 batch 全部收敛为 7 个 act op：
///   POST /api/receipts/{receiving|assigning|data-entry|review|approve|issuance|archived}/act
/// 共享端点 body.action={SUBMIT|RETURN|WITHDRAW} 区分动作。
/// 接受 action 范围因 stage 而异：
///   - early 3 stages {SUBMIT, RETURN, WITHDRAW}，WITHDRAW 仅 receiving 合法
///   - report 3 stages {SUBMIT, RETURN}
///   - archived {SUBMIT}（终态无 next/prev，写 history 当 audit）
///
/// 单条失败不炸整批，进 FlowActionResult{ok=false,message}。每次转移 append FlowHistoryEntry。
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

    // === 2026-09-17 重整 — 7 阶段全 act 模式 ===
    // 早期 3 阶段（M03.F01/F02/F03）允许 SUBMIT/RETURN/WITHDRAW，
    // 报告 4 阶段（M03.F05/F06/F07/F08）允许 SUBMIT/RETURN（archived 仅 SUBMIT）。

    /// <summary>M03.F01.I08/I09/I10 receiving 态 act — 允许 submit/return/withdraw。</summary>
    public ICollection<FlowActionResult> ActFlowReceiving(string tenantId, FlowActionRequest body) =>
        ActForStage(tenantId, body, FlowStatus.Receiving,
            new[] { FlowAction.Submit, FlowAction.Return, FlowAction.Withdraw });

    /// <summary>M03.F02.I05/I06/I07 task_assignment 态 act — 允许 submit/return/withdraw。
    /// WITHDRAW 在非 receiving 阶段由 ActForStage 拒（Invalid transition）。</summary>
    public ICollection<FlowActionResult> ActFlowAssigning(string tenantId, FlowActionRequest body) =>
        ActForStage(tenantId, body, FlowStatus.Task_assignment,
            new[] { FlowAction.Submit, FlowAction.Return, FlowAction.Withdraw });

    /// <summary>M03.F03.I12/I13/I14 data_entry 态 act — 允许 submit/return/withdraw。
    /// WITHDRAW 在非 receiving 阶段由 ActForStage 拒（Invalid transition）。</summary>
    public ICollection<FlowActionResult> ActFlowDataEntry(string tenantId, FlowActionRequest body) =>
        ActForStage(tenantId, body, FlowStatus.Data_entry,
            new[] { FlowAction.Submit, FlowAction.Return, FlowAction.Withdraw });

    /// <summary>M03.F05.I07/I08/I09 review 态 act — submit 推进 / return 退回。</summary>
    public ICollection<FlowActionResult> ActFlowReview(string tenantId, FlowActionRequest body) =>
        ActForStage(tenantId, body, FlowStatus.Review, new[] { FlowAction.Submit, FlowAction.Return });

    /// <summary>M03.F06.I05/I06/I07 approval 态 act — submit 推进 / return 退回。</summary>
    public ICollection<FlowActionResult> ActFlowApprove(string tenantId, FlowActionRequest body) =>
        ActForStage(tenantId, body, FlowStatus.Approval, new[] { FlowAction.Submit, FlowAction.Return });

    /// <summary>M03.F07.I05/I06/I07 issuance 态 act — submit 推进 / return 退回。</summary>
    public ICollection<FlowActionResult> ActFlowIssuance(string tenantId, FlowActionRequest body) =>
        ActForStage(tenantId, body, FlowStatus.Issuance, new[] { FlowAction.Submit, FlowAction.Return });

    /// <summary>M03.F08.I05/I06/I07 archived 态 act — 仅 submit 写 history 当 audit，return/withdraw 拒绝。
    /// 不走 TryTransition：archived 终态无 next/prev，由本方法直接 append FlowHistoryEntry。</summary>
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
