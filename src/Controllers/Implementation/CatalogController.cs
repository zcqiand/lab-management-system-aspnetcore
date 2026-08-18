namespace Lab.AspNetCore.Controllers.Implementation;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Security;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// M04.F06/F07/F08/F09 — 码表 4 组 × 4 端点（B2，16 端点）。
/// tenant 从 token claim 注入，dev fallback TENANT-001（镜像 springboot currentTenantIdOrDefault）。
/// </summary>
[ApiController]
[Authorize]
public sealed class CatalogController(CatalogService service, ITenantContext tenantContext)
    : CatalogControllerBase
{
    private readonly CatalogService _service = service;
    private readonly ITenantContext _tenantContext = tenantContext;

    // === M04.F06 型号 ===

    public override Task<System.Collections.Generic.ICollection<InspectionModel>> ListModels(
        [FromQuery] string inspectionObjectCode, [FromQuery] string keyword) =>
        Task.FromResult<System.Collections.Generic.ICollection<InspectionModel>>(
            _service.ListModels(_tenantContext.TenantId, inspectionObjectCode, keyword).ToList());

    public override Task<InspectionModel> CreateModel([FromBody] CreateCatalogEntryRequest body) =>
        Task.FromResult(_service.CreateModel(_tenantContext.TenantId, body));

    public override Task<InspectionModel> UpdateModel(string code, [FromBody] UpdateCatalogEntryRequest body) =>
        Task.FromResult(_service.UpdateModel(_tenantContext.TenantId, code, body));

    public override Task DeleteModel(string code)
    {
        _service.DeleteModel(_tenantContext.TenantId, code);
        return Task.CompletedTask;
    }

    // === M04.F07 规格 ===

    public override Task<System.Collections.Generic.ICollection<InspectionSpec>> ListSpecs(
        [FromQuery] string inspectionObjectCode, [FromQuery] string keyword) =>
        Task.FromResult<System.Collections.Generic.ICollection<InspectionSpec>>(
            _service.ListSpecs(_tenantContext.TenantId, inspectionObjectCode, keyword).ToList());

    public override Task<InspectionSpec> CreateSpec([FromBody] CreateCatalogEntryRequest body) =>
        Task.FromResult(_service.CreateSpec(_tenantContext.TenantId, body));

    public override Task<InspectionSpec> UpdateSpec(string code, [FromBody] UpdateCatalogEntryRequest body) =>
        Task.FromResult(_service.UpdateSpec(_tenantContext.TenantId, code, body));

    public override Task DeleteSpec(string code)
    {
        _service.DeleteSpec(_tenantContext.TenantId, code);
        return Task.CompletedTask;
    }

    // === M04.F08 等级 ===

    public override Task<System.Collections.Generic.ICollection<InspectionGrade>> ListGrades(
        [FromQuery] string inspectionObjectCode, [FromQuery] string keyword) =>
        Task.FromResult<System.Collections.Generic.ICollection<InspectionGrade>>(
            _service.ListGrades(_tenantContext.TenantId, inspectionObjectCode, keyword).ToList());

    public override Task<InspectionGrade> CreateGrade([FromBody] CreateCatalogEntryRequest body) =>
        Task.FromResult(_service.CreateGrade(_tenantContext.TenantId, body));

    public override Task<InspectionGrade> UpdateGrade(string code, [FromBody] UpdateCatalogEntryRequest body) =>
        Task.FromResult(_service.UpdateGrade(_tenantContext.TenantId, code, body));

    public override Task DeleteGrade(string code)
    {
        _service.DeleteGrade(_tenantContext.TenantId, code);
        return Task.CompletedTask;
    }

    // === M04.F09 牌号 ===

    public override Task<System.Collections.Generic.ICollection<InspectionBrand>> ListBrands(
        [FromQuery] string inspectionObjectCode, [FromQuery] string keyword) =>
        Task.FromResult<System.Collections.Generic.ICollection<InspectionBrand>>(
            _service.ListBrands(_tenantContext.TenantId, inspectionObjectCode, keyword).ToList());

    public override Task<InspectionBrand> CreateBrand([FromBody] CreateCatalogEntryRequest body) =>
        Task.FromResult(_service.CreateBrand(_tenantContext.TenantId, body));

    public override Task<InspectionBrand> UpdateBrand(string code, [FromBody] UpdateCatalogEntryRequest body) =>
        Task.FromResult(_service.UpdateBrand(_tenantContext.TenantId, code, body));

    public override Task DeleteBrand(string code)
    {
        _service.DeleteBrand(_tenantContext.TenantId, code);
        return Task.CompletedTask;
    }
}
