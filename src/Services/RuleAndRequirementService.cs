namespace Lab.AspNetCore.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// M06.F05 计算规则 CRUD（B2）。复合主键 (objectCode, parameterCode)，平台级无 tenant。
/// 创建默认 algorithmType=Manual、specimenCount=1（镜像 springboot CalculationRuleMapper）。
/// </summary>
public sealed class CalculationRuleService(InMemoryRuleStore store)
{
    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public IReadOnlyList<CalculationRule> List(string? objectCode, string? parameterCode) =>
        store.Filter(objectCode, parameterCode);

    public CalculationRule Get(string objectCode, string parameterCode) =>
        store.Find(objectCode, parameterCode) ?? throw new KeyNotFoundException($"rule {objectCode}/{parameterCode} not found");

    public CalculationRule Create(CreateCalculationRuleRequest body)
    {
        var now = Now();
        var r = new CalculationRule
        {
            InspectionObjectCode = body.InspectionObjectCode,
            InspectionParameterCode = body.InspectionParameterCode,
            TestingStandardCode = body.TestingStandardCode ?? "",
            ReportNameCode = body.ReportNameCode ?? "",
            AlgorithmType = body.AlgorithmType == default ? CalculationAlgorithmType.Manual : body.AlgorithmType,
            SpecimenCount = body.SpecimenCount == 0 ? 1 : body.SpecimenCount,
            Formula = body.Formula ?? "",
            Conditions = body.Conditions ?? "",
            RoundingRule = body.RoundingRule ?? "",
            Remark = body.Remark ?? "",
            SortOrder = body.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.Save(r);
        return r;
    }

    public CalculationRule Update(string objectCode, string parameterCode, UpdateCalculationRuleRequest body)
    {
        var r = Get(objectCode, parameterCode);
        if (body.TestingStandardCode is not null) r.TestingStandardCode = body.TestingStandardCode;
        if (body.ReportNameCode is not null) r.ReportNameCode = body.ReportNameCode;
        if (body.AlgorithmType != default) r.AlgorithmType = body.AlgorithmType;
        if (body.SpecimenCount != 0) r.SpecimenCount = body.SpecimenCount;
        if (body.Formula is not null) r.Formula = body.Formula;
        if (body.Conditions is not null) r.Conditions = body.Conditions;
        if (body.RoundingRule is not null) r.RoundingRule = body.RoundingRule;
        if (body.Remark is not null) r.Remark = body.Remark;
        if (body.SortOrder != 0) r.SortOrder = body.SortOrder;
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
public sealed class TechnicalRequirementService(InMemoryRequirementStore store)
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
            ValueType = body.ValueType == default ? RequirementValueType.Numeric : body.ValueType,
            MinValue = body.MinValue,
            MaxValue = body.MaxValue,
            TargetValue = body.TargetValue ?? "",
            Expression = body.Expression ?? "",
            Unit = body.Unit ?? "",
            Comparison = body.Comparison == default ? RequirementComparison.Ge : body.Comparison,
            JudgmentMode = body.JudgmentMode == default ? RequirementJudgmentMode.Manual : body.JudgmentMode,
            VerificationStatus = body.VerificationStatus == default ? RequirementVerificationStatus.Draft : body.VerificationStatus,
            Clause = body.Clause ?? "",
            SourcePage = body.SourcePage,
            SourceHash = body.SourceHash ?? "",
            Brand = body.Brand ?? "",
            Model = body.Model ?? "",
            Grade = body.Grade ?? "",
            Spec = body.Spec ?? "",
            Sieve = body.Sieve ?? "",
            Remark = body.Remark ?? "",
            SortOrder = body.SortOrder,
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
        if (body.ValueType != default) t.ValueType = body.ValueType;
        if (body.MinValue != 0) t.MinValue = body.MinValue;
        if (body.MaxValue != 0) t.MaxValue = body.MaxValue;
        if (body.TargetValue is not null) t.TargetValue = body.TargetValue;
        if (body.Expression is not null) t.Expression = body.Expression;
        if (body.Unit is not null) t.Unit = body.Unit;
        if (body.Comparison != default) t.Comparison = body.Comparison;
        if (body.JudgmentMode != default) t.JudgmentMode = body.JudgmentMode;
        if (body.VerificationStatus != default) t.VerificationStatus = body.VerificationStatus;
        if (body.Clause is not null) t.Clause = body.Clause;
        if (body.SourcePage != 0) t.SourcePage = body.SourcePage;
        if (body.SourceHash is not null) t.SourceHash = body.SourceHash;
        if (body.Brand is not null) t.Brand = body.Brand;
        if (body.Model is not null) t.Model = body.Model;
        if (body.Grade is not null) t.Grade = body.Grade;
        if (body.Spec is not null) t.Spec = body.Spec;
        if (body.Sieve is not null) t.Sieve = body.Sieve;
        if (body.Remark is not null) t.Remark = body.Remark;
        if (body.SortOrder != 0) t.SortOrder = body.SortOrder;
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
