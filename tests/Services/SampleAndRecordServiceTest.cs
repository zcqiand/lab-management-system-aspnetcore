namespace Lab.AspNetCore.Tests.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Lab.AspNetCore.Services;
using Xunit;

/// <summary>
/// M03.F03.I01-I05 样品 + I06-I11 检测记录 fnTest（B3）。
/// 语义基准：lab-springboot SampleServiceTest / TestRecordServiceTest。
/// </summary>
public class SampleAndRecordServiceTest
{
    private const string Tenant = "TENANT-001";

    private static InMemoryFlowStore StoreWithReceipt()
    {
        var store = new InMemoryFlowStore();
        store.SaveContract(new Contract
        {
            Id = "C-1", TenantId = Tenant, ContractCode = "HT-A", Status = ContractStatus.Active,
            CreatedAt = "t", UpdatedAt = "t",
        });
        store.SaveReceipt(new SampleReceipt
        {
            Id = "R-1", TenantId = Tenant, ContractId = "C-1", FlowStatus = FlowStatus.Receiving,
            CreatedAt = "t", UpdatedAt = "t",
        });
        return store;
    }

    // === 样品 M03.F03.I01-I05 ===

    [Fact]
    [Trait("Fn", "M03.F03.I01")]
    public void ListSamples_receiptAndKeywordFilters()
    {
        var store = StoreWithReceipt();
        var service = new SampleService(store);
        service.Create(Tenant, new CreateSampleRequest { ReceiptId = "R-1", SampleCode = "S-A", SampleName = "混凝土试块" });
        service.Create(Tenant, new CreateSampleRequest { ReceiptId = "R-1", SampleCode = "S-B", SampleName = "钢筋" });

        Assert.Equal(2, service.List(Tenant, "R-1", null).Count);
        Assert.Single(service.List(Tenant, "R-1", "钢筋"));
        Assert.Empty(service.List(Tenant, "R-GHOST", null));
    }

    [Fact]
    [Trait("Fn", "M03.F03.I02")]
    public void GetSample_missing404()
    {
        Assert.Throws<KeyNotFoundException>(
            () => new SampleService(StoreWithReceipt()).Get(Tenant, "GHOST"));
    }

    [Fact]
    [Trait("Fn", "M03.F03.I03")]
    public void CreateSample_missingReceipt_throws()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            new SampleService(new InMemoryFlowStore()).Create(Tenant,
                new CreateSampleRequest { ReceiptId = "R-GHOST", SampleCode = "S-A", SampleName = "x" }));
    }

    [Fact]
    [Trait("Fn", "M03.F03.I03")]
    public void CreateSample_extDefaultsToEmptyDict()
    {
        var s = new SampleService(StoreWithReceipt()).Create(Tenant,
            new CreateSampleRequest { ReceiptId = "R-1", SampleCode = "S-A", SampleName = "试块" });

        Assert.StartsWith("S-", s.Id);
        Assert.Empty(s.Ext); // jsonb 默认 {}
    }

    [Fact]
    [Trait("Fn", "M03.F03.I04")]
    public void UpdateSample_patchSemantics()
    {
        var store = StoreWithReceipt();
        var service = new SampleService(store);
        var s = service.Create(Tenant, new CreateSampleRequest { ReceiptId = "R-1", SampleCode = "S-A", SampleName = "旧名" });

        var updated = service.Update(Tenant, s.Id, new UpdateSampleRequest { SampleName = "新名" });

        Assert.Equal("新名", updated.SampleName);
        Assert.Equal("S-A", updated.SampleCode);
    }

    [Fact]
    [Trait("Fn", "M03.F03.I05")]
    public void DeleteSample_removes()
    {
        var store = StoreWithReceipt();
        var service = new SampleService(store);
        var s = service.Create(Tenant, new CreateSampleRequest { ReceiptId = "R-1", SampleCode = "S-A", SampleName = "x" });

        service.Delete(Tenant, s.Id);

        Assert.Empty(service.List(Tenant, "R-1", null));
    }

    // === 检测记录 M03.F03.I06-I11 ===

    private static CreateTestRecordRequest RecReq(string sampleId = "R-1") => new()
    {
        SampleId = sampleId, ParameterCode = "PARAM-1", Requirement = "≥30MPa", Result = "35.2",
    };

    [Fact]
    [Trait("Fn", "M03.F03.I06")]
    public void ListRecords_filtersBySampleIdOnly()
    {
        var store = StoreWithReceipt();
        var service = new TestRecordService(store);
        service.Create(Tenant, RecReq());
        service.Create(Tenant, RecReq());

        Assert.Equal(2, service.List(Tenant, "R-1").Count);
        Assert.Empty(service.List(Tenant, "R-GHOST"));
    }

    [Fact]
    [Trait("Fn", "M03.F03.I07")]
    public void GetRecord_missing404()
    {
        Assert.Throws<KeyNotFoundException>(
            () => new TestRecordService(StoreWithReceipt()).Get(Tenant, "GHOST"));
    }

    [Fact]
    [Trait("Fn", "M03.F03.I08")]
    public void CreateRecord_mapsRequiredFields()
    {
        var t = new TestRecordService(StoreWithReceipt()).Create(Tenant, RecReq());

        Assert.StartsWith("T-", t.Id);
        Assert.Equal("PARAM-1", t.ParameterCode);
        Assert.Equal("≥30MPa", t.Requirement);
        Assert.Equal("35.2", t.Result);
        Assert.Equal(Tenant, t.TenantId);
    }

    [Fact]
    [Trait("Fn", "M03.F03.I09")]
    public void UpdateRecord_patchSemantics()
    {
        var store = StoreWithReceipt();
        var service = new TestRecordService(store);
        var t = service.Create(Tenant, RecReq());

        var updated = service.Update(Tenant, t.Id, new UpdateTestRecordRequest { Result = "36.0" });

        Assert.Equal("36.0", updated.Result);
        Assert.Equal("PARAM-1", updated.ParameterCode); // 未传保留
    }

    [Fact]
    [Trait("Fn", "M03.F03.I10")]
    public void DeleteRecord_missing404()
    {
        Assert.Throws<KeyNotFoundException>(
            () => new TestRecordService(StoreWithReceipt()).Delete(Tenant, "GHOST"));
    }

    [Fact]
    [Trait("Fn", "M03.F03.I11")]
    public void SetVerdict_overwrites()
    {
        var store = StoreWithReceipt();
        var service = new TestRecordService(store);
        var t = service.Create(Tenant, RecReq());

        var updated = service.SetVerdict(Tenant, t.Id, "pass");

        Assert.Equal("pass", updated.Verdict);
    }
}
