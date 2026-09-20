namespace Lab.AspNetCore.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// M06.F05 计算方法 CRUD（B2）。复合主键 (objectCode, parameterCode)，平台级无 tenant。
/// 创建默认 algorithmType=Manual、specimenCount=1（镜像 springboot CalculationMethodMapper）。
/// </summary>
public sealed class CalculationMethodService(IMethodStore store)
{
    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public IReadOnlyList<CalculationMethod> List(string? objectCode, string? parameterCode) =>
        store.Filter(objectCode, parameterCode);

    public CalculationMethod Get(string objectCode, string parameterCode) =>
        store.Find(objectCode, parameterCode) ?? throw new KeyNotFoundException($"rule {objectCode}/{parameterCode} not found");

    public CalculationMethod Create(CreateCalculationMethodRequest body)
    {
        var now = Now();
        var r = new CalculationMethod
        {
            InspectionObjectCode = body.InspectionObjectCode,
            InspectionParameterCode = body.InspectionParameterCode,
            TestingStandardCode = body.TestingStandardCode ?? "",
            ReportNameCode = body.ReportNameCode ?? "",
            AlgorithmType = body.AlgorithmType ?? CalculationAlgorithmType.Manual,
            SpecimenCount = body.SpecimenCount is null ? 1 : body.SpecimenCount.Value,
            Formula = body.Formula ?? "",
            Conditions = body.Conditions ?? "",
            RoundingRule = body.RoundingRule ?? "",
            Remark = body.Remark ?? "",
            SortOrder = body.SortOrder ?? 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.Save(r);
        return r;
    }

    public CalculationMethod Update(string objectCode, string parameterCode, UpdateCalculationMethodRequest body)
    {
        var r = Get(objectCode, parameterCode);
        if (body.TestingStandardCode is not null) r.TestingStandardCode = body.TestingStandardCode;
        if (body.ReportNameCode is not null) r.ReportNameCode = body.ReportNameCode;
        if (body.AlgorithmType is not null) r.AlgorithmType = body.AlgorithmType.Value;
        if (body.SpecimenCount is not null) r.SpecimenCount = body.SpecimenCount.Value;
        if (body.Formula is not null) r.Formula = body.Formula;
        if (body.Conditions is not null) r.Conditions = body.Conditions;
        if (body.RoundingRule is not null) r.RoundingRule = body.RoundingRule;
        if (body.Remark is not null) r.Remark = body.Remark;
        if (body.SortOrder is not null) r.SortOrder = body.SortOrder.Value;
        r.UpdatedAt = Now();
        store.Save(r);
        return r;
    }

    public void Delete(string objectCode, string parameterCode)
    {
        if (!store.Delete(objectCode, parameterCode))
        {
            throw new KeyNotFoundException($"rule {objectCode}/{parameterCode} not found");
        }
    }
}

/// <summary>
/// M06.F06 技术要求 CRUD（B2）。业务三键 (object, parameter, judgmentStandard) + tenant。
/// 创建默认 numeric/Ge/Manual/Draft；tenant 从 token claim 注入（controller 层）。
/// </summary>
public sealed class TechnicalRequirementService(IRequirementStore store)
{
    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public IReadOnlyList<TechnicalRequirement> List(
        string tenantId, string? objectCode, string? parameterCode, string? standardCode,
        RequirementVerificationStatus? status) =>
        store.Filter(tenantId, objectCode, parameterCode, standardCode, status);

    public TechnicalRequirement Get(string tenantId, string objectCode, string parameterCode, string standardCode) =>
        store.Find(tenantId, objectCode, parameterCode, standardCode)
            ?? throw new KeyNotFoundException($"requirement {objectCode}/{parameterCode}/{standardCode} not found");

    public TechnicalRequirement Create(string tenantId, CreateTechnicalRequirementRequest body)
    {
        var now = Now();
        var t = new TechnicalRequirement
        {
            TenantId = tenantId,
            InspectionObjectCode = body.InspectionObjectCode,
            InspectionParameterCode = body.InspectionParameterCode,
            JudgmentStandardCode = body.JudgmentStandardCode,
            Conditions = body.Conditions ?? "",
            ValueType = body.ValueType ?? RequirementValueType.Numeric,
            MinValue = body.MinValue,
            MaxValue = body.MaxValue,
            TargetValue = body.TargetValue ?? "",
            Expression = body.Expression ?? "",
            Unit = body.Unit ?? "",
            Comparison = body.Comparison ?? RequirementComparison.Ge,
            JudgmentMode = body.JudgmentMode ?? RequirementJudgmentMode.Manual,
            VerificationStatus = body.VerificationStatus ?? RequirementVerificationStatus.Draft,
            Clause = body.Clause ?? "",
            SourcePage = body.SourcePage,
            SourceHash = body.SourceHash ?? "",
            Brand = body.Brand ?? "",
            Model = body.Model ?? "",
            Grade = body.Grade ?? "",
            Spec = body.Spec ?? "",
            Sieve = body.Sieve ?? "",
            Remark = body.Remark ?? "",
            SortOrder = body.SortOrder ?? 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.Save(t);
        return t;
    }

    public TechnicalRequirement Update(string tenantId, string objectCode, string parameterCode, string standardCode, UpdateTechnicalRequirementRequest body)
    {
        var t = Get(tenantId, objectCode, parameterCode, standardCode);
        if (body.Conditions is not null) t.Conditions = body.Conditions;
        if (body.ValueType is not null) t.ValueType = body.ValueType.Value;
        if (body.MinValue is not null) t.MinValue = body.MinValue.Value;
        if (body.MaxValue is not null) t.MaxValue = body.MaxValue.Value;
        if (body.TargetValue is not null) t.TargetValue = body.TargetValue;
        if (body.Expression is not null) t.Expression = body.Expression;
        if (body.Unit is not null) t.Unit = body.Unit;
        if (body.Comparison is not null) t.Comparison = body.Comparison.Value;
        if (body.JudgmentMode is not null) t.JudgmentMode = body.JudgmentMode.Value;
        if (body.VerificationStatus is not null) t.VerificationStatus = body.VerificationStatus.Value;
        if (body.Clause is not null) t.Clause = body.Clause;
        if (body.SourcePage is not null) t.SourcePage = body.SourcePage.Value;
        if (body.SourceHash is not null) t.SourceHash = body.SourceHash;
        if (body.Brand is not null) t.Brand = body.Brand;
        if (body.Model is not null) t.Model = body.Model;
        if (body.Grade is not null) t.Grade = body.Grade;
        if (body.Spec is not null) t.Spec = body.Spec;
        if (body.Sieve is not null) t.Sieve = body.Sieve;
        if (body.Remark is not null) t.Remark = body.Remark;
        if (body.SortOrder is not null) t.SortOrder = body.SortOrder.Value;
        t.UpdatedAt = Now();
        store.Save(t);
        return t;
    }

    public void Delete(string tenantId, string objectCode, string parameterCode, string standardCode)
    {
        if (!store.Delete(tenantId, objectCode, parameterCode, standardCode))
        {
            throw new KeyNotFoundException($"requirement {objectCode}/{parameterCode}/{standardCode} not found");
        }
    }
}
