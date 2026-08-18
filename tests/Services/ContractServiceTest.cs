namespace Lab.AspNetCore.Tests.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Lab.AspNetCore.Services;
using Xunit;

/// <summary>
/// M02.F01 合同 fnTest（B3）。语义基准：lab-springboot ContractServiceTest。
/// </summary>
public class ContractServiceTest
{
    private const string Tenant = "TENANT-001";

    private static Contract C(string id, string code, string project, ContractStatus status = ContractStatus.Active) => new()
    {
        Id = id, TenantId = Tenant, ContractCode = code, ProjectName = project,
        Status = status, CreatedAt = "2026-01-01", UpdatedAt = "2026-01-01",
    };

    [Fact]
    [Trait("Fn", "M02.F01.I01")]
    public void List_keywordMatchesCodeOrProjectName()
    {
        var store = new InMemoryFlowStore();
        store.SaveContract(C("C-1", "HT-2026-A", "地铁工程"));
        store.SaveContract(C("C-2", "HT-2026-B", "桥梁工程"));
        store.SaveContract(C("C-3", "XX-其他", " unrelated"));
        var service = new ContractService(store);

        var byCode = service.List(Tenant, "ht-2026", null);
        Assert.Equal(2, byCode.Count); // 不敏包含

        var byProject = service.List(Tenant, "桥梁", null);
        Assert.Single(byProject);
    }

    [Fact]
    [Trait("Fn", "M02.F01.I01")]
    public void List_statusFilter()
    {
        var store = new InMemoryFlowStore();
        store.SaveContract(C("C-1", "A", "p1"));
        store.SaveContract(C("C-2", "B", "p2", ContractStatus.Archived));
        var service = new ContractService(store);

        Assert.Single(service.List(Tenant, null, ContractStatus.Archived));
        Assert.Single(service.List(Tenant, null, ContractStatus.Active));
    }

    [Fact]
    [Trait("Fn", "M02.F01.I02")]
    public void Get_missing_throws404()
    {
        var service = new ContractService(new InMemoryFlowStore());
        Assert.Throws<KeyNotFoundException>(() => service.Get(Tenant, "GHOST"));
    }

    [Fact]
    [Trait("Fn", "M02.F01.I03")]
    public void Create_generatesIdAndDefaultsActive()
    {
        var service = new ContractService(new InMemoryFlowStore());

        var c = service.Create(Tenant, new CreateContractRequest
        {
            ContractCode = "HT-NEW", ClientUnit = "甲方", ProjectName = "新工程", ConstructionUnit = "乙方",
        });

        Assert.StartsWith("C-", c.Id);
        Assert.Equal(ContractStatus.Active, c.Status);
        Assert.Equal(Tenant, c.TenantId);
    }

    [Fact]
    [Trait("Fn", "M02.F01.I04")]
    public void Update_patchKeepsUnset()
    {
        var store = new InMemoryFlowStore();
        store.SaveContract(C("C-1", "HT-A", "旧工程"));
        var service = new ContractService(store);

        var c = service.Update(Tenant, "C-1", new UpdateContractRequest { ProjectName = "新工程" });

        Assert.Equal("新工程", c.ProjectName);
        Assert.Equal("HT-A", c.ContractCode); // 未传保留
    }

    [Fact]
    [Trait("Fn", "M02.F01.I05")]
    public void Delete_unreferenced_removes()
    {
        var store = new InMemoryFlowStore();
        store.SaveContract(C("C-1", "HT-A", "p"));
        var service = new ContractService(store);

        service.Delete(Tenant, "C-1");
        Assert.Empty(service.List(Tenant, null, null));
    }

    [Fact]
    [Trait("Fn", "M02.F01.I05")]
    public void Delete_referencedByReceipt_throwsRestrict()
    {
        var store = new InMemoryFlowStore();
        store.SaveContract(C("C-1", "HT-A", "p"));
        store.SaveReceipt(new SampleReceipt
        {
            Id = "R-1", TenantId = Tenant, ContractId = "C-1", CreatedAt = "t", UpdatedAt = "t",
        });
        var service = new ContractService(store);

        Assert.Throws<InvalidOperationException>(() => service.Delete(Tenant, "C-1")); // FK RESTRICT
    }
}
