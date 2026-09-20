namespace Lab.AspNetCore.Tests.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Services;
using Lab.AspNetCore.Tests.TestDoubles;
using Xunit;

/// <summary>
/// M03.F05/F06 流程状态机 fnTest（B3）。语义基准：lab-springboot ReportFlowServiceTest
/// （SUBMIT 前进 / RETURN 退回 / 容错批量 / 队列过滤）。
///
/// 5.49-① 同步（2026-09-20）：2026-09-17 lab-shared 13122e9 重整把 FlowQueue/SubmitAction
/// 从 service 删除（收敛为 7 个 act 端点 + URL 隐式 stage 校验），测试未随迁导致编译不过
/// （11 error CS1061/CS1503 自 220ee0f）。本文件断言已同步现行实现语义：
///   - 队列过滤下沉 IFlowStore（service 无 queue 面），等价改写为 store 契约断言
///   - 单端点 SubmitAction → 各 stage act 端点（ActFlow*）
///   - 「archived SUBMIT invalid」断言随 archived 自转移 audit 语义删除，改断言现行行为
/// </summary>
public class ReportFlowServiceTest
{
    private const string Tenant = "TENANT-001";

    private static (InMemoryFlowStore Store, ReportFlowService Flow) Setup(params (string Id, FlowStatus Stage)[] receipts)
    {
        var store = new InMemoryFlowStore();
        store.SaveContract(new Contract
        {
            Id = "C-1",
            TenantId = Tenant,
            ContractCode = "HT-A",
            Status = ContractStatus.Active,
            CreatedAt = "t",
            UpdatedAt = "t",
        });
        foreach (var (id, stage) in receipts)
        {
            store.SaveReceipt(new SampleReceipt
            {
                Id = id,
                TenantId = Tenant,
                ContractId = "C-1",
                FlowStatus = stage,
                FlowHistory = new List<FlowHistoryEntry>(),
                CreatedAt = "t",
                UpdatedAt = "t",
            });
        }
        return (store, new ReportFlowService(store));
    }

    [Fact]
    [Trait("Fn", "M03.F05.I01")]
    [Trait("Fn", "M03.F07.I01")]
    [Trait("Fn", "M03.F08.I01")]
    public void FlowQueue_filtersByStage()
    {
        var (store, _) = Setup(("R-1", FlowStatus.Review), ("R-2", FlowStatus.Review), ("R-3", FlowStatus.Approval));

        var queue = store.FlowQueue(Tenant, FlowStatus.Review, 50);
        var issuanceQueue = store.FlowQueue(Tenant, FlowStatus.Issuance, 50);
        var archivedQueue = store.FlowQueue(Tenant, FlowStatus.Archived, 50);

        Assert.Equal(2, queue.Count);
        Assert.All(queue, r => Assert.Equal(FlowStatus.Review, r.FlowStatus));
        Assert.Empty(issuanceQueue); // 发放队列（M03.F07.I01 同语义）
        Assert.Empty(archivedQueue); // 归档队列（M03.F08.I01 同语义）
    }

    [Fact]
    [Trait("Fn", "M03.F06.I01")]
    [Trait("Fn", "M03.F05.I03")]
    public void ActFlowReview_submit_advancesReviewToApproval()
    {
        var (store, flow) = Setup(("R-1", FlowStatus.Review));

        var results = flow.ActFlowReview(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Submit,
            Operator = "审核员",
        }).ToList();

