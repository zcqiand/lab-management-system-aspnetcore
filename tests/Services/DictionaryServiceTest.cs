namespace Lab.AspNetCore.Tests.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Lab.AspNetCore.Services;
using Xunit;

/// <summary>
/// B5 字典 + B6 objects fnTest。语义基准：lab-springboot InspectionDictionaryServiceTest
/// （默认值 / keyword 过滤 / PATCH / 404 / FK RESTRICT）。
/// </summary>
public class DictionaryServiceTest
{
    private static DictionaryService Svc(InMemoryDictionaryStore? store = null) => new(store ?? new InMemoryDictionaryStore());

    // === M06.F01 专项 ===

    [Fact]
    [Trait("Fn", "M06.F01.I01")]
    public void ListSpecialties_keywordFilters()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateSpecialty(new CreateInspectionSpecialtyRequest { Code = "SP-concrete", OfficialNo = "1", Name = "混凝土" });
        svc.CreateSpecialty(new CreateInspectionSpecialtyRequest { Code = "SP-steel", OfficialNo = "2", Name = "钢材" });

        Assert.Equal(2, svc.ListSpecialties(null).Count);
        Assert.Single(svc.ListSpecialties("steel"));
        Assert.Single(svc.ListSpecialties("混凝"));
    }

    [Fact]
    [Trait("Fn", "M06.F01.I02")]
    public void GetSpecialty_missing404() =>
        Assert.Throws<KeyNotFoundException>(() => Svc().GetSpecialty("GHOST"));

    [Fact]
    [Trait("Fn", "M06.F01.I03")]
    public void CreateSpecialty_defaults()
    {
        var s = Svc().CreateSpecialty(new CreateInspectionSpecialtyRequest { Code = "SP-1", OfficialNo = "1", Name = "专项一" });

        Assert.Equal("SP-1", s.Code);
        Assert.Equal(s.CreatedAt, s.UpdatedAt);
    }

    [Fact]
    [Trait("Fn", "M06.F01.I04")]
    public void UpdateSpecialty_patchKeepsUnset()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateSpecialty(new CreateInspectionSpecialtyRequest { Code = "SP-1", OfficialNo = "1", Name = "旧名" });

        var s = svc.UpdateSpecialty("SP-1", new UpdateInspectionSpecialtyRequest { Name = "新名" });

        Assert.Equal("新名", s.Name);
        Assert.Equal("1", s.OfficialNo); // 未传保留
    }

    [Fact]
    [Trait("Fn", "M06.F01.I04")]
    public void DeleteSpecialty_missing404() =>
        Assert.Throws<KeyNotFoundException>(() => Svc().DeleteSpecialty("GHOST"));

    // === M06.F03 参数 ===

    [Fact]
    [Trait("Fn", "M06.F03.I01")]
    public void ListParameters_keywordAndSourceTypeFilters()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateParameter(new CreateInspectionParameterRequest
        {
            Code = "P-1",
            Name = "抗压强度",
            RawName = "r",
            CanonicalName = "c",
            SourceType = InspectionParameterSourceType.Official,
        });
        svc.CreateParameter(new CreateInspectionParameterRequest
        {
            Code = "P-2",
            Name = "抗折强度",
            RawName = "r",
            CanonicalName = "c",
            SourceType = InspectionParameterSourceType.Custom,
        });

        Assert.Equal(2, svc.ListParameters(null, null).Count);
        Assert.Single(svc.ListParameters("抗压", null));
        Assert.Single(svc.ListParameters(null, InspectionParameterSourceType.Custom));
    }

    [Fact]
    [Trait("Fn", "M06.F03.I02")]
    public void GetParameter_missing404() =>
        Assert.Throws<KeyNotFoundException>(() => Svc().GetParameter("GHOST"));

    [Fact]
    [Trait("Fn", "M06.F03.I03")]
    public void CreateParameter_defaultsOfficialAndEmptyAliases()
    {
        var p = Svc().CreateParameter(new CreateInspectionParameterRequest
        {
            Code = "P-1",
            Name = "抗压",
            RawName = "r",
            CanonicalName = "c",
        });

        Assert.Empty(p.Aliases); // 默认 []
    }

    [Fact]
    [Trait("Fn", "M06.F03.I04")]
    public void UpdateParameter_aliasesReplaceWholeList()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateParameter(new CreateInspectionParameterRequest
        {
            Code = "P-1",
            Name = "抗压",
            RawName = "r",
            CanonicalName = "c",
            Aliases = new List<string> { "旧别名" },
        });

        var p = svc.UpdateParameter("P-1", new UpdateInspectionParameterRequest
        {
            Aliases = new List<string> { "别名A", "别名B" },
        });

        Assert.Equal(2, p.Aliases.Count); // 整体替换
        Assert.Equal("抗压", p.Name); // 未传保留
    }

    [Fact]
    [Trait("Fn", "M06.F03.I04")]
    public void DeleteParameter_removes()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateParameter(new CreateInspectionParameterRequest { Code = "P-1", Name = "n", RawName = "r", CanonicalName = "c" });

        svc.DeleteParameter("P-1");
        Assert.Empty(svc.ListParameters(null, null));
    }

    // === M06.F04 标准 ===

    [Fact]
    [Trait("Fn", "M06.F04.I01")]
    public void ListStandards_keywordAndStatusFilters()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateStandard(new CreateInspectionStandardRequest { Code = "STD-1", Name = "GB50081" });
        svc.CreateStandard(new CreateInspectionStandardRequest
        {
            Code = "STD-2",
            Name = "GB50082",
            Status = InspectionStandardStatus.Superseded,
        });

        Assert.Equal(2, svc.ListStandards(null, null).Count);
        Assert.Single(svc.ListStandards("50081", null));
        Assert.Single(svc.ListStandards(null, InspectionStandardStatus.Superseded));
    }

    [Fact]
    [Trait("Fn", "M06.F04.I02")]
    public void GetStandard_missing404() =>
        Assert.Throws<KeyNotFoundException>(() => Svc().GetStandard("GHOST"));

    [Fact]
    [Trait("Fn", "M06.F04.I03")]
    public void CreateStandard_defaultsActive()
    {
        var s = Svc().CreateStandard(new CreateInspectionStandardRequest { Code = "STD-1", Name = "新标准" });

        Assert.Equal(InspectionStandardStatus.Active, s.Status);
    }

    [Fact]
    [Trait("Fn", "M06.F04.I04")]
    public void UpdateStandard_statusTransition()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateStandard(new CreateInspectionStandardRequest { Code = "STD-1", Name = "标准" });

        var s = svc.UpdateStandard("STD-1", new UpdateInspectionStandardRequest
        {
            Status = InspectionStandardStatus.Superseded,
        });

        Assert.Equal(InspectionStandardStatus.Superseded, s.Status);
        Assert.Equal("标准", s.Name);
    }

    [Fact]
    [Trait("Fn", "M06.F04.I04")]
    public void DeleteStandard_missing404() =>
        Assert.Throws<KeyNotFoundException>(() => Svc().DeleteStandard("GHOST"));

    // === M06.F07 报告名称 ===

    [Fact]
    [Trait("Fn", "M06.F07.I01")]
    public void ListReportNames_keywordFilters()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateReportName(new CreateInspectionReportNameRequest { Code = "RN-1", Name = "混凝土抗压报告" });
        svc.CreateReportName(new CreateInspectionReportNameRequest { Code = "RN-2", Name = "钢材拉伸报告" });

        Assert.Single(svc.ListReportNames("钢材"));
    }

    [Fact]
    [Trait("Fn", "M06.F07.I02")]
    public void GetReportName_includesExtFields()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateReportName(new CreateInspectionReportNameRequest
        {
            Code = "RN-1",
            Name = "报告",
            ExtFields = new List<ExtFieldDef>
            {
                new() { Key = "w", Label = "水灰比", Type = ExtFieldDefType.Number },
            },
        });

        var r = svc.GetReportName("RN-1");

        Assert.Single(r.ExtFields);
        Assert.Equal("水灰比", r.ExtFields[0].Label);
    }

    [Fact]
    [Trait("Fn", "M06.F07.I03")]
    public void CreateReportName_extFieldsDefaultEmpty()
    {
        var r = Svc().CreateReportName(new CreateInspectionReportNameRequest { Code = "RN-1", Name = "报告" });

        Assert.Empty(r.ExtFields);
    }

    [Fact]
    [Trait("Fn", "M06.F07.I04")]
    public void UpdateReportName_patchSemantics()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateReportName(new CreateInspectionReportNameRequest { Code = "RN-1", Name = "旧" });

        var r = svc.UpdateReportName("RN-1", new UpdateInspectionReportNameRequest { FullName = "全名" });

        Assert.Equal("旧", r.Name);
        Assert.Equal("全名", r.FullName);
    }

    [Fact]
    [Trait("Fn", "M06.F07.I05")]
    public void DeleteReportName_removes()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateReportName(new CreateInspectionReportNameRequest { Code = "RN-1", Name = "n" });

        svc.DeleteReportName("RN-1");
        Assert.Empty(svc.ListReportNames(null));
    }

    // === M06.F08 参数界面 ===

    [Fact]
    [Trait("Fn", "M06.F08.I01")]
    public void ListInterfaces_keywordFilters()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateInterface(new CreateParamInterfaceRequest { Code = "PI-1", ComponentPath = "/components/a" });
        svc.CreateInterface(new CreateParamInterfaceRequest { Code = "PI-2", ComponentPath = "/components/b" });

        Assert.Single(svc.ListInterfaces("PI-2"));
    }

    [Fact]
    [Trait("Fn", "M06.F08.I02")]
    public void GetInterface_configMap()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateInterface(new CreateParamInterfaceRequest
        {
            Code = "PI-1",
            ComponentPath = "/c",
            Config = new Dictionary<string, object> { ["precision"] = 2 },
        });

        var i = svc.GetInterface("PI-1");

        Assert.Equal(2, i.Config.Count == 0 ? 0 : Convert.ToInt32(i.Config["precision"]));
    }

    [Fact]
    [Trait("Fn", "M06.F08.I03")]
    public void CreateInterface_configDefaultsEmpty()
    {
        var i = Svc().CreateInterface(new CreateParamInterfaceRequest { Code = "PI-1", ComponentPath = "/c" });

        Assert.Empty(i.Config); // jsonb 默认 {}
    }

    [Fact]
    [Trait("Fn", "M06.F08.I04")]
    public void UpdateInterface_patchSemantics()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateInterface(new CreateParamInterfaceRequest { Code = "PI-1", ComponentPath = "/old" });

        var i = svc.UpdateInterface("PI-1", new UpdateParamInterfaceRequest { Name = "界面名" });

        Assert.Equal("/old", i.ComponentPath); // 未传保留
        Assert.Equal("界面名", i.Name);
    }

    [Fact]
    [Trait("Fn", "M06.F08.I05")]
    public void DeleteInterface_missing404() =>
        Assert.Throws<KeyNotFoundException>(() => Svc().DeleteInterface("GHOST"));

    // === M06.F02 objects ===

    [Fact]
    [Trait("Fn", "M06.F02.I01")]
    public void ListObjects_specialtyFilter()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateSpecialty(new CreateInspectionSpecialtyRequest { Code = "SP-1", OfficialNo = "1", Name = "专项" });
        svc.CreateObject(new CreateInspectionObjectRequest
        {
            Code = "OBJ-1",
            InspectionSpecialtyCode = "SP-1",
            SourceProjectNo = "P1",
            SourceProjectName = "p1",
            Name = "对象一",
        });
        svc.CreateObject(new CreateInspectionObjectRequest
        {
            Code = "OBJ-2",
            InspectionSpecialtyCode = "SP-1",
            SourceProjectNo = "P2",
            SourceProjectName = "p2",
            Name = "对象二",
        });

        Assert.Equal(2, svc.ListObjects("SP-1", null).Count);
        Assert.Single(svc.ListObjects(null, "OBJ-2"));
        Assert.Empty(svc.ListObjects("SP-GHOST", null));
    }

    [Fact]
    [Trait("Fn", "M06.F02.I02")]
    public void GetObject_missing404() =>
        Assert.Throws<KeyNotFoundException>(() => Svc().GetObject("GHOST"));

    [Fact]
    [Trait("Fn", "M06.F02.I03")]
    public void CreateObject_missingSpecialty_throwsRestrict() =>
        Assert.Throws<KeyNotFoundException>(() => Svc().CreateObject(new CreateInspectionObjectRequest
        {
            Code = "OBJ-1",
            InspectionSpecialtyCode = "SP-GHOST",
            SourceProjectNo = "P",
            SourceProjectName = "p",
            Name = "n",
        }));

    [Fact]
    [Trait("Fn", "M06.F02.I03")]
    public void CreateObject_mapsFields()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateSpecialty(new CreateInspectionSpecialtyRequest { Code = "SP-1", OfficialNo = "1", Name = "专项" });

        var o = svc.CreateObject(new CreateInspectionObjectRequest
        {
            Code = "OBJ-1",
            InspectionSpecialtyCode = "SP-1",
            SourceProjectNo = "P1",
            SourceProjectName = "pn",
            Name = "对象",
        });

        Assert.Equal("P1", o.SourceProjectNo);
        Assert.Equal("SP-1", o.InspectionSpecialtyCode);
    }

    [Fact]
    [Trait("Fn", "M06.F02.I04")]
    public void UpdateObject_patchSemantics()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateSpecialty(new CreateInspectionSpecialtyRequest { Code = "SP-1", OfficialNo = "1", Name = "专项" });
        svc.CreateObject(new CreateInspectionObjectRequest
        {
            Code = "OBJ-1",
            InspectionSpecialtyCode = "SP-1",
            SourceProjectNo = "P1",
            SourceProjectName = "p",
            Name = "旧名",
        });

        var o = svc.UpdateObject("OBJ-1", new UpdateInspectionObjectRequest { Name = "新名" });

        Assert.Equal("新名", o.Name);
        Assert.Equal("P1", o.SourceProjectNo);
    }

    [Fact]
    [Trait("Fn", "M06.F02.I04")]
    public void DeleteObject_removes()
    {
        var store = new InMemoryDictionaryStore();
        var svc = Svc(store);
        svc.CreateSpecialty(new CreateInspectionSpecialtyRequest { Code = "SP-1", OfficialNo = "1", Name = "专项" });
        svc.CreateObject(new CreateInspectionObjectRequest
        {
            Code = "OBJ-1",
            InspectionSpecialtyCode = "SP-1",
            SourceProjectNo = "P",
            SourceProjectName = "p",
            Name = "n",
        });

        svc.DeleteObject("OBJ-1");
        Assert.Empty(svc.ListObjects(null, null));
    }
}
