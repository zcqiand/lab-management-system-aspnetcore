namespace Lab.AspNetCore.Data;

using Lab.AspNetCore.Controllers.Generated;

// EF Core 换装（lab_dev 共库）抽的仓储契约：签名 = InMemory*Store 公共面原样上提，
// service/controller/fnTest 一行不动。两个实现：
//   InMemory*Store -- 测试与默认 dev（无 DB，语义快照）
//   Persistence/Ef*Store -- EF Core + Npgsql，镜像 shared SQL（真实 FK/唯一约束生效）
// 注意：BrandDeleted 事件 / OnBrandDeleted（V011 SET NULL 模拟）刻意不进接口 --
// EF 模式下该语义由 DB 的 ON DELETE SET NULL 原生承担。

public interface ICatalogStore
{
    IReadOnlyList<InspectionModel> FilterModels(string tenantId, string? objectCode, string? keyword);
    InspectionModel? FindModel(string tenantId, string code);
    void SaveModel(InspectionModel m);
    bool DeleteModel(string tenantId, string code);

    IReadOnlyList<InspectionSpec> FilterSpecs(string tenantId, string? objectCode, string? keyword);
    InspectionSpec? FindSpec(string tenantId, string code);
    void SaveSpec(InspectionSpec s);
    bool DeleteSpec(string tenantId, string code);

    IReadOnlyList<InspectionGrade> FilterGrades(string tenantId, string? objectCode, string? keyword);
    InspectionGrade? FindGrade(string tenantId, string code);
    void SaveGrade(InspectionGrade g);
    bool DeleteGrade(string tenantId, string code);

    IReadOnlyList<InspectionBrand> FilterBrands(string tenantId, string? objectCode, string? keyword);
    InspectionBrand? FindBrand(string tenantId, string code);
    void SaveBrand(InspectionBrand b);
    bool DeleteBrand(string tenantId, string code);
}

public interface IMethodStore
{
    IReadOnlyList<CalculationMethod> Filter(string? objectCode, string? parameterCode);
    CalculationMethod? Find(string objectCode, string parameterCode);
    void Save(CalculationMethod r);
    bool Delete(string objectCode, string parameterCode);
}

public interface IRequirementStore
{
    IReadOnlyList<TechnicalRequirement> Filter(
        string tenantId, string? objectCode, string? parameterCode, string? standardCode,
        RequirementVerificationStatus? status);
    TechnicalRequirement? Find(string tenantId, string objectCode, string parameterCode, string standardCode);
    void Save(TechnicalRequirement t);
    bool Delete(string tenantId, string objectCode, string parameterCode, string standardCode);
}

public interface IFlowStore
{
    // 合同 M02.F01
    IReadOnlyList<Contract> FilterContracts(string tenantId, string? keyword, ContractStatus? status);
    Contract? FindContract(string tenantId, string id);
    bool ContractReferenced(string contractId);
    void SaveContract(Contract c);
    bool DeleteContract(string tenantId, string id);

    // 接样 M03.F01（含 B4 summary）
    IReadOnlyList<SampleReceipt> FilterReceipts(string tenantId, string? contractId, FlowStatus? flowStatus, string? keyword);
    IReadOnlyList<SampleReceipt> Summary(string tenantId, string categoryCode, string dateFrom, string dateTo);
    IReadOnlyList<SampleReceipt> FlowQueue(string tenantId, FlowStatus stage, int pageSize);
    SampleReceipt? FindReceipt(string tenantId, string id);
    SampleReceipt? FindReceiptAnyTenant(string id);
    void SaveReceipt(SampleReceipt r);
    bool DeleteReceipt(string tenantId, string id, out int cascadedSamples);

    // 样品 M03.F03
    IReadOnlyList<Sample> FilterSamples(string tenantId, string? receiptId, string? keyword);
    Sample? FindSample(string tenantId, string id);
    bool ReceiptExists(string receiptId);
    bool ContractExists(string contractId);
    void SaveSample(Sample s);
    bool DeleteSample(string tenantId, string id);
    int CountSamples(string tenantId);
    int CountContracts(string tenantId);

    // 检测记录 M03.F03.I06-I11
    IReadOnlyList<TestRecord> FilterRecords(string tenantId, string? sampleId);
    TestRecord? FindRecord(string tenantId, string id);
    void SaveRecord(TestRecord t);
    bool DeleteRecord(string tenantId, string id);
}

public interface IDictionaryStore
{
    IReadOnlyList<InspectionSpecialty> FilterSpecialties(string? keyword);
    InspectionSpecialty? FindSpecialty(string code);
    void SaveSpecialty(InspectionSpecialty s);
    bool DeleteSpecialty(string code);
    bool SpecialtyExists(string code);

    IReadOnlyList<InspectionParameter> FilterParameters(string? keyword, InspectionParameterSourceType? sourceType);
    InspectionParameter? FindParameter(string code);
    void SaveParameter(InspectionParameter p);
    bool DeleteParameter(string code);

    IReadOnlyList<InspectionStandard> FilterStandards(string? keyword, InspectionStandardStatus? status);
    InspectionStandard? FindStandard(string code);
    void SaveStandard(InspectionStandard s);
    bool DeleteStandard(string code);

    IReadOnlyList<InspectionReportName> FilterReportNames(string? keyword);
    InspectionReportName? FindReportName(string code);
    void SaveReportName(InspectionReportName r);
    bool DeleteReportName(string code);

    IReadOnlyList<ParamInterface> FilterInterfaces(string? keyword);
    ParamInterface? FindInterface(string code);
    void SaveInterface(ParamInterface i);
    bool DeleteInterface(string code);

    IReadOnlyList<InspectionObject> FilterObjects(string? specialtyCode, string? keyword);
    InspectionObject? FindObject(string code);
    void SaveObject(InspectionObject o);
    bool DeleteObject(string code);
}

public interface IJunctionStore
{
    void SaveSpecialtyObject(SpecialtyObjectLink l);
    bool DeleteSpecialtyObject(string spec, string obj);
    IReadOnlyList<SpecialtyObjectLink> ListSpecialtyObject(string? spec);

    void SaveObjectParameter(ObjectParameterLink l);
    bool DeleteObjectParameter(string obj, string param);
    IReadOnlyList<ObjectParameterLink> ListObjectParameter(string? obj, string? param);

    void SaveObjectStandard(ObjectStandardLink l);
    bool DeleteObjectStandard(string obj, string std, string role);
    IReadOnlyList<ObjectStandardLink> ListObjectStandard(string? obj, InspectionStandardRole? role);

    void SaveStandardParameter(StandardParameterLink l);
    bool DeleteStandardParameter(string std, string param);
    IReadOnlyList<StandardParameterLink> ListStandardParameter(string? std, string? param);

    void SaveObjectReportName(ObjectReportNameLink l);
    bool DeleteObjectReportName(string obj, string report);

    void SaveReportNameStandard(ReportNameStandardLink l);
    bool DeleteReportNameStandard(string report, string std, string role);

    void SaveReportNameParameter(ReportNameParameterLink l);
    bool DeleteReportNameParameter(string report, string param);

    void SaveParamInterface(ParamInterfaceLink l);
    bool DeleteParamInterface(string param, string iface);
}