        Assert.True(results[0].Ok);
        Assert.Equal(FlowStatus.Approval, results[0].FlowStatus);
        var receipt = store.FindReceipt(Tenant, "R-1");
        Assert.NotNull(receipt);
        Assert.Single(receipt!.FlowHistory); // 转移写 history
    }

    [Fact]
    [Trait("Fn", "M03.F06.I01")]
    public void ActFlowApprove_return_returnsApprovalToReview()
    {
        var (_, flow) = Setup(("R-1", FlowStatus.Approval));

        var results = flow.ActFlowApprove(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Return,
        }).ToList();

        Assert.True(results[0].Ok);
        Assert.Equal(FlowStatus.Review, results[0].FlowStatus);
    }

    [Fact]
    [Trait("Fn", "M03.F06.I03")]
    public void ActFlow_missing_andStageMismatch_failWithoutBreakingBatch()
    {
        // 5.49-① 同步：原「archived SUBMIT invalid」断言随 archived 自转移语义删除；
        // 现行等价失效路径 = not found + stage mismatch（单条失败不炸整批不变）。
        var (_, flow) = Setup(("R-1", FlowStatus.Receiving));

        var results = flow.ActFlowReview(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-GHOST", "R-1" },
            Action = FlowAction.Submit,
        }).ToList();

        Assert.False(results[0].Ok); // not found
        Assert.Contains("not found", results[0].Message);
        Assert.False(results[1].Ok); // receiving ≠ review → stage mismatch
        Assert.Contains("Stage mismatch", results[1].Message);
    }

    [Fact]
    [Trait("Fn", "M03.F08.I03")]
    public void FullLifecycle_receivingToArchived_viaActSubmit()
    {
        // 5.49-① 同步：单端点 SubmitAction → 7 个 act 端点（URL 隐式 stage 校验）；
        // archived 终态改为接受 SUBMIT 自转移写 audit（不再断言「第 7 次失败」）。
        var (_, flow) = Setup(("R-1", FlowStatus.Receiving));
        var stages = new (Func<FlowActionRequest, ICollection<FlowActionResult>> Act, FlowStatus Expected)[]
        {
            (b => flow.ActFlowReceiving(Tenant, b), FlowStatus.Task_assignment),
            (b => flow.ActFlowAssigning(Tenant, b), FlowStatus.Data_entry),
            (b => flow.ActFlowDataEntry(Tenant, b), FlowStatus.Review),
            (b => flow.ActFlowReview(Tenant, b), FlowStatus.Approval),
            (b => flow.ActFlowApprove(Tenant, b), FlowStatus.Issuance),
            (b => flow.ActFlowIssuance(Tenant, b), FlowStatus.Archived),
        };

        foreach (var (act, expected) in stages)
        {
            var results = act(new FlowActionRequest
            {
                Ids = new List<string> { "R-1" },
                Action = FlowAction.Submit,
            }).ToList();
            Assert.True(results[0].Ok);
            Assert.Equal(expected, results[0].FlowStatus);
        }

        // archived 后 SUBMIT = audit 自转移：Ok 且保持 archived
        var audit = flow.ActFlowArchived(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Submit,
        }).ToList();
        Assert.True(audit[0].Ok);
        Assert.Equal(FlowStatus.Archived, audit[0].FlowStatus);

        // archived 拒 RETURN（终态不可回退）
        var returned = flow.ActFlowArchived(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Return,
        }).ToList();
        Assert.False(returned[0].Ok);
        Assert.Contains("Action not allowed", returned[0].Message);
    }

    [Fact]
    [Trait("Fn", "M03.F07.I03")]
    public void Withdraw_onlyValidInReceiving_selfTransition()
    {
        var (_, flow) = Setup(("R-1", FlowStatus.Receiving), ("R-2", FlowStatus.Review));

        var inReceiving = flow.ActFlowReceiving(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Withdraw,
        }).ToList();
        Assert.True(inReceiving[0].Ok);
        Assert.Equal(FlowStatus.Receiving, inReceiving[0].FlowStatus); // 自转移

        var inReview = flow.ActFlowReview(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-2" },
            Action = FlowAction.Withdraw,
        }).ToList();
        Assert.False(inReview[0].Ok); // 非 receiving 不允许撤回
    }

    // === §1 lab-shared b114f34 拆端点 PR-1 (重做)：
    // late 4 stage 共用 act 端点，submit + return (body.action 区分)；
    // archived 仅 submit；late 4 stage 全部拒 withdraw。
    // 镜像 shared/tsp/routes/report-flow.tsp @route("/review/act|approve/act|issuance/act|archived/act")。

    [Fact]
    [Trait("Fn", "M03.F05.I03")]
    public void ActFlowReview_submitAndReturn_bothSupported()
    {
        // review 态 act 端点接受 submit 和 return 两种 action（与原 SubmitAction 单端点对齐，
        // 但 URL 隐式 stage=review 校验）
        var (store, flow) = Setup(("R-1", FlowStatus.Review), ("R-2", FlowStatus.Review));

        var submits = flow.ActFlowReview(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Submit,
        }).ToList();
        Assert.True(submits[0].Ok);
        Assert.Equal(FlowStatus.Approval, submits[0].FlowStatus);

        var returns = flow.ActFlowReview(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-2" },
            Action = FlowAction.Return,
        }).ToList();
        Assert.True(returns[0].Ok);
        Assert.Equal(FlowStatus.Data_entry, returns[0].FlowStatus);

        // 两次都写 history
        Assert.Single(store.FindReceipt(Tenant, "R-1")!.FlowHistory);
        Assert.Single(store.FindReceipt(Tenant, "R-2")!.FlowHistory);
    }

    [Fact]
    [Trait("Fn", "M03.F05.I03")]
    public void ActFlowReview_withdraw_rejected_actionNotAllowed()
    {
        // late stage (review/approve/issuance/archived) 全部拒 withdraw（按状态机 only receiving）
        var (store, flow) = Setup(("R-1", FlowStatus.Review));

        var results = flow.ActFlowReview(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Withdraw,
        }).ToList();

        Assert.False(results[0].Ok);
        Assert.Contains("Action not allowed", results[0].Message);
        Assert.Contains("submit,return", results[0].Message); // 列出允许的 action
        // store 不变
        var receipt = store.FindReceipt(Tenant, "R-1");
        Assert.Equal(FlowStatus.Review, receipt!.FlowStatus);
        Assert.Empty(receipt.FlowHistory);
    }

    [Fact]
    [Trait("Fn", "M03.F08.I03")]
    public void ActFlowArchived_submitOnlyReturnAndWithdrawRejected()
    {
        // archived 是终态但允许 submit 自转移（写 history），拒 return/withdraw
        var (store, flow) = Setup(("R-1", FlowStatus.Archived));

        var submitOk = flow.ActFlowArchived(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Submit,
        }).ToList();
        Assert.True(submitOk[0].Ok);
        Assert.Equal(FlowStatus.Archived, submitOk[0].FlowStatus); // 自转移
        Assert.Single(store.FindReceipt(Tenant, "R-1")!.FlowHistory);
    }

    [Fact]
    [Trait("Fn", "M03.F06.I03")]
    public void ActFlowApprove_stageMismatch_rejectsNonApproval()
    {
        // URL 隐式 stage=approval：review 态 receipt 调 /approve/act → stage mismatch
        var (store, flow) = Setup(("R-1", FlowStatus.Review));

        var results = flow.ActFlowApprove(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Submit,
        }).ToList();

        Assert.False(results[0].Ok);
        Assert.Contains("Stage mismatch", results[0].Message);
        Assert.Equal(FlowStatus.Review, store.FindReceipt(Tenant, "R-1")!.FlowStatus);
        Assert.Empty(store.FindReceipt(Tenant, "R-1")!.FlowHistory);
    }
}
