namespace Lab.AspNetCore.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// B5 检测能力字典（专项/参数/标准/报告名称/参数界面 + objects）CRUD。
/// 语义镜像 springboot：平台级无 tenant；keyword 模糊 code/name；PATCH 语义；
/// 默认值 isOfficial/enabled=true、sourceType=OFFICIAL、aliases=[]、status=ACTIVE、config={}。
/// </summary>
public sealed class DictionaryService(IDictionaryStore store)
{
    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    // === M06.F01 专项 ===

    public IReadOnlyList<InspectionSpecialty> ListSpecialties(string? keyword) => store.FilterSpecialties(keyword);

    public InspectionSpecialty GetSpecialty(string code) =>
        store.FindSpecialty(code) ?? throw new KeyNotFoundException($"specialty {code} not found");

    public InspectionSpecialty CreateSpecialty(CreateInspectionSpecialtyRequest body)
    {
        var now = Now();
        var s = new InspectionSpecialty
        {
            Code = body.Code,
            OfficialNo = body.OfficialNo ?? "",
            Name = body.Name,
            IsOfficial = body.IsOfficial,
            Enabled = body.Enabled,
            SortOrder = body.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveSpecialty(s);
        return s;
    }

    public InspectionSpecialty UpdateSpecialty(string code, UpdateInspectionSpecialtyRequest body)
    {
        var s = GetSpecialty(code);
        if (body.OfficialNo is not null) s.OfficialNo = body.OfficialNo;
        if (body.Name is not null) s.Name = body.Name;
        if (body.SortOrder != 0) s.SortOrder = body.SortOrder;
        s.UpdatedAt = Now();
        store.SaveSpecialty(s);
        return s;
    }

    public void DeleteSpecialty(string code)
    {
        if (!store.DeleteSpecialty(code))
        {
            throw new KeyNotFoundException($"specialty {code} not found");
        }
    }

    // === M06.F03 参数 ===

    public IReadOnlyList<InspectionParameter> ListParameters(string? keyword, InspectionParameterSourceType? sourceType) =>
        store.FilterParameters(keyword, sourceType);

    public InspectionParameter GetParameter(string code) =>
        store.FindParameter(code) ?? throw new KeyNotFoundException($"parameter {code} not found");

    public InspectionParameter CreateParameter(CreateInspectionParameterRequest body)
    {
        var now = Now();
        var p = new InspectionParameter
        {
            Code = body.Code,
            Name = body.Name,
            RawName = body.RawName ?? "",
            CanonicalName = body.CanonicalName ?? "",
            MethodText = body.MethodText ?? "",
            Aliases = body.Aliases?.ToList() ?? new List<string>(),
            Unit = body.Unit ?? "",
            SourceType = body.SourceType == default ? InspectionParameterSourceType.Official : body.SourceType,
            SortOrder = body.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveParameter(p);
        return p;
    }

    public InspectionParameter UpdateParameter(string code, UpdateInspectionParameterRequest body)
    {
        var p = GetParameter(code);
        if (body.Name is not null) p.Name = body.Name;
        if (body.RawName is not null) p.RawName = body.RawName;
        if (body.CanonicalName is not null) p.CanonicalName = body.CanonicalName;
        if (body.MethodText is not null) p.MethodText = body.MethodText;
        if (body.Aliases is not null) p.Aliases = body.Aliases.ToList(); // 整体替换
        if (body.Unit is not null) p.Unit = body.Unit;
        if (body.SourceType != default) p.SourceType = body.SourceType;
        if (body.SortOrder != 0) p.SortOrder = body.SortOrder;
        p.UpdatedAt = Now();
        store.SaveParameter(p);
        return p;
    }

    public void DeleteParameter(string code)
    {
        if (!store.DeleteParameter(code))
        {
            throw new KeyNotFoundException($"parameter {code} not found");
        }
    }

    // === M06.F04 标准 ===

    public IReadOnlyList<InspectionStandard> ListStandards(string? keyword, InspectionStandardStatus? status) =>
        store.FilterStandards(keyword, status);

    public InspectionStandard GetStandard(string code) =>
        store.FindStandard(code) ?? throw new KeyNotFoundException($"standard {code} not found");

    public InspectionStandard CreateStandard(CreateInspectionStandardRequest body)
    {
        var now = Now();
        var s = new InspectionStandard
        {
            Code = body.Code,
            Name = body.Name,
            Version = body.Version ?? "",
            Status = body.Status == default ? InspectionStandardStatus.Active : body.Status,
            SourceDocumentId = body.SourceDocumentId ?? "",
            SourceHash = body.SourceHash ?? "",
            SortOrder = body.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveStandard(s);
        return s;
    }

    public InspectionStandard UpdateStandard(string code, UpdateInspectionStandardRequest body)
    {
        var s = GetStandard(code);
        if (body.Name is not null) s.Name = body.Name;
        if (body.Version is not null) s.Version = body.Version;
        if (body.Status != default) s.Status = body.Status;
        if (body.SourceDocumentId is not null) s.SourceDocumentId = body.SourceDocumentId;
        if (body.SourceHash is not null) s.SourceHash = body.SourceHash;
        if (body.SortOrder != 0) s.SortOrder = body.SortOrder;
        s.UpdatedAt = Now();
        store.SaveStandard(s);
        return s;
    }

    public void DeleteStandard(string code)
    {
        if (!store.DeleteStandard(code))
        {
            throw new KeyNotFoundException($"standard {code} not found");
        }
    }

    // === M06.F07 报告名称 ===

    public IReadOnlyList<InspectionReportName> ListReportNames(string? keyword) => store.FilterReportNames(keyword);

    public InspectionReportName GetReportName(string code) =>
        store.FindReportName(code) ?? throw new KeyNotFoundException($"report-name {code} not found");

    public InspectionReportName CreateReportName(CreateInspectionReportNameRequest body)
    {
        var now = Now();
        var r = new InspectionReportName
        {
            Code = body.Code,
            Name = body.Name,
            FullName = body.FullName ?? "",
            TemplatePath = body.TemplatePath ?? "",
            SummaryName = body.SummaryName ?? "",
            ExtFields = body.ExtFields?.ToList() ?? new List<ExtFieldDef>(),
            Description = body.Description ?? "",
            SortOrder = body.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveReportName(r);
        return r;
    }

    public InspectionReportName UpdateReportName(string code, UpdateInspectionReportNameRequest body)
    {
        var r = GetReportName(code);
        if (body.Name is not null) r.Name = body.Name;
        if (body.FullName is not null) r.FullName = body.FullName;
        if (body.TemplatePath is not null) r.TemplatePath = body.TemplatePath;
        if (body.SummaryName is not null) r.SummaryName = body.SummaryName;
        if (body.ExtFields is not null) r.ExtFields = body.ExtFields.ToList();
        if (body.Description is not null) r.Description = body.Description;
        if (body.SortOrder != 0) r.SortOrder = body.SortOrder;
        r.UpdatedAt = Now();
        store.SaveReportName(r);
        return r;
    }

    public void DeleteReportName(string code)
    {
        if (!store.DeleteReportName(code))
        {
            throw new KeyNotFoundException($"report-name {code} not found");
        }
    }

    // === M06.F08 参数界面 ===

    public IReadOnlyList<ParamInterface> ListInterfaces(string? keyword) => store.FilterInterfaces(keyword);

    public ParamInterface GetInterface(string code) =>
        store.FindInterface(code) ?? throw new KeyNotFoundException($"param-interface {code} not found");

    public ParamInterface CreateInterface(CreateParamInterfaceRequest body)
    {
        var now = Now();
        var i = new ParamInterface
        {
            Code = body.Code,
            Name = body.Name ?? "",
            ComponentPath = body.ComponentPath,
            Description = body.Description ?? "",
            IsOfficial = body.IsOfficial,
            SortOrder = body.SortOrder,
            Config = body.Config is null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(body.Config), // jsonb 默认 {}
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveInterface(i);
        return i;
    }

    public ParamInterface UpdateInterface(string code, UpdateParamInterfaceRequest body)
    {
        var i = GetInterface(code);
        if (body.Name is not null) i.Name = body.Name;
        if (body.ComponentPath is not null) i.ComponentPath = body.ComponentPath;
        if (body.Description is not null) i.Description = body.Description;
        if (body.Config is not null) i.Config = new Dictionary<string, object>(body.Config);
        if (body.SortOrder != 0) i.SortOrder = body.SortOrder;
        i.UpdatedAt = Now();
        store.SaveInterface(i);
        return i;
    }

    public void DeleteInterface(string code)
    {
        if (!store.DeleteInterface(code))
        {
            throw new KeyNotFoundException($"param-interface {code} not found");
        }
    }

    // === M06.F02 objects ===

    public IReadOnlyList<InspectionObject> ListObjects(string? specialtyCode, string? keyword) =>
        store.FilterObjects(specialtyCode, keyword);

    public InspectionObject GetObject(string code) =>
        store.FindObject(code) ?? throw new KeyNotFoundException($"object {code} not found");

    public InspectionObject CreateObject(CreateInspectionObjectRequest body)
    {
        if (!store.SpecialtyExists(body.InspectionSpecialtyCode))
        {
            throw new KeyNotFoundException($"specialty {body.InspectionSpecialtyCode} not found"); // FK RESTRICT
        }

        var now = Now();
        var o = new InspectionObject
        {
            Code = body.Code,
            InspectionSpecialtyCode = body.InspectionSpecialtyCode,
            SourceProjectNo = body.SourceProjectNo,
            SourceProjectName = body.SourceProjectName,
            Name = body.Name,
            IsOptionalForQualification = body.IsOptionalForQualification,
            IsOfficial = body.IsOfficial,
            Enabled = body.Enabled,
            SortOrder = body.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.SaveObject(o);
        return o;
    }

    public InspectionObject UpdateObject(string code, UpdateInspectionObjectRequest body)
    {
        var o = GetObject(code);
        if (body.InspectionSpecialtyCode is not null) o.InspectionSpecialtyCode = body.InspectionSpecialtyCode;
        if (body.SourceProjectNo is not null) o.SourceProjectNo = body.SourceProjectNo;
        if (body.SourceProjectName is not null) o.SourceProjectName = body.SourceProjectName;
        if (body.Name is not null) o.Name = body.Name;
        if (body.SortOrder != 0) o.SortOrder = body.SortOrder;
        o.UpdatedAt = Now();
        store.SaveObject(o);
        return o;
    }

    public void DeleteObject(string code)
    {
        if (!store.DeleteObject(code))
        {
            throw new KeyNotFoundException($"object {code} not found");
        }
    }
}
