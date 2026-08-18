namespace Lab.AspNetCore.Tests.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Lab.AspNetCore.Services;
using Xunit;

/// <summary>
/// M03.F05/F06 流程状态机 fnTest（B3）。语义基准：lab-springboot ReportFlowServiceTest
/// （SUBMIT 前进 / RETURN 退回 / 容错批量 / 队列过滤）。
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
        var (store, flow) = Setup(("R-1", FlowStatus.Review), ("R-2", FlowStatus.Review), ("R-3", FlowStatus.Approval));

        var queue = flow.FlowQueue(Tenant, FlowStatus.Review, 50);
        var issuanceQueue = flow.FlowQueue(Tenant, FlowStatus.Issuance, 50);
        var archivedQueue = flow.FlowQueue(Tenant, FlowStatus.Archived, 50);

        Assert.Equal(2, queue.Count);
        Assert.All(queue, r => Assert.Equal(FlowStatus.Review, r.FlowStatus));
        Assert.Empty(issuanceQueue); // 发放队列（M03.F07.I01 同语义）
        Assert.Empty(archivedQueue); // 归档队列（M03.F08.I01 同语义）
    }

    [Fact]
    [Trait("Fn", "M03.F06.I01")]
    [Trait("Fn", "M03.F05.I03")]
    [Trait("Fn", "M03.F06.I03")]
    [Trait("Fn", "M03.F07.I03")]
    [Trait("Fn", "M03.F08.I03")]
    public void SubmitAction_advance_reviewToApproval()
    {
        var (store, flow) = Setup(("R-1", FlowStatus.Review));

        var results = flow.SubmitAction(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Submit,
            Operator = "审核员",
        });

        Assert.True(results[0].Ok);
        Assert.Equal(FlowStatus.Approval, results[0].FlowStatus);
        var receipt = store.FindReceipt(Tenant, "R-1");
        Assert.NotNull(receipt);
        Assert.Single(receipt!.FlowHistory); // 转移写 history
    }

    [Fact]
    [Trait("Fn", "M03.F06.I01")]
    public void SubmitAction_return_approvalToReview()
    {
        var (_, flow) = Setup(("R-1", FlowStatus.Approval));

        var results = flow.SubmitAction(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Return,
        });

        Assert.True(results[0].Ok);
        Assert.Equal(FlowStatus.Review, results[0].FlowStatus);
    }

    [Fact]
    [Trait("Fn", "M03.F06.I01")]
    public void SubmitAction_missing_andInvalid_failWithoutBreakingBatch()
    {
        var (_, flow) = Setup(("R-1", FlowStatus.Archived)); // archived 无 next

        var results = flow.SubmitAction(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-GHOST", "R-1" },
            Action = FlowAction.Submit,
        });

        Assert.False(results[0].Ok); // not found
        Assert.Contains("not found", results[0].Message);
        Assert.False(results[1].Ok); // archived→SUBMIT invalid
        Assert.Contains("Invalid transition", results[1].Message);
    }

    [Fact]
    [Trait("Fn", "M03.F08.I03")]
    public void FullLifecycle_receivingToArchived_viaSubmit()
    {
        var (_, flow) = Setup(("R-1", FlowStatus.Receiving));

        foreach (var expected in new[]
        {
            FlowStatus.Task_assignment, FlowStatus.Data_entry, FlowStatus.Review,
            FlowStatus.Approval, FlowStatus.Issuance, FlowStatus.Archived,
        })
        {
            var results = flow.SubmitAction(Tenant, new FlowActionRequest
            {
                Ids = new List<string> { "R-1" },
                Action = FlowAction.Submit,
            });
            Assert.True(results[0].Ok);
            Assert.Equal(expected, results[0].FlowStatus);
        }

        // archived 无 next → 第 7 次 SUBMIT 失败
        var beyond = flow.SubmitAction(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Submit,
        });
        Assert.False(beyond[0].Ok);
    }

    [Fact]
    [Trait("Fn", "M03.F07.I03")]
    public void Withdraw_onlyValidInReceiving_selfTransition()
    {
        var (_, flow) = Setup(("R-1", FlowStatus.Receiving), ("R-2", FlowStatus.Review));

        var inReceiving = flow.SubmitAction(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-1" },
            Action = FlowAction.Withdraw,
        });
        Assert.True(inReceiving[0].Ok);
        Assert.Equal(FlowStatus.Receiving, inReceiving[0].FlowStatus); // 自转移

        var inReview = flow.SubmitAction(Tenant, new FlowActionRequest
        {
            Ids = new List<string> { "R-2" },
            Action = FlowAction.Withdraw,
        });
        Assert.False(inReview[0].Ok); // 非 receiving 不允许撤回
    }
}
