namespace Lab.AspNetCore.Controllers.Implementation;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Security;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>M06.F05 计算规则 CRUD（B2，5 端点，平台级无 tenant）。</summary>
[ApiController]
[Authorize]
public sealed class CalculationRulesController(CalculationRuleService service)
    : CalculationRulesControllerBase
{
    private readonly CalculationRuleService _service = service;

    public override Task<System.Collections.Generic.ICollection<CalculationRule>> ListCalculationRules(
        [FromQuery] string inspectionObjectCode, [FromQuery] string inspectionParameterCode) =>
        Task.FromResult<System.Collections.Generic.ICollection<CalculationRule>>(
            _service.List(inspectionObjectCode, inspectionParameterCode).ToList());

    public override Task<CalculationRule> CreateCalculationRule([FromBody] CreateCalculationRuleRequest body) =>
        Task.FromResult(_service.Create(body));

    public override Task<CalculationRule> GetCalculationRule(string inspectionObjectCode, string inspectionParameterCode) =>
        Task.FromResult(_service.Get(inspectionObjectCode, inspectionParameterCode));

    public override Task<CalculationRule> UpdateCalculationRule(
        string inspectionObjectCode, string inspectionParameterCode, [FromBody] UpdateCalculationRuleRequest body) =>
        Task.FromResult(_service.Update(inspectionObjectCode, inspectionParameterCode, body));

    public override Task DeleteCalculationRule(string inspectionObjectCode, string inspectionParameterCode)
    {
        _service.Delete(inspectionObjectCode, inspectionParameterCode);
        return Task.CompletedTask;
    }
}

/// <summary>M06.F06 技术要求 CRUD（B2，5 端点，tenant 从 claim 注入）。</summary>
[ApiController]
[Authorize]
public sealed class TechnicalRequirementsController(TechnicalRequirementService service, ITenantContext tenantContext)
    : TechnicalRequirementsControllerBase
{
    private readonly TechnicalRequirementService _service = service;
    private readonly ITenantContext _tenantContext = tenantContext;

    public override Task<System.Collections.Generic.ICollection<TechnicalRequirement>> ListTechnicalRequirements(
        [FromQuery] string inspectionObjectCode, [FromQuery] string inspectionParameterCode,
        [FromQuery] string judgmentStandardCode, [FromQuery] RequirementVerificationStatus? verificationStatus) =>
        Task.FromResult<System.Collections.Generic.ICollection<TechnicalRequirement>>(
            _service.List(_tenantContext.TenantId, inspectionObjectCode, inspectionParameterCode, judgmentStandardCode, verificationStatus).ToList());

    public override Task<TechnicalRequirement> CreateTechnicalRequirement([FromBody] CreateTechnicalRequirementRequest body) =>
        Task.FromResult(_service.Create(_tenantContext.TenantId, body));

    public override Task<TechnicalRequirement> GetTechnicalRequirement(
        string inspectionObjectCode, string inspectionParameterCode, string judgmentStandardCode) =>
        Task.FromResult(_service.Get(_tenantContext.TenantId, inspectionObjectCode, inspectionParameterCode, judgmentStandardCode));

    public override Task<TechnicalRequirement> UpdateTechnicalRequirement(
        string inspectionObjectCode, string inspectionParameterCode, string judgmentStandardCode,
        [FromBody] UpdateTechnicalRequirementRequest body) =>
        Task.FromResult(_service.Update(_tenantContext.TenantId, inspectionObjectCode, inspectionParameterCode, judgmentStandardCode, body));

    public override Task DeleteTechnicalRequirement(
        string inspectionObjectCode, string inspectionParameterCode, string judgmentStandardCode)
    {
        _service.Delete(_tenantContext.TenantId, inspectionObjectCode, inspectionParameterCode, judgmentStandardCode);
        return Task.CompletedTask;
    }
}
