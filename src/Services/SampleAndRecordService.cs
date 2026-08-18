namespace Lab.AspNetCore.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// M03.F03.I01-I05 样品 CRUD（B3）。语义镜像 springboot SampleService：
/// list receiptId 精确 + keyword 模糊 sampleCode/sampleName，createdAt DESC；
/// create 校验 receipt FK；ext 默认 {}；id = "S-"+UUID。
/// </summary>
public sealed class SampleService(InMemoryFlowStore store)
{
    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public IReadOnlyList<Sample> List(string tenantId, string? receiptId, string? keyword) =>
        store.FilterSamples(tenantId, receiptId, keyword);

    public Sample Get(string tenantId, string id) =>
        store.FindSample(tenantId, id) ?? throw new KeyNotFoundException($"sample {id} not found");

    public Sample Create(string tenantId, CreateSampleRequest body)
    {
        if (!store.ReceiptExists(body.ReceiptId))
        {
            throw new KeyNotFoundException($"receipt {body.ReceiptId} not found"); // FK 校验
        }

        var now = Now();
        var s = new Sample
        {
            Id = "S-" + Guid.NewGuid().ToString("N")[..12],
            TenantId = tenantId,
            ReceiptId = body.ReceiptId,
            SampleCode = body.SampleCode ?? "",
            SampleName = body.SampleName ?? "",
            Model = body.Model ?? "",
            Specification = body.Specification ?? "",
            Grade = body.Grade ?? "",
            Brand = body.Brand ?? "",
            Manufacturer = body.Manufacturer ?? "",
            StructuralPart = body.StructuralPart ?? "",
            RepresentQuantity = body.RepresentQuantity ?? "",
            SampleQuantity = body.SampleQuantity ?? "",
            BatchNumber = body.BatchNumber ?? "",
            SupplyUnit = body.SupplyUnit ?? "",
            ArrivalDate = body.ArrivalDate ?? "",
            SamplingDate = body.SamplingDate ?? "",
            CuringCondition = body.CuringCondition ?? "",
            Age = body.Age ?? "",
            Ext = body.Ext is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(body.Ext), // jsonb 默认 {}
            Remark = body.Remark ?? "",
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveSample(s);
        return s;
    }

    public Sample Update(string tenantId, string id, UpdateSampleRequest body)
    {
        var s = Get(tenantId, id);
        if (body.ReceiptId is not null) s.ReceiptId = body.ReceiptId;
        if (body.SampleCode is not null) s.SampleCode = body.SampleCode;
        if (body.SampleName is not null) s.SampleName = body.SampleName;
        if (body.Model is not null) s.Model = body.Model;
        if (body.Specification is not null) s.Specification = body.Specification;
        if (body.Grade is not null) s.Grade = body.Grade;
        if (body.Brand is not null) s.Brand = body.Brand;
        if (body.Manufacturer is not null) s.Manufacturer = body.Manufacturer;
        if (body.StructuralPart is not null) s.StructuralPart = body.StructuralPart;
        if (body.RepresentQuantity is not null) s.RepresentQuantity = body.RepresentQuantity;
        if (body.SampleQuantity is not null) s.SampleQuantity = body.SampleQuantity;
        if (body.BatchNumber is not null) s.BatchNumber = body.BatchNumber;
        if (body.SupplyUnit is not null) s.SupplyUnit = body.SupplyUnit;
        if (body.ArrivalDate is not null) s.ArrivalDate = body.ArrivalDate;
        if (body.SamplingDate is not null) s.SamplingDate = body.SamplingDate;
        if (body.CuringCondition is not null) s.CuringCondition = body.CuringCondition;
        if (body.Age is not null) s.Age = body.Age;
        if (body.Ext is not null) s.Ext = new Dictionary<string, string>(body.Ext);
        if (body.Remark is not null) s.Remark = body.Remark;
        s.UpdatedAt = Now();
        store.SaveSample(s);
        return s;
    }

    public void Delete(string tenantId, string id)
    {
        Get(tenantId, id);
        store.DeleteSample(tenantId, id);
    }
}

/// <summary>
/// M03.F03.I06-I11 检测记录 CRUD + 改判（B3）。语义镜像 springboot TestRecordService：
/// list 只按 tenant+sampleId 过滤（parameterCode 接收未用，分页回显）；verdict 直接覆写。
/// </summary>
public sealed class TestRecordService(InMemoryFlowStore store)
{
    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public IReadOnlyList<TestRecord> List(string tenantId, string? sampleId) =>
        store.FilterRecords(tenantId, sampleId);

    public TestRecord Get(string tenantId, string id) =>
        store.FindRecord(tenantId, id) ?? throw new KeyNotFoundException($"test-record {id} not found");

    public TestRecord Create(string tenantId, CreateTestRecordRequest body)
    {
        var now = Now();
        var t = new TestRecord
        {
            Id = "T-" + Guid.NewGuid().ToString("N")[..12],
            TenantId = tenantId,
            SampleId = body.SampleId,
            ParameterCode = body.ParameterCode,
            StandardCode = body.StandardCode ?? "",
            RequirementCode = body.RequirementCode ?? "",
            Requirement = body.Requirement,
            Result = body.Result,
            Verdict = body.Verdict ?? "",
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveRecord(t);
        return t;
    }

    public TestRecord Update(string tenantId, string id, UpdateTestRecordRequest body)
    {
        var t = Get(tenantId, id);
        if (body.SampleId is not null) t.SampleId = body.SampleId;
        if (body.ParameterCode is not null) t.ParameterCode = body.ParameterCode;
        if (body.StandardCode is not null) t.StandardCode = body.StandardCode;
        if (body.RequirementCode is not null) t.RequirementCode = body.RequirementCode;
        if (body.Requirement is not null) t.Requirement = body.Requirement;
        if (body.Result is not null) t.Result = body.Result;
        if (body.Verdict is not null) t.Verdict = body.Verdict;
        t.UpdatedAt = Now();
        store.SaveRecord(t);
        return t;
    }

    public void Delete(string tenantId, string id)
    {
        Get(tenantId, id);
        store.DeleteRecord(tenantId, id);
    }

    public TestRecord SetVerdict(string tenantId, string id, string verdict)
    {
        var t = Get(tenantId, id);
        t.Verdict = verdict;
        t.UpdatedAt = Now();
        store.SaveRecord(t);
        return t;
    }
}
