namespace Lab.AspNetCore.Tests.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Lab.AspNetCore.Services;
using Xunit;

/// <summary>
/// M06.F05 计算规则 + M06.F06 技术要求 fnTest（B2）。
/// 语义基准：lab-springboot CalculationRuleServiceTest / TechnicalRequirementServiceTest
/// （复合键 / 默认值 manual+1 / 四过滤 / PATCH / 404）。
/// </summary>
public class RuleAndRequirementServiceTest
{
    private const string Tenant = "TENANT-001";

    private static CalculationRule Rule(string obj, string param, int sortOrder = 0, CalculationAlgorithmType algo = CalculationAlgorithmType.Formula) => new()
    {
        InspectionObjectCode = obj, InspectionParameterCode = param,
        AlgorithmType = algo, SpecimenCount = 3, CreatedAt = "t", UpdatedAt = "t", SortOrder = sortOrder,
    };

    // === M06.F05 计算规则 ===

    [Fact]
    [Trait("Fn", "M06.F05.I01")]
    public void ListRules_dualFilter()
    {
        var store = new InMemoryRuleStore();
        store.Save(Rule("OBJ-A", "P-1"));
        store.Save(Rule("OBJ-A", "P-2"));
        store.Save(Rule("OBJ-B", "P-1"));
        var service = new CalculationRuleService(store);

        Assert.Equal(2, service.List("OBJ-A", null).Count);
        Assert.Single(service.List("OBJ-A", "P-2"));
        Assert.Equal(3, service.List(null, null).Count); // 空串/null 不过滤
    }

    [Fact]
    [Trait("Fn", "M06.F05.I02")]
    public void GetRule_compositeKey_missing404()
    {
        var service = new CalculationRuleService(new InMemoryRuleStore());
        Assert.Throws<KeyNotFoundException>(() => service.Get("OBJ-A", "GHOST"));
    }

    [Fact]
    [Trait("Fn", "M06.F05.I03")]
    public void CreateRule_defaultsManualAndSpecimen1()
    {
        var service = new CalculationRuleService(new InMemoryRuleStore());

        var r = service.Create(new CreateCalculationRuleRequest
        {
            InspectionObjectCode = "OBJ-A", InspectionParameterCode = "P-NEW",
        });

        Assert.Equal(CalculationAlgorithmType.Manual, r.AlgorithmType); // 默认 manual
        Assert.Equal(1, r.SpecimenCount); // 默认 1
        Assert.Equal(r.CreatedAt, r.UpdatedAt);
    }

    [Fact]
    [Trait("Fn", "M06.F05.I04")]
    public void UpdateRule_patchKeepsUnset()
    {
        var store = new InMemoryRuleStore();
        store.Save(Rule("OBJ-A", "P-1"));
        var service = new CalculationRuleService(store);

        var r = service.Update("OBJ-A", "P-1", new UpdateCalculationRuleRequest { Formula = "f = m*a" });

        Assert.Equal("f = m*a", r.Formula);
        Assert.Equal(3, r.SpecimenCount); // 未传保留
        Assert.Equal(CalculationAlgorithmType.Formula, r.AlgorithmType);
    }

    [Fact]
    [Trait("Fn", "M06.F05.I04")]
    public void UpdateRule_missing404()
    {
        var service = new CalculationRuleService(new InMemoryRuleStore());
        Assert.Throws<KeyNotFoundException>(
            () => service.Update("GHOST", "P", new UpdateCalculationRuleRequest()));
    }

    [Fact]
    [Trait("Fn", "M06.F05.I05")]
    public void DeleteRule_compositeKey()
    {
        var store = new InMemoryRuleStore();
        store.Save(Rule("OBJ-A", "P-1"));
        store.Save(Rule("OBJ-A", "P-2"));
        var service = new CalculationRuleService(store);

        service.Delete("OBJ-A", "P-1");

        Assert.Single(service.List(null, null));
        Assert.Throws<KeyNotFoundException>(() => service.Delete("OBJ-A", "P-1"));
    }

    // === M06.F06 技术要求 ===

