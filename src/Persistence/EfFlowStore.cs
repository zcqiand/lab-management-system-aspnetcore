namespace Lab.AspNetCore.Persistence;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

public sealed class EfFlowStore(LabDbContext db) : IFlowStore
{
    // === 合同 M02.F01 ===

    public IReadOnlyList<Contract> FilterContracts(string tenantId, string? keyword, ContractStatus? status) =>
        BuildFilterContractsQuery(db, tenantId, keyword, status).ToList();

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
        BuildFilterReceiptsQuery(db, tenantId, contractId, flowStatus, keyword).ToList();

    public IReadOnlyList<SampleReceipt> Summary(string tenantId, string categoryCode, string dateFrom, string dateTo) =>
        BuildSummaryQuery(db, tenantId, categoryCode, dateFrom, dateTo).ToList();

    public IReadOnlyList<SampleReceipt> FlowQueue(string tenantId, FlowStatus stage, int pageSize) =>
        BuildFlowQueueQuery(db, tenantId, stage, pageSize).ToList();

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
        BuildFilterSamplesQuery(db, tenantId, receiptId, keyword).ToList();

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
        BuildFilterRecordsQuery(db, tenantId, sampleId).ToList();

    public TestRecord? FindRecord(string tenantId, string id) =>
        db.TestRecords.FirstOrDefault(t => t.TenantId == tenantId && t.Id == id);

    public void SaveRecord(TestRecord t) => EfStoreOps.Upsert(db, db.TestRecords, t, x => x.Id == t.Id);

    public bool DeleteRecord(string tenantId, string id) =>
        db.TestRecords.Where(t => t.TenantId == tenantId && t.Id == id).ExecuteDelete() > 0;

    // === 查询构建器：internal 供翻译性测试（EfQueryTranslatabilityTest）逐个 ToQueryString ===

    internal static IQueryable<Contract> BuildFilterContractsQuery(
        LabDbContext db, string tenantId, string? keyword, ContractStatus? status) =>
        db.Contracts
            .Where(c => c.TenantId == tenantId)
            .Where(c => status == null || c.Status == status)
            .WhereKw(c => c.ContractCode, c => c.ProjectName, keyword)
            .OrderBy(c => c.CreatedAt);

    internal static IQueryable<SampleReceipt> BuildFilterReceiptsQuery(
        LabDbContext db, string tenantId, string? contractId, FlowStatus? flowStatus, string? keyword) =>
        db.SampleReceipts
            .Where(r => r.TenantId == tenantId)
            .Where(r => contractId == null || contractId == "" || r.ContractId == contractId)
            .Where(r => flowStatus == null || r.FlowStatus == flowStatus)
            .WhereKw(r => r.CommissionCode, r => r.ProjectName, keyword)
            .OrderBy(r => r.CreatedAt);

    internal static IQueryable<SampleReceipt> BuildSummaryQuery(
        LabDbContext db, string tenantId, string categoryCode, string dateFrom, string dateTo) =>
        db.SampleReceipts
            .Where(r => tenantId == "" || r.TenantId == tenantId)
            .Where(r => categoryCode == "ALL" || r.CategoryCode == categoryCode)
            .Where(r => dateFrom == "" || string.Compare(r.CommissionDate ?? "", dateFrom) >= 0)
            .Where(r => dateTo == "" || string.Compare(r.CommissionDate ?? "", dateTo) <= 0)
            .OrderByDescending(r => r.CommissionDate)
            .ThenBy(r => r.CommissionCode);

    internal static IQueryable<SampleReceipt> BuildFlowQueueQuery(
        LabDbContext db, string tenantId, FlowStatus stage, int pageSize) =>
        db.SampleReceipts
            .Where(r => r.TenantId == tenantId && r.FlowStatus == stage)
            .OrderBy(r => r.CreatedAt)
            .Take(Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200));

    internal static IQueryable<Sample> BuildFilterSamplesQuery(
        LabDbContext db, string tenantId, string? receiptId, string? keyword) =>
        db.Samples
            .Where(s => s.TenantId == tenantId)
            .Where(s => receiptId == null || receiptId == "" || s.ReceiptId == receiptId)
            .WhereKw(s => s.SampleCode, s => s.SampleName, keyword)
            .OrderByDescending(s => s.CreatedAt)
            .ThenBy(s => s.SampleCode);

    internal static IQueryable<TestRecord> BuildFilterRecordsQuery(
        LabDbContext db, string tenantId, string? sampleId) =>
        db.TestRecords
            .Where(t => t.TenantId == tenantId)
            .Where(t => sampleId == null || sampleId == "" || t.SampleId == sampleId)
            .OrderBy(t => t.CreatedAt);

}

/// <summary>
/// keyword 大小写不敏包含（空串不过滤），EF 可翻译形式。
/// v0.2.26 教训：普通 C# 方法/方法组在 Where lambda 里不可翻译（EF 不内联方法体），
/// 必须以表达式树组合。EF.Functions.ILike 直接写在 lambda 里由编译器生成正确调用树
/// （→ PG ILIKE，语义 = 内存版 ToLower().Contains；中文无大小写差异）。
/// </summary>
internal static class KwQueryExtensions
{
    /// <summary>Where(x => ILIKE(x.A) || ILIKE(x.B)) —— keyword 空则原样返回（不过滤）。</summary>
    public static IQueryable<T> WhereKw<T>(
        this IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, string?>> a,
        System.Linq.Expressions.Expression<Func<T, string?>> b,
        string? keyword)
    {
        if (keyword is null || keyword == "")
        {
            return query; // 空串不过滤（与内存版同语义）
        }

        var pattern = $"%{keyword}%";
        var param = System.Linq.Expressions.Expression.Parameter(typeof(T));
        var aBody = System.Linq.Expressions.Expression.Invoke(a, param);
        var bBody = System.Linq.Expressions.Expression.Invoke(b, param);

        // EF.Functions.ILike EF 翻译要求 DbFunctions 实例是 EF.Functions 常量 —— 用表达式引用它
        var efFunctions = System.Linq.Expressions.Expression.Property(
            null, typeof(EF), nameof(EF.Functions));

        // ILike(DbFunctions, string, string) 3 参重载，显式类型数组避免歧义
        var ilike = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            new[] { typeof(DbFunctions), typeof(string), typeof(string) })!;

        var likeA = System.Linq.Expressions.Expression.Call(ilike, efFunctions, aBody,
            System.Linq.Expressions.Expression.Constant(pattern));
        var likeB = System.Linq.Expressions.Expression.Call(ilike, efFunctions, bBody,
            System.Linq.Expressions.Expression.Constant(pattern));

        var lambda = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
            System.Linq.Expressions.Expression.OrElse(likeA, likeB),
            param);

        return query.Where(lambda);
    }
}
