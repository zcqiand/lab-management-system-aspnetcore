namespace Lab.AspNetCore.Persistence;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Microsoft.EntityFrameworkCore;

public sealed class EfFlowStore(LabDbContext db) : IFlowStore
{
    private static bool Kw(string? a, string? b, string? keyword) =>
        keyword == null || keyword == ""
        || (a != null && a.ToLower().Contains(keyword.ToLower()))
        || (b != null && b.ToLower().Contains(keyword.ToLower()));

    // === 合同 M02.F01 ===

    public IReadOnlyList<Contract> FilterContracts(string tenantId, string? keyword, ContractStatus? status) =>
        db.Contracts
            .Where(c => c.TenantId == tenantId)
            .Where(c => Kw(c.ContractCode, c.ProjectName, keyword))
            .Where(c => status == null || c.Status == status)
            .OrderBy(c => c.CreatedAt)
            .ToList();

    public Contract? FindContract(string tenantId, string id) =>
        db.Contracts.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id);

    public bool ContractReferenced(string contractId) =>
        db.SampleReceipts.Any(r => r.ContractId == contractId);

    public void SaveContract(Contract c) =>
        EfStoreOps.Upsert(db, db.Contracts, c, x => x.Id == c.Id);

    public bool DeleteContract(string tenantId, string id)
    {
        // RESTRICT 语义：service 层已用 ContractReferenced 前置拦截；这里防御性再查一次
        if (ContractReferenced(id))
        {
            throw new InvalidOperationException($"contract {id} is referenced by receipts");
        }

        return db.Contracts.Where(c => c.TenantId == tenantId && c.Id == id).ExecuteDelete() > 0;
    }

    // === 接样 M03.F01（含 B4 summary） ===

    public IReadOnlyList<SampleReceipt> FilterReceipts(string tenantId, string? contractId, FlowStatus? flowStatus, string? keyword) =>
        db.SampleReceipts
            .Where(r => r.TenantId == tenantId)
            .Where(r => contractId == null || contractId == "" || r.ContractId == contractId)
            .Where(r => flowStatus == null || r.FlowStatus == flowStatus)
            .Where(r => Kw(r.CommissionCode, r.ProjectName, keyword))
            .OrderBy(r => r.CreatedAt)
            .ToList();

    public IReadOnlyList<SampleReceipt> Summary(string tenantId, string categoryCode, string dateFrom, string dateTo) =>
        db.SampleReceipts
            .Where(r => tenantId == "" || r.TenantId == tenantId)
            .Where(r => categoryCode == "ALL" || r.CategoryCode == categoryCode)
            .Where(r => dateFrom == "" || string.Compare(r.CommissionDate ?? "", dateFrom) >= 0)
            .Where(r => dateTo == "" || string.Compare(r.CommissionDate ?? "", dateTo) <= 0)
            .OrderByDescending(r => r.CommissionDate)
            .ThenBy(r => r.CommissionCode)
            .ToList();

    public IReadOnlyList<SampleReceipt> FlowQueue(string tenantId, FlowStatus stage, int pageSize) =>
        db.SampleReceipts
            .Where(r => r.TenantId == tenantId && r.FlowStatus == stage)
            .OrderBy(r => r.CreatedAt)
            .Take(Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200))
            .ToList();

    public SampleReceipt? FindReceipt(string tenantId, string id) =>
        db.SampleReceipts.FirstOrDefault(r => r.TenantId == tenantId && r.Id == id);

    public SampleReceipt? FindReceiptAnyTenant(string id) =>
        db.SampleReceipts.FirstOrDefault(r => r.Id == id);

    public void SaveReceipt(SampleReceipt r) =>
        EfStoreOps.Upsert(db, db.SampleReceipts, r, x => x.Id == r.Id);

    public bool DeleteReceipt(string tenantId, string id, out int cascadedSamples)
    {
        var receipt = db.SampleReceipts.FirstOrDefault(r => r.TenantId == tenantId && r.Id == id);
        if (receipt is null)
        {
            cascadedSamples = 0;
            return false;
        }

        cascadedSamples = db.Samples.Count(s => s.ReceiptId == id);
        db.SampleReceipts.Remove(receipt); // 下属样品/记录由 DB CASCADE 级联
        db.SaveChanges();
        return true;
    }

    // === 样品 M03.F03 ===

    public IReadOnlyList<Sample> FilterSamples(string tenantId, string? receiptId, string? keyword) =>
        db.Samples
            .Where(s => s.TenantId == tenantId)
            .Where(s => receiptId == null || receiptId == "" || s.ReceiptId == receiptId)
            .Where(s => Kw(s.SampleCode, s.SampleName, keyword))
            .OrderByDescending(s => s.CreatedAt)
            .ThenBy(s => s.SampleCode)
            .ToList();

    public Sample? FindSample(string tenantId, string id) =>
        db.Samples.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id);

    public bool ReceiptExists(string receiptId) => db.SampleReceipts.Any(r => r.Id == receiptId);

    public bool ContractExists(string contractId) => db.Contracts.Any(c => c.Id == contractId);

    public void SaveSample(Sample s) => EfStoreOps.Upsert(db, db.Samples, s, x => x.Id == s.Id);

    public bool DeleteSample(string tenantId, string id) =>
        db.Samples.Where(s => s.TenantId == tenantId && s.Id == id).ExecuteDelete() > 0;

    public int CountSamples(string tenantId) =>
        db.Samples.Count(s => tenantId == "" || s.TenantId == tenantId);

    public int CountContracts(string tenantId) =>
        db.Contracts.Count(c => tenantId == "" || c.TenantId == tenantId);

    // === 检测记录 M03.F03.I06-I11 ===

    public IReadOnlyList<TestRecord> FilterRecords(string tenantId, string? sampleId) =>
        db.TestRecords
            .Where(t => t.TenantId == tenantId)
            .Where(t => sampleId == null || sampleId == "" || t.SampleId == sampleId)
            .OrderBy(t => t.CreatedAt)
            .ToList();

    public TestRecord? FindRecord(string tenantId, string id) =>
        db.TestRecords.FirstOrDefault(t => t.TenantId == tenantId && t.Id == id);

    public void SaveRecord(TestRecord t) => EfStoreOps.Upsert(db, db.TestRecords, t, x => x.Id == t.Id);

    public bool DeleteRecord(string tenantId, string id) =>
        db.TestRecords.Where(t => t.TenantId == tenantId && t.Id == id).ExecuteDelete() > 0;
}
