namespace Lab.AspNetCore.Data;

using System.Collections.Concurrent;
using Lab.AspNetCore.Controllers.Generated;

/// <summary>
/// B4 汇总+仪表盘的数据源（内存）。B3 流程域落地后由其 store 取代——
/// 这里只放 summary/stats 需要的最小形状：合同/样品按 tenant 计数，
/// 接样单带 categoryCode/commissionDate/flowStatus 供过滤与 3 桶聚合。
/// </summary>
public sealed class InMemoryFlowStore
{
    private readonly ConcurrentDictionary<(string Tenant, string Id), Contract> _contracts = new();
    private readonly ConcurrentDictionary<(string Tenant, string Id), Sample> _samples = new();
    private readonly ConcurrentDictionary<(string Tenant, string Id), SampleReceipt> _receipts = new();

    // === 合同（计数用） ===

    public int CountContracts(string tenantId) =>
        _contracts.Values.Count(c => tenantId == "" || c.TenantId == tenantId);

    public void SaveContract(Contract c) => _contracts[(c.TenantId, c.Id)] = c;

    // === 样品（计数用） ===

    public int CountSamples(string tenantId) =>
        _samples.Values.Count(s => tenantId == "" || s.TenantId == tenantId);

    public void SaveSample(Sample s) => _samples[(s.TenantId, s.Id)] = s;

    // === 接样单（summary 行 + 3 桶聚合） ===

    /// <summary>
    /// summary 查询：tenant（空串=全租户）+ categoryCode（"ALL"=不过滤）+
    /// commissionDate 闭区间（空串=无界，YYYY-MM-DD 字典序）。
    /// 排序 commissionDate DESC, commissionCode ASC（镜像 springboot JPQL）。
    /// </summary>
    public IReadOnlyList<SampleReceipt> Summary(string tenantId, string categoryCode, string dateFrom, string dateTo) =>
        _receipts.Values
            .Where(r => tenantId == "" || r.TenantId == tenantId)
            .Where(r => categoryCode == "ALL" || r.CategoryCode == categoryCode)
            .Where(r => dateFrom == "" || string.Compare(r.CommissionDate ?? "", dateFrom, StringComparison.Ordinal) >= 0)
            .Where(r => dateTo == "" || string.Compare(r.CommissionDate ?? "", dateTo, StringComparison.Ordinal) <= 0)
            .OrderByDescending(r => r.CommissionDate)
            .ThenBy(r => r.CommissionCode)
            .ToList();

    public void SaveReceipt(SampleReceipt r) => _receipts[(r.TenantId, r.Id)] = r;
}
