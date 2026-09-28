namespace Lab.AspNetCore.Controllers.Implementation;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Security;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>M05.F01 报告汇总 + 仪表盘统计 M05.F01.I06（B4，2 端点；ADR-0033 阶段二自 M05.F02.I01 改挂）。</summary>
[ApiController]
[Authorize]
public sealed class SummaryController(SummaryService service, ITenantContext tenantContext)
    : SummaryControllerBase
{
    private readonly SummaryService _service = service;
    private readonly ITenantContext _tenantContext = tenantContext;

    // @entry M05.F01.I01 — 报告汇总：按类目代码/日期闭区间聚合查询（book anchor xr-know-016）
    public override Task<SummaryData> GetReportSummary(
        [FromQuery] string? categoryCode, [FromQuery] string? dateFrom, [FromQuery] string? dateTo) =>
        Task.FromResult(_service.GetReportSummary(_tenantContext.TenantId, categoryCode, dateFrom, dateTo));

    // @entry M05.F01.I03 (book anchor xr-know-016)
    // @entry M05.F01.I04 (book anchor xr-know-016)
    // @entry M05.F01.I06 — 仪表盘统计基础端点：核心指标卡（合格率等） (book anchor xr-know-016)
    //    └ 六阶段任务状态漏斗聚合（book anchor xr-know-016）
    public override Task<DashboardStats> GetDashboardStats() =>
        Task.FromResult(_service.GetDashboardStats(_tenantContext.TenantId));
}
