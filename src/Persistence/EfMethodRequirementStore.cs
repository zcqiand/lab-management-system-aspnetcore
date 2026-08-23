namespace Lab.AspNetCore.Persistence;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Microsoft.EntityFrameworkCore;

public sealed class EfMethodStore(LabDbContext db) : IMethodStore
{
    public IReadOnlyList<CalculationMethod> Filter(string? objectCode, string? parameterCode) =>
        db.CalculationMethods
            .Where(r => objectCode == null || objectCode == "" || r.InspectionObjectCode == objectCode)
            .Where(r => parameterCode == null || parameterCode == "" || r.InspectionParameterCode == parameterCode)
            .OrderBy(r => r.SortOrder)
            .ToList();

    public CalculationMethod? Find(string objectCode, string parameterCode) =>
        db.CalculationMethods.Find(objectCode, parameterCode);

    public void Save(CalculationMethod r) =>
        EfStoreOps.Upsert(db, db.CalculationMethods, r,
            x => x.InspectionObjectCode == r.InspectionObjectCode
                && x.InspectionParameterCode == r.InspectionParameterCode);

    public bool Delete(string objectCode, string parameterCode)
    {
        var existing = db.CalculationMethods.Find(objectCode, parameterCode);
        if (existing is null)
        {
            return false;
        }

        db.CalculationMethods.Remove(existing);
        db.SaveChanges();
        return true;
    }
}

public sealed class EfRequirementStore(LabDbContext db) : IRequirementStore
{
    public IReadOnlyList<TechnicalRequirement> Filter(
        string tenantId, string? objectCode, string? parameterCode, string? standardCode,
        RequirementVerificationStatus? status) =>
        db.TechnicalRequirements
            .Where(t => t.TenantId == tenantId)
            .Where(t => objectCode == null || objectCode == "" || t.InspectionObjectCode == objectCode)
            .Where(t => parameterCode == null || parameterCode == "" || t.InspectionParameterCode == parameterCode)
            .Where(t => standardCode == null || standardCode == "" || t.JudgmentStandardCode == standardCode)
            .Where(t => status == null || t.VerificationStatus == status)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.InspectionObjectCode)
            .ThenBy(t => t.InspectionParameterCode)
            .ThenBy(t => t.JudgmentStandardCode)
            .ToList();

    // SQL PK = 业务三键（tenant_id 不在 PK，镜像 SSOT；同三键跨租户在 DB 侧冲突）
    public TechnicalRequirement? Find(string tenantId, string objectCode, string parameterCode, string standardCode) =>
        db.TechnicalRequirements.Find(objectCode, parameterCode, standardCode) is { } t && t.TenantId == tenantId
            ? t
            : null;

    public void Save(TechnicalRequirement t) =>
        EfStoreOps.Upsert(db, db.TechnicalRequirements, t,
            x => x.InspectionObjectCode == t.InspectionObjectCode
                && x.InspectionParameterCode == t.InspectionParameterCode
                && x.JudgmentStandardCode == t.JudgmentStandardCode);

    public bool Delete(string tenantId, string objectCode, string parameterCode, string standardCode)
    {
        var existing = db.TechnicalRequirements.Find(objectCode, parameterCode, standardCode);
        if (existing is null || existing.TenantId != tenantId)
        {
            return false;
        }

        db.TechnicalRequirements.Remove(existing);
        db.SaveChanges();
        return true;
    }
}