    private static TechnicalRequirement Req(string obj = "OBJ", string param = "PARAM", string std = "STD",
        RequirementVerificationStatus status = RequirementVerificationStatus.Verified) => new()
    {
        TenantId = Tenant, InspectionObjectCode = obj, InspectionParameterCode = param,
        JudgmentStandardCode = std, VerificationStatus = status,
        ValueType = RequirementValueType.Numeric, CreatedAt = "t", UpdatedAt = "t",
    };

    [Fact]
    [Trait("Fn", "M06.F06.I01")]
    public void ListRequirements_quadFilter_withStatus()
    {
        var store = new InMemoryRequirementStore();
        store.Save(Req("OBJ-1", "IP-1", "STD-1"));
        store.Save(Req("OBJ-1", "IP-2", "STD-1"));
        store.Save(Req("OBJ-2", "IP-1", "STD-1", status: RequirementVerificationStatus.Draft));
        var service = new TechnicalRequirementService(store);

        Assert.Equal(2, service.List(Tenant, "OBJ-1", null, null, null).Count);
        Assert.Single(service.List(Tenant, "OBJ-1", "IP-2", null, null));
        Assert.Single(service.List(Tenant, null, null, null, RequirementVerificationStatus.Draft));
        Assert.Empty(service.List("TENANT-002", null, null, null, null)); // tenant 隔离
    }

    [Fact]
    [Trait("Fn", "M06.F06.I02")]
    public void GetRequirement_tripleKey_missing404()
    {
        var service = new TechnicalRequirementService(new InMemoryRequirementStore());
        Assert.Throws<KeyNotFoundException>(() => service.Get(Tenant, "OBJ", "PARAM", "GHOST"));
    }

    [Fact]
    [Trait("Fn", "M06.F06.I03")]
    public void CreateRequirement_defaultsNumericGeManualDraft()
    {
        var service = new TechnicalRequirementService(new InMemoryRequirementStore());

        var t = service.Create(Tenant, new CreateTechnicalRequirementRequest
        {
            InspectionObjectCode = "OBJ", InspectionParameterCode = "PARAM", JudgmentStandardCode = "STD",
        });

        Assert.Equal(RequirementValueType.Numeric, t.ValueType);
        Assert.Equal(RequirementComparison.Ge, t.Comparison);
        Assert.Equal(RequirementJudgmentMode.Manual, t.JudgmentMode);
        Assert.Equal(RequirementVerificationStatus.Draft, t.VerificationStatus);
        Assert.Equal(Tenant, t.TenantId); // tenant 从上下文注入，不取 body
    }

    [Fact]
    [Trait("Fn", "M06.F06.I04")]
    public void UpdateRequirement_patchKeepsUnset()
    {
        var store = new InMemoryRequirementStore();
        store.Save(Req(status: RequirementVerificationStatus.Verified));
        var service = new TechnicalRequirementService(store);

        var t = service.Update(Tenant, "OBJ", "PARAM", "STD",
            new UpdateTechnicalRequirementRequest { MinValue = 30, Unit = "MPa" });

        Assert.Equal(30, t.MinValue);
        Assert.Equal("MPa", t.Unit);
        Assert.Equal(RequirementVerificationStatus.Verified, t.VerificationStatus); // 未传保留
    }

    [Fact]
    [Trait("Fn", "M06.F06.I04")]
    public void UpdateRequirement_fourDimensionFields()
    {
        var store = new InMemoryRequirementStore();
        store.Save(Req());
        var service = new TechnicalRequirementService(store);

        var t = service.Update(Tenant, "OBJ", "PARAM", "STD", new UpdateTechnicalRequirementRequest
        {
            Brand = "HRB400", Model = "M-1", Grade = "G-1", Spec = "S-1",
        });

        Assert.Equal("HRB400", t.Brand);
        Assert.Equal("M-1", t.Model);
        Assert.Equal("G-1", t.Grade);
        Assert.Equal("S-1", t.Spec);
    }

    [Fact]
    [Trait("Fn", "M06.F06.I05")]
    public void DeleteRequirement_tripleKey()
    {
        var store = new InMemoryRequirementStore();
        store.Save(Req());
        var service = new TechnicalRequirementService(store);

        service.Delete(Tenant, "OBJ", "PARAM", "STD");

        Assert.Empty(service.List(Tenant, null, null, null, null));
        Assert.Throws<KeyNotFoundException>(() => service.Delete(Tenant, "OBJ", "PARAM", "STD"));
    }
}
