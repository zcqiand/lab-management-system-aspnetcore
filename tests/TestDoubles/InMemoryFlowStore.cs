namespace Lab.AspNetCore.Tests.TestDoubles;

using System.Collections.Concurrent;
using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// B3 流程域内存存储（合同/接样/样品/检测记录），B4 汇总也从这里取数。
/// 语义镜像 springboot JPA repository：tenant 收口 + 各 list 过滤 + 排序；
/// 删除约束：合同被接样引用 RESTRICT、接样删除 CASCADE 下属样品。
/// </summary>
public sealed class InMemoryFlowStore : IFlowStore
{
    private readonly ConcurrentDictionary<(string Tenant, string Id), Contract> _contracts = new();
    private readonly ConcurrentDictionary<(string Tenant, string Id), SampleReceipt> _receipts = new();
    private readonly ConcurrentDictionary<(string Tenant, string Id), Sample> _samples = new();
    private readonly ConcurrentDictionary<(string Tenant, string Id), TestRecord> _records = new();

    private static string N(string? s) => s ?? "";

    // === 合同 M02.F01 ===

    public IReadOnlyList<Contract> FilterContracts(string tenantId, string? keyword, ContractStatus? status) =>
        _contracts.Values
            .Where(c => c.TenantId == tenantId)
            .Where(c => string.IsNullOrEmpty(keyword)
                || (c.ContractCode ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (c.ProjectName ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Where(c => status is null || c.Status == status)
            .OrderBy(c => c.CreatedAt)
            .ToList();

    public Contract? FindContract(string tenantId, string id) =>
        _contracts.TryGetValue((tenantId, id), out var c) ? c : null;

    public bool ContractReferenced(string contractId) =>
        _receipts.Values.Any(r => r.ContractId == contractId);

    public void SaveContract(Contract c) => _contracts[(c.TenantId, c.Id)] = c;

    public bool DeleteContract(string tenantId, string id) => _contracts.TryRemove((tenantId, id), out _);

    // === 接样 M03.F01（含 B4 summary 查询） ===

    /// <summary>
    /// 列表 + 三态 filter（5.57 入契约，语义 SSOT = lab-nextjs db-queries.ts:53-58）：
    ///   not_yet   = 停在 flowStatus 环节待提交（无 flowStatus 时 = 无流转记录的新单）
    ///   submitted = 已从本环节 submit 至下一环节（无 flowStatus 时 = 有流转记录且记录了提交人）
    ///   其余值（含 null）不参与过滤——flowStatus 精确过滤照旧生效。
    /// </summary>
    public IReadOnlyList<SampleReceipt> FilterReceipts(string tenantId, string? contractId, FlowStatus? flowStatus, string? keyword, string? filter = null)
    {
        IEnumerable<SampleReceipt> q = _receipts.Values.Where(r => r.TenantId == tenantId);
        if (filter == "not_yet")
        {
            q = flowStatus is null
                ? q.Where(r => r.FlowHistory.Count == 0)
                : q.Where(r => r.FlowStatus == flowStatus);
        }
        else if (filter == "submitted")
        {
            q = flowStatus is null
                ? q.Where(r => r.FlowHistory.Count > 0 && r.LastSubmittedBy != null)
                : q.Where(r => r.FlowStatus != flowStatus
                    && r.FlowHistory.Any(h => h.Action == FlowAction.Submit && h.From == flowStatus));
        }
        else if (flowStatus is not null)
        {
            q = q.Where(r => r.FlowStatus == flowStatus);
        }

        return q
            .Where(r => N(contractId) == "" || r.ContractId == N(contractId))
            .Where(r => string.IsNullOrEmpty(keyword)
                || (r.CommissionCode ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (r.ProjectName ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.CreatedAt)
            .ToList();
    }

    /// <summary>B4 summary：tenant（空串=全租户）+ categoryCode（ALL=不过滤）+ commissionDate 闭区间，commissionDate DESC, code。</summary>
    public IReadOnlyList<SampleReceipt> Summary(string tenantId, string categoryCode, string dateFrom, string dateTo) =>
        _receipts.Values
            .Where(r => tenantId == "" || r.TenantId == tenantId)
            .Where(r => categoryCode == "ALL" || r.CategoryCode == categoryCode)
            .Where(r => dateFrom == "" || string.Compare(r.CommissionDate ?? "", dateFrom, StringComparison.Ordinal) >= 0)
            .Where(r => dateTo == "" || string.Compare(r.CommissionDate ?? "", dateTo, StringComparison.Ordinal) <= 0)
            .OrderByDescending(r => r.CommissionDate)
            .ThenBy(r => r.CommissionCode)
            .ToList();

    /// <summary>B3 流程队列：stage 精确 + tenant 收口，pageSize 默认 50 cap 200。</summary>
    public IReadOnlyList<SampleReceipt> FlowQueue(string tenantId, FlowStatus stage, int pageSize) =>
        _receipts.Values
            .Where(r => r.TenantId == tenantId && r.FlowStatus == stage)
            .OrderBy(r => r.CreatedAt)
            .Take(Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200))
            .ToList();

    public SampleReceipt? FindReceipt(string tenantId, string id) =>
        _receipts.TryGetValue((tenantId, id), out var r) ? r : null;

    public SampleReceipt? FindReceiptAnyTenant(string id) =>
        _receipts.Values.FirstOrDefault(r => r.Id == id);

    public void SaveReceipt(SampleReceipt r) => _receipts[(r.TenantId, r.Id)] = r;

    public bool DeleteReceipt(string tenantId, string id, out int cascadedSamples)
    {
        cascadedSamples = 0;
        if (!_receipts.TryRemove((tenantId, id), out _))
        {
            return false;
        }

        foreach (var key in _samples.Keys.Where(k => _samples[k].ReceiptId == id).ToList())
        {
            if (_samples.TryRemove(key, out _))
            {
                cascadedSamples++;
            }
        }
        return true;
    }

    // === 样品 M03.F03 ===

    public IReadOnlyList<Sample> FilterSamples(string tenantId, string? receiptId, string? keyword) =>
        _samples.Values
            .Where(s => s.TenantId == tenantId)
            .Where(s => N(receiptId) == "" || s.ReceiptId == N(receiptId))
            .Where(s => string.IsNullOrEmpty(keyword)
                || (s.SampleCode ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (s.SampleName ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.CreatedAt)
            .ThenBy(s => s.SampleCode)
            .ToList();

    public Sample? FindSample(string tenantId, string id) =>
        _samples.TryGetValue((tenantId, id), out var s) ? s : null;

    public bool ReceiptExists(string receiptId) => _receipts.Values.Any(r => r.Id == receiptId);

    public bool ContractExists(string contractId) => _contracts.Values.Any(c => c.Id == contractId);

    public void SaveSample(Sample s) => _samples[(s.TenantId, s.Id)] = s;

    public bool DeleteSample(string tenantId, string id) => _samples.TryRemove((tenantId, id), out _);

    public int CountSamples(string tenantId) => _samples.Values.Count(s => tenantId == "" || s.TenantId == tenantId);

    public int CountContracts(string tenantId) => _contracts.Values.Count(c => tenantId == "" || c.TenantId == tenantId);

    // === 检测记录 M03.F03.I06-I11 ===

    public IReadOnlyList<TestRecord> FilterRecords(string tenantId, string? sampleId) =>
        _records.Values
            .Where(t => t.TenantId == tenantId)
            .Where(t => N(sampleId) == "" || t.SampleId == N(sampleId))
            .OrderBy(t => t.CreatedAt)
            .ToList();

    public TestRecord? FindRecord(string tenantId, string id) =>
        _records.TryGetValue((tenantId, id), out var t) ? t : null;

    public void SaveRecord(TestRecord t) => _records[(t.TenantId, t.Id)] = t;

    public bool DeleteRecord(string tenantId, string id) => _records.TryRemove((tenantId, id), out _);
}
