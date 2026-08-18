namespace Lab.AspNetCore.Tests.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Lab.AspNetCore.Services;
using Xunit;

/// <summary>
/// M03.F01 接样 + M03.F02 任务分配 fnTest（B3）。
/// 语义基准：lab-springboot SampleReceiptServiceTest（receiving 起步/history/assignTask 副作用）。
/// </summary>
public class SampleReceiptServiceTest
{
    private const string Tenant = "TENANT-001";

    private static InMemoryFlowStore StoreWithContract()
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
        return store;
    }

    private static CreateSampleReceiptRequest Req(string commissionCode = "WT-001", string category = "CAT-A") => new()
    {
        ContractId = "C-1",
        CommissionCode = commissionCode,
        CommissionDate = "2026-01-10",
        CategoryCode = category,
        ProjectName = "工程一",
        ReceivedBy = "张三",
        SampleSource = "送样",
        TestCategory = "常规",
    };

    [Fact]
    [Trait("Fn", "M03.F01.I01")]
    public void List_contractAndFlowStatusFilters()
    {
        var store = StoreWithContract();
        var service = new SampleReceiptService(store);
        var r1 = service.Create(Tenant, Req());
        var r2 = service.Create(Tenant, Req("WT-002", "CAT-B"));

        Assert.Equal(2, service.List(Tenant, null, null, null).Count);
        Assert.Equal(2, service.List(Tenant, "C-1", null, null).Count); // 两张同合同
        Assert.Empty(service.List(Tenant, "C-GHOST", null, null));
        Assert.Equal(2, service.List(Tenant, null, FlowStatus.Receiving, null).Count); // 全部 receiving 起步
        // keyword 模糊 commissionCode 或 projectName；两张单 projectName 都是「工程一」→ 都命中
        Assert.Equal(2, service.List(Tenant, null, null, "工程一").Count);
        var onlyCode = service.List(Tenant, null, null, "WT-002");
        Assert.Single(onlyCode);
        Assert.Equal("CAT-B", onlyCode[0].CategoryCode);
    }

    [Fact]
    [Trait("Fn", "M03.F01.I02")]
    [Trait("Fn", "M03.F05.I02")]
    [Trait("Fn", "M03.F06.I02")]
    [Trait("Fn", "M03.F07.I02")]
    [Trait("Fn", "M03.F08.I02")]
    [Trait("Fn", "M03.F09.I01")]
    public void Get_missing_throws404()
    {
        var service = new SampleReceiptService(StoreWithContract());
        Assert.Throws<KeyNotFoundException>(() => service.Get(Tenant, "GHOST"));
    }

    [Fact]
    [Trait("Fn", "M03.F01.I03")]
    public void Create_initializesReceivingAndEmptyHistory()
    {
        var service = new SampleReceiptService(StoreWithContract());

        var r = service.Create(Tenant, Req());

        Assert.StartsWith("R-", r.Id);
        Assert.Equal(FlowStatus.Receiving, r.FlowStatus);
        Assert.Empty(r.FlowHistory); // "[]"
        Assert.Equal(Tenant, r.TenantId);
    }

    [Fact]
    [Trait("Fn", "M03.F01.I03")]
    public void Create_missingContract_throws()
    {
        var service = new SampleReceiptService(new InMemoryFlowStore());

        Assert.Throws<KeyNotFoundException>(() => service.Create(Tenant, Req()));
    }

    [Fact]
    [Trait("Fn", "M03.F01.I04")]
    public void Update_patchSemantics()
    {
        var store = StoreWithContract();
        var service = new SampleReceiptService(store);
        var r = service.Create(Tenant, Req());

        var updated = service.Update(Tenant, r.Id, new UpdateSampleReceiptRequest { ProjectName = "改名工程" });

        Assert.Equal("改名工程", updated.ProjectName);
        Assert.Equal("WT-001", updated.CommissionCode); // 未传保留
    }

    [Fact]
    [Trait("Fn", "M03.F01.I05")]
    public void Delete_cascadesSamples()
    {
        var store = StoreWithContract();
        var receipts = new SampleReceiptService(store);
        var samples = new SampleService(store);
        var r = receipts.Create(Tenant, Req());
        samples.Create(Tenant, new CreateSampleRequest { ReceiptId = r.Id, SampleCode = "S-A", SampleName = "试块" });

        var cascaded = receipts.Delete(Tenant, r.Id);

        Assert.Equal(1, cascaded); // CASCADE 下属样品
        Assert.Empty(samples.List(Tenant, r.Id, null));
    }

    [Fact]
    [Trait("Fn", "M03.F01.I06")]
    public void History_returnsFlowHistoryEntries()
    {
        var store = StoreWithContract();
        var service = new SampleReceiptService(store);
        var r = service.Create(Tenant, Req());
        service.AssignTask(Tenant, r.Id, new AssignTaskRequest { AssigneeId = "U-1", AssigneeName = "李四" });

        var history = service.History(Tenant, r.Id);

        Assert.Single(history);
        Assert.Equal(FlowAction.Submit, history[0].Action);
        Assert.Equal(FlowStatus.Receiving, history[0].From);
        Assert.Equal(FlowStatus.Task_assignment, history[0].To);
    }

    // === M03.F02 任务分配 ===

    [Fact]
    [Trait("Fn", "M03.F02.I01")]
    public void AssignTask_inReceiving_autoAdvancesToTaskAssignment()
    {
        var store = StoreWithContract();
        var service = new SampleReceiptService(store);
        var r = service.Create(Tenant, Req());

        var assigned = service.AssignTask(Tenant, r.Id, new AssignTaskRequest
        {
            AssigneeId = "U-1",
            AssigneeName = "李四",
            PlannedTestDate = "2026-01-15",
        });

        Assert.Equal("U-1", assigned.AssigneeId);
        Assert.Equal(FlowStatus.Task_assignment, assigned.FlowStatus); // 自动推进
        Assert.Single(assigned.FlowHistory); // 写 history
    }

    [Fact]
    [Trait("Fn", "M03.F02.I01")]
    public void AssignTask_alreadyAssigned_doesNotAdvanceStage()
    {
        var store = StoreWithContract();
        var service = new SampleReceiptService(store);
        var r = service.Create(Tenant, Req());
        service.AssignTask(Tenant, r.Id, new AssignTaskRequest { AssigneeId = "U-1", AssigneeName = "李四" });

        var reassigned = service.AssignTask(Tenant, r.Id, new AssignTaskRequest { AssigneeId = "U-2", AssigneeName = "王五" });

        Assert.Equal(FlowStatus.Task_assignment, reassigned.FlowStatus); // 停在原阶段
        Assert.Single(reassigned.FlowHistory); // 不再追加
        Assert.Equal("U-2", reassigned.AssigneeId);
    }
}
