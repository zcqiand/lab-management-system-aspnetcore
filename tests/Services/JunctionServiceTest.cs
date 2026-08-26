namespace Lab.AspNetCore.Tests.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Lab.AspNetCore.Services;
using Xunit;

/// <summary>
/// B6 八组 junction fnTest。语义基准：lab-springboot InspectionJunctionServiceTest：
/// link = upsert（重复不报错覆盖）；unlink miss → 404。
/// @Fn 模式：link 挂创建侧 I，unlink 挂删除侧 I；归属不同 F 时双标
/// （report-name-standard link = M06.F07.I07 + M06.F04.I07 等）。
/// </summary>
public class JunctionServiceTest
{
    private static JunctionService Svc() => new(new InMemoryJunctionStore());

    // === specialty-object（link F02.I05 / unlink F02.I06） ===

    [Fact]
    [Trait("Fn", "M06.F02.I05")]
    public void LinkSpecialtyObject_upsertNoError()
    {
        var svc = Svc();
        var link = new SpecialtyObjectLink { InspectionSpecialtyCode = "SP-1", InspectionObjectCode = "OBJ-1" };

        svc.LinkSpecialtyObject(link);
        svc.LinkSpecialtyObject(link); // 重复 link 不报错（upsert）
    }

    [Fact]
    [Trait("Fn", "M06.F02.I06")]
    public void UnlinkSpecialtyObject_missing404()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            Svc().UnlinkSpecialtyObject(new SpecialtyObjectLink
            {
                InspectionSpecialtyCode = "SP-GHOST",
                InspectionObjectCode = "OBJ-GHOST",
            }));
    }

    [Fact]
    [Trait("Fn", "M06.F02.I06")]
    public void UnlinkSpecialtyObject_afterLink_succeeds()
    {
        var svc = Svc();
        svc.LinkSpecialtyObject(new SpecialtyObjectLink { InspectionSpecialtyCode = "SP-1", InspectionObjectCode = "OBJ-1" });

        svc.UnlinkSpecialtyObject(new SpecialtyObjectLink { InspectionSpecialtyCode = "SP-1", InspectionObjectCode = "OBJ-1" });

        Assert.Throws<KeyNotFoundException>(() =>
            svc.UnlinkSpecialtyObject(new SpecialtyObjectLink
            {
                InspectionSpecialtyCode = "SP-1",
                InspectionObjectCode = "OBJ-1",
            })); // 已删再删 404
    }

    // === object-parameter（link F02.I07 / unlink F02.I08） ===

    [Fact]
    [Trait("Fn", "M06.F02.I07")]
    public void LinkObjectParameter_defaultsQualified()
    {
        var svc = Svc();
        var link = new ObjectParameterLink { InspectionObjectCode = "OBJ-1", InspectionParameterCode = "P-1" };

        svc.LinkObjectParameter(link);

        Assert.Equal(QualificationLevel.QUALIFIED, link.QualificationLevel); // 默认 QUALIFIED
    }

    [Fact]
    [Trait("Fn", "M06.F02.I08")]
    public void UnlinkObjectParameter_missing404() =>
        Assert.Throws<KeyNotFoundException>(() => Svc().UnlinkObjectParameter("OBJ-GHOST", "P-GHOST"));

    // === object-standard（role 必填 + 在 PK；link F01.I05 / unlink F01.I06） ===

    [Fact]
    [Trait("Fn", "M06.F01.I05")]
    public void LinkObjectStandard_samePairDifferentRoles_areTwoRows()
    {
        var svc = Svc();
        var testing = new ObjectStandardLink
        {
            InspectionObjectCode = "OBJ-1",
            InspectionStandardCode = "STD-1",
            Role = InspectionStandardRole.TESTING,
        };
        var judgment = new ObjectStandardLink
        {
            InspectionObjectCode = "OBJ-1",
            InspectionStandardCode = "STD-1",
            Role = InspectionStandardRole.JUDGMENT,
        };

        svc.LinkObjectStandard(testing);
        svc.LinkObjectStandard(judgment); // 同 code 对不同 role 是两行，不冲突

        // 删 TESTING 不影响 JUDGMENT
        svc.UnlinkObjectStandard("OBJ-1", "STD-1", InspectionStandardRole.TESTING);
        Assert.Throws<KeyNotFoundException>(() =>
            svc.UnlinkObjectStandard("OBJ-1", "STD-1", InspectionStandardRole.TESTING)); // 已删
        // JUDGMENT 仍在：删它成功
        svc.UnlinkObjectStandard("OBJ-1", "STD-1", InspectionStandardRole.JUDGMENT);
    }

    [Fact]
    [Trait("Fn", "M06.F01.I06")]
    public void UnlinkObjectStandard_missing404() =>
        Assert.Throws<KeyNotFoundException>(() =>
            Svc().UnlinkObjectStandard("OBJ-GHOST", "STD-GHOST", InspectionStandardRole.TESTING));

    // === standard-parameter（link F03.I05 / unlink F03.I06） ===

    [Fact]
    [Trait("Fn", "M06.F03.I05")]
    public void LinkStandardParameter_upsert()
    {
        var svc = Svc();
        var link = new StandardParameterLink { InspectionStandardCode = "STD-1", InspectionParameterCode = "P-1" };

        svc.LinkStandardParameter(link);
        svc.LinkStandardParameter(link); // 幂等
    }

    [Fact]
    [Trait("Fn", "M06.F03.I06")]
    public void UnlinkStandardParameter_missing404() =>
        Assert.Throws<KeyNotFoundException>(() =>
            Svc().UnlinkStandardParameter(new StandardParameterLink
            {
                InspectionStandardCode = "GHOST",
                InspectionParameterCode = "GHOST",
            }));

    // === report-name-object（link F07.I06 / unlink F04.I05） ===

    [Fact]
    [Trait("Fn", "M06.F07.I06")]
    public void LinkObjectReportName_upsert()
    {
        var svc = Svc();
        var link = new ObjectReportNameLink { InspectionObjectCode = "OBJ-1", ReportNameCode = "RN-1" };

        svc.LinkObjectReportName(link);
        svc.LinkObjectReportName(link);
    }

    [Fact]
    [Trait("Fn", "M06.F04.I05")]
    public void UnlinkObjectReportName_missing404() =>
        Assert.Throws<KeyNotFoundException>(() => Svc().UnlinkObjectReportName("OBJ-GHOST", "RN-GHOST"));

    // === report-name-standard（role 在 PK；link 双标 F07.I07+F04.I07 / unlink F04.I07） ===

    [Fact]
    [Trait("Fn", "M06.F07.I07")]
    [Trait("Fn", "M06.F04.I07")]
    public void LinkReportNameStandard_roleDefaultsToTesting()
    {
        var svc = Svc();

        // role 枚举无 null 态（TESTING=0 是默认值），C# 侧无法区分未传与显式 TESTING
        // —— 与 springboot（可选 role 校验）分叉：默认即 TESTING，直接落
        svc.LinkReportNameStandard(new ReportNameStandardLink
        {
            ReportNameCode = "RN-1",
            InspectionStandardCode = "STD-1",
        });
        svc.LinkReportNameStandard(new ReportNameStandardLink
        {
            ReportNameCode = "RN-1",
            InspectionStandardCode = "STD-1",
            Role = InspectionStandardRole.JUDGMENT,
        });
    }

    [Fact]
    [Trait("Fn", "M06.F04.I07")]
    public void UnlinkReportNameStandard_missing404() =>
        Assert.Throws<KeyNotFoundException>(() =>
            Svc().UnlinkReportNameStandard("RN-GHOST", "STD-GHOST", InspectionStandardRole.TESTING));

    // === report-name-parameter（link 双标 F07.I08+F03.I07 / unlink F04.I06） ===

    [Fact]
    [Trait("Fn", "M06.F07.I08")]
    [Trait("Fn", "M06.F03.I07")]
    public void LinkReportNameParameter_upsert()
    {
        var svc = Svc();
        var link = new ReportNameParameterLink { ReportNameCode = "RN-1", InspectionParameterCode = "P-1" };

        svc.LinkReportNameParameter(link);
        svc.LinkReportNameParameter(link);
    }

    [Fact]
    [Trait("Fn", "M06.F04.I06")]
    public void UnlinkReportNameParameter_missing404() =>
        Assert.Throws<KeyNotFoundException>(() => Svc().UnlinkReportNameParameter("RN-GHOST", "P-GHOST"));

    // === param-interface（link F08.I06 / unlink F03.I07） ===

    [Fact]
    [Trait("Fn", "M06.F08.I06")]
    public void LinkParamInterface_withRowLevelConfig()
    {
        var svc = Svc();

        svc.LinkParamInterface(new ParamInterfaceLink
        {
            InspectionParameterCode = "P-1",
            ParamInterfaceCode = "PI-1",
            Config = new Dictionary<string, object> { ["row"] = "level" }, // 行级 config（区别于 PI.config）
        });
    }

    [Fact]
    [Trait("Fn", "M06.F03.I07")]
    public void UnlinkParamInterface_missing404_thenSucceeds()
    {
        var svc = Svc();

        Assert.Throws<KeyNotFoundException>(() => svc.UnlinkParamInterface("P-GHOST", "PI-GHOST"));
        svc.LinkParamInterface(new ParamInterfaceLink { InspectionParameterCode = "P-1", ParamInterfaceCode = "PI-1" });
        svc.UnlinkParamInterface("P-1", "PI-1");
        Assert.Throws<KeyNotFoundException>(() => svc.UnlinkParamInterface("P-1", "PI-1")); // 已删 404
    }

    // === junction GET（Page<T> 契约补齐 — link 后按 query 过滤取回）===

    [Fact]
    [Trait("Fn", "M06.F07.I06")]
    public void ListObjectReportNameLinks_filterByObject()
    {
        var svc = Svc();
        svc.LinkObjectReportName(new ObjectReportNameLink { InspectionObjectCode = "OBJ-1", ReportNameCode = "RN-A" });
        svc.LinkObjectReportName(new ObjectReportNameLink { InspectionObjectCode = "OBJ-2", ReportNameCode = "RN-B" });

        var byObj = svc.ListObjectReportNameLinks("OBJ-1", null);
        Assert.Single(byObj);
        Assert.Equal("RN-A", byObj[0].ReportNameCode);

        var all = svc.ListObjectReportNameLinks(null, null);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    [Trait("Fn", "M06.F07.I07")]
    public void ListReportNameStandardLinks_filterByRole()
    {
        var svc = Svc();
        svc.LinkReportNameStandard(new ReportNameStandardLink
        {
            ReportNameCode = "RN-A",
            InspectionStandardCode = "STD-1",
            Role = InspectionStandardRole.TESTING,
        });
        svc.LinkReportNameStandard(new ReportNameStandardLink
        {
            ReportNameCode = "RN-A",
            InspectionStandardCode = "STD-2",
            Role = InspectionStandardRole.JUDGMENT,
        });

        var testingOnly = svc.ListReportNameStandardLinks("RN-A", InspectionStandardRole.TESTING);
        Assert.Single(testingOnly);
        Assert.Equal("STD-1", testingOnly[0].InspectionStandardCode);

        Assert.Equal(2, svc.ListReportNameStandardLinks(null, null).Count);
    }

    [Fact]
    [Trait("Fn", "M06.F07.I08")]
    public void ListReportNameParameterLinks_filterByReport()
    {
        var svc = Svc();
        svc.LinkReportNameParameter(new ReportNameParameterLink { ReportNameCode = "RN-A", InspectionParameterCode = "P-1" });
        svc.LinkReportNameParameter(new ReportNameParameterLink { ReportNameCode = "RN-B", InspectionParameterCode = "P-2" });

        var byReport = svc.ListReportNameParameterLinks("RN-A", null);
        Assert.Single(byReport);
        Assert.Equal("P-1", byReport[0].InspectionParameterCode);
    }

    [Fact]
    [Trait("Fn", "M06.F08.I06")]
    public void ListParamInterfaceLinks_filterByParam()
    {
        var svc = Svc();
        svc.LinkParamInterface(new ParamInterfaceLink { InspectionParameterCode = "P-1", ParamInterfaceCode = "PI-1" });
        svc.LinkParamInterface(new ParamInterfaceLink { InspectionParameterCode = "P-2", ParamInterfaceCode = "PI-2" });

        var byParam = svc.ListParamInterfaceLinks("P-1", null);
        Assert.Single(byParam);
        Assert.Equal("PI-1", byParam[0].ParamInterfaceCode);

        var empty = svc.ListParamInterfaceLinks("P-GHOST", null);
        Assert.Empty(empty);
    }
}
