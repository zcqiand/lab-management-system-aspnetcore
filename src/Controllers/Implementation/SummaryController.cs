namespace Lab.AspNetCore.Controllers.Implementation;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Security;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>M05.F01 报告汇总 + M05.F02 仪表盘统计（B4，2 端点）。</summary>
[ApiController]
[Authorize]
public sealed class SummaryController(SummaryService service, ITenantContext tenantContext)
    : SummaryControllerBase
{
    private readonly SummaryService _service = service;
    private readonly ITenantContext _tenantContext = tenantContext;

    public override Task<SummaryData> GetReportSummary(
        [FromQuery] string? categoryCode, [FromQuery] string? dateFrom, [FromQuery] string? dateTo) =>
        Task.FromResult(_service.GetReportSummary(_tenantContext.TenantId, categoryCode, dateFrom, dateTo));

    public override Task<DashboardStats> GetDashboardStats() =>
        Task.FromResult(_service.GetDashboardStats(_tenantContext.TenantId));
}
