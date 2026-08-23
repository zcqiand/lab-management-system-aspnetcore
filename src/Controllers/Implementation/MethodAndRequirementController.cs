namespace Lab.AspNetCore.Controllers.Implementation;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Security;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>M06.F05 计算方法 CRUD（B2，5 端点，平台级无 tenant）。</summary>
[ApiController]
[Authorize]
public sealed class CalculationMethodsController(CalculationMethodService service)
    : CalculationMethodsControllerBase
{
    private readonly CalculationMethodService _service = service;

    public override Task<System.Collections.Generic.ICollection<CalculationMethod>> ListCalculationMethods(
        [FromQuery] string inspectionObjectCode, [FromQuery] string inspectionParameterCode) =>
        Task.FromResult<System.Collections.Generic.ICollection<CalculationMethod>>(
            _service.List(inspectionObjectCode, inspectionParameterCode).ToList());

    public override Task<CalculationMethod> CreateCalculationMethod([FromBody] CreateCalculationMethodRequest body) =>
        Task.FromResult(_service.Create(body));

    public override Task<CalculationMethod> GetCalculationMethod(string inspectionObjectCode, string inspectionParameterCode) =>
        Task.FromResult(_service.Get(inspectionObjectCode, inspectionParameterCode));

    public override Task<CalculationMethod> UpdateCalculationMethod(
        string inspectionObjectCode, string inspectionParameterCode, [FromBody] UpdateCalculationMethodRequest body) =>
        Task.FromResult(_service.Update(inspectionObjectCode, inspectionParameterCode, body));

    public override Task DeleteCalculationMethod(string inspectionObjectCode, string inspectionParameterCode)
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
