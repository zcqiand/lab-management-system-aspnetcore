namespace Lab.AspNetCore.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// M02.F01 合同 CRUD（B3）。语义镜像 springboot ContractService：
/// keyword 模糊 contractCode/projectName（不敏）、status 精确、tenant 收口；
/// id = "C-"+UUID；创建默认 ACTIVE；删除被接样引用时 RESTRICT 拒。
/// </summary>
public sealed class ContractService(InMemoryFlowStore store)
{
    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public IReadOnlyList<Contract> List(string tenantId, string? keyword, ContractStatus? status) =>
        store.FilterContracts(tenantId, keyword, status);

    public Contract Get(string tenantId, string id) =>
        store.FindContract(tenantId, id) ?? throw new KeyNotFoundException($"contract {id} not found");

    public Contract Create(string tenantId, CreateContractRequest body)
    {
        var now = Now();
        var c = new Contract
        {
            Id = "C-" + Guid.NewGuid().ToString("N")[..12],
            TenantId = tenantId,
            ContractCode = body.ContractCode,
            ClientUnit = body.ClientUnit,
            ProjectName = body.ProjectName,
            ProjectLocation = body.ProjectLocation ?? "",
            ConstructionUnit = body.ConstructionUnit,
            InspectionSpecialtyCode = body.InspectionSpecialtyCode ?? "",
            BuildingUnit = body.BuildingUnit ?? "",
            SupervisorUnit = body.SupervisorUnit ?? "",
            InspectionPerson = body.InspectionPerson ?? "",
            InspectionPhone = body.InspectionPhone ?? "",
            WitnessUnit = body.WitnessUnit ?? "",
            Witness = body.Witness ?? "",
            WitnessPhone = body.WitnessPhone ?? "",
            ContactPerson = body.ContactPerson ?? "",
            ContactPhone = body.ContactPhone ?? "",
            EntrustedDate = body.EntrustedDate ?? "",
            Status = body.Status == default ? ContractStatus.Active : body.Status,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveContract(c);
        return c;
    }

    public Contract Update(string tenantId, string id, UpdateContractRequest body)
    {
        var c = Get(tenantId, id);
        if (body.ContractCode is not null) c.ContractCode = body.ContractCode;
        if (body.ClientUnit is not null) c.ClientUnit = body.ClientUnit;
        if (body.ProjectName is not null) c.ProjectName = body.ProjectName;
        if (body.ProjectLocation is not null) c.ProjectLocation = body.ProjectLocation;
        if (body.ConstructionUnit is not null) c.ConstructionUnit = body.ConstructionUnit;
        if (body.InspectionSpecialtyCode is not null) c.InspectionSpecialtyCode = body.InspectionSpecialtyCode;
        if (body.BuildingUnit is not null) c.BuildingUnit = body.BuildingUnit;
        if (body.SupervisorUnit is not null) c.SupervisorUnit = body.SupervisorUnit;
        if (body.InspectionPerson is not null) c.InspectionPerson = body.InspectionPerson;
        if (body.InspectionPhone is not null) c.InspectionPhone = body.InspectionPhone;
        if (body.WitnessUnit is not null) c.WitnessUnit = body.WitnessUnit;
        if (body.Witness is not null) c.Witness = body.Witness;
        if (body.WitnessPhone is not null) c.WitnessPhone = body.WitnessPhone;
        if (body.ContactPerson is not null) c.ContactPerson = body.ContactPerson;
        if (body.ContactPhone is not null) c.ContactPhone = body.ContactPhone;
        if (body.EntrustedDate is not null) c.EntrustedDate = body.EntrustedDate;
        if (body.Status != default) c.Status = body.Status;
        c.UpdatedAt = Now();
        store.SaveContract(c);
        return c;
    }

    public void Delete(string tenantId, string id)
    {
        Get(tenantId, id);
        if (store.ContractReferenced(id))
        {
            throw new InvalidOperationException($"contract {id} is referenced by receipts"); // FK RESTRICT
        }

        store.DeleteContract(tenantId, id);
    }
}
