namespace Lab.AspNetCore.Tests.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;
using Lab.AspNetCore.Services;
using Xunit;

/// <summary>
/// M05.F01 报告汇总 + M05.F02 仪表盘 fnTest（B4）。
/// 语义基准：lab-springboot SummaryServiceTest（ALL 哨兵 / 闭区间 / 6 列 / null→"" / 3 桶聚合）。
/// </summary>
public class SummaryServiceTest
{
    private const string Tenant = "TENANT-001";

    private static SampleReceipt Receipt(
        string id, string category, string date, FlowStatus status,
        string code = "C-1", string project = "工程一", string? reportCode = "R-1", ReceiptResult result = ReceiptResult.Pass) => new()
        {
            Id = id,
            TenantId = Tenant,
            CategoryCode = category,
            CommissionDate = date,
            CommissionCode = code,
            ProjectName = project,
            FlowStatus = status,
            // 生成 DTO 的 ReportCode 声明为非 nullable，但运行时语义允许 null
            //（SummaryService 漏斗分桶以 ReportCode != null 判断）；测试需要造 null 态
            ReportCode = reportCode!,
            Result = result,
        };

    private static InMemoryFlowStore Store(params SampleReceipt[] receipts)
    {
        var store = new InMemoryFlowStore();
        foreach (var r in receipts)
        {
            store.SaveReceipt(r);
        }
        return store;
    }

    /// <summary>空码表字典存储（summary 材料映射测试单独灌数据，默认空表）。</summary>
    private static InMemoryDictionaryStore DictStore(params InspectionReportName[] names)
    {
        var store = new InMemoryDictionaryStore();
        foreach (var rn in names)
        {
            store.SaveReportName(rn);
        }
        return store;
    }

    // === M05.F01.I01 报告汇总 ===

    [Fact]
    [Trait("Fn", "M05.F01.I01")]
    public void GetReportSummary_all_returnsColumnsAndAllRows()
    {
        var service = new SummaryService(Store(
            Receipt("R1", "CAT-A", "2026-01-10", FlowStatus.Data_entry, code: "C-2"),
            Receipt("R2", "CAT-B", "2026-01-11", FlowStatus.Review, code: "C-1")), DictStore());

        var data = service.GetReportSummary(Tenant, "ALL", null, null);

        Assert.Equal("报告汇总（ALL）", data.SummaryName);
        Assert.Equal(6, data.Columns.Count);
        Assert.Equal(2, data.Rows.Count);
        // 排序 commissionDate DESC → R2 在前
        Assert.Equal("C-1", data.Rows[0]["commissionCode"]);
        Assert.Equal("review", data.Rows[0]["flowStatus"]);
        Assert.Equal("data_entry", data.Rows[1]["flowStatus"]);
        Assert.Equal("pass", data.Rows[0]["result"]);
    }

    [Fact]
    [Trait("Fn", "M05.F01.I01")]
    public void GetReportSummary_byCategoryCode_filters()
    {
        var service = new SummaryService(Store(
            Receipt("R1", "CAT-A", "2026-01-10", FlowStatus.Review),
            Receipt("R2", "CAT-B", "2026-01-11", FlowStatus.Review)), DictStore());

        var data = service.GetReportSummary(Tenant, "CAT-A", "", "");

        Assert.Equal("报告汇总（CAT-A）", data.SummaryName);
        Assert.Single(data.Rows);
    }

    [Fact]
    [Trait("Fn", "M05.F01.I01")]
    public void GetReportSummary_blankCategoryCode_treatedAsAll()
    {
        var service = new SummaryService(Store(), DictStore()); // 空库也行，只看哨兵归一

        var data = service.GetReportSummary(Tenant, "  ", null, null);

        Assert.Equal("报告汇总（ALL）", data.SummaryName); // 空白 → ALL
        Assert.Empty(data.Rows);
    }

    [Fact]
    [Trait("Fn", "M05.F01.I01")]
    public void GetReportSummary_dateRange_closedInterval()
    {
        var service = new SummaryService(Store(
            Receipt("R1", "CAT-A", "2026-01-01", FlowStatus.Review),
            Receipt("R2", "CAT-A", "2026-01-05", FlowStatus.Review),
            Receipt("R3", "CAT-A", "2026-01-10", FlowStatus.Review)), DictStore());

        var inclusive = service.GetReportSummary(Tenant, "ALL", "2026-01-05", "2026-01-05");

        Assert.Single(inclusive.Rows); // 闭区间含两端

        var open = service.GetReportSummary(Tenant, "ALL", "2026-01-06", "");
        Assert.Single(open.Rows); // 只有 01-10
    }

    // === M05.F02.I01 仪表盘统计 ===

    [Fact]
    [Trait("Fn", "M05.F02.I01")]
    public void GetDashboardStats_aggregatesByStatus()
    {
        var store = new InMemoryFlowStore();
        store.SaveContract(new Contract { Id = "CT-1", TenantId = Tenant });
        store.SaveContract(new Contract { Id = "CT-2", TenantId = Tenant });
        store.SaveSample(new Sample { Id = "S-1", TenantId = Tenant });
        store.SaveSample(new Sample { Id = "S-2", TenantId = Tenant });
        store.SaveSample(new Sample { Id = "S-3", TenantId = Tenant });
        // 7 张接样单：draft 桶 3（receiving/task/data_entry）、reviewing 2（review/approval）、issued 2（issuance/archived）
        foreach (var (id, status) in new[]
        {
            ("R1", FlowStatus.Receiving), ("R2", FlowStatus.Task_assignment), ("R3", FlowStatus.Data_entry),
            ("R4", FlowStatus.Review), ("R5", FlowStatus.Approval),
            ("R6", FlowStatus.Issuance), ("R7", FlowStatus.Archived),
        })
        {
            store.SaveReceipt(Receipt(id, "CAT-A", "2026-01-10", status));
        }
        var service = new SummaryService(store, DictStore());

        var stats = service.GetDashboardStats(Tenant);

        Assert.Equal(2, stats.ContractCount);
        Assert.Equal(7, stats.ReceiptCount);
        Assert.Equal(3, stats.SampleCount);
        Assert.Equal(3, stats.ReportCountByStatus.Draft);
        Assert.Equal(2, stats.ReportCountByStatus.Reviewing);
        Assert.Equal(2, stats.ReportCountByStatus.Issued);
        Assert.Equal(3, stats.PendingTaskCount); // task_assignment + data_entry + review
    }

    [Fact]
    [Trait("Fn", "M05.F02.I01")]
    public void GetDashboardStats_empty_returnsZeros()
    {
        var service = new SummaryService(new InMemoryFlowStore(), DictStore());

        var stats = service.GetDashboardStats(Tenant);

        Assert.Equal(0, stats.ContractCount);
        Assert.Equal(0, stats.ReceiptCount);
        Assert.Equal(0, stats.SampleCount);
        Assert.Equal(0, stats.ReportCountByStatus.Draft);
        Assert.Equal(0, stats.ReportCountByStatus.Reviewing);
        Assert.Equal(0, stats.ReportCountByStatus.Issued);
        Assert.Equal(0, stats.PendingTaskCount);
    }

    // === M05.F01.I03 核心指标（材料映射走码表 summaryName，不是 categoryCode）===

    [Fact]
    [Trait("Fn", "M05.F01.I03")]
    public void GetDashboardStats_materialRate_usesReportNameSummaryName()
    {
        // 码表：CAT-A summaryName 含「混凝土」→ concrete；CAT-B 含「钢筋」→ rebar
        var dict = DictStore(
            new InspectionReportName { Code = "CAT-A", SummaryName = "混凝土及砂浆汇总" },
            new InspectionReportName { Code = "CAT-B", SummaryName = "钢筋原材料汇总" });
        var store = new InMemoryFlowStore();
        // 2 条 CAT-A（1 pass）+ 1 条 CAT-B（pass）
        store.SaveReceipt(Receipt("R1", "CAT-A", "2026-01-10", FlowStatus.Review, result: ReceiptResult.Pass));
        store.SaveReceipt(Receipt("R2", "CAT-A", "2026-01-10", FlowStatus.Review, result: ReceiptResult.Fail));
        store.SaveReceipt(Receipt("R3", "CAT-B", "2026-01-10", FlowStatus.Review, result: ReceiptResult.Pass));
        var service = new SummaryService(store, dict);

        var stats = service.GetDashboardStats(Tenant);

        Assert.Equal(2, stats.QualifiedRateByMaterial.Concrete.Total);
        Assert.Equal(1, stats.QualifiedRateByMaterial.Concrete.Pass);
        Assert.Equal(0.5, stats.QualifiedRateByMaterial.Concrete.Rate, 3);
        Assert.Equal(1, stats.QualifiedRateByMaterial.Rebar.Total);
        Assert.Equal(0, stats.QualifiedRateByMaterial.Sand.Total);
    }

    [Fact]
    [Trait("Fn", "M05.F01.I04")]
    public void GetDashboardStats_funnel_sixStages()
    {
        var store = new InMemoryFlowStore();
        // 各状态 1 条：receiving/task_assignment/data_entry(无report)/review/issuance + data_entry(有report)
        store.SaveReceipt(Receipt("R1", "CAT-A", "2026-01-10", FlowStatus.Receiving, reportCode: "RP-1"));
        store.SaveReceipt(Receipt("R2", "CAT-A", "2026-01-10", FlowStatus.Task_assignment, reportCode: "RP-2"));
        store.SaveReceipt(Receipt("R3", "CAT-A", "2026-01-10", FlowStatus.Data_entry, reportCode: null));
        store.SaveReceipt(Receipt("R4", "CAT-A", "2026-01-10", FlowStatus.Review, reportCode: "RP-4"));
        store.SaveReceipt(Receipt("R5", "CAT-A", "2026-01-10", FlowStatus.Issuance, reportCode: "RP-5"));
        var service = new SummaryService(store, DictStore());

        var f = service.GetDashboardStats(Tenant).FunnelByStage;

        Assert.Equal(1, f.Pending_collect);
        Assert.Equal(1, f.Received);
        Assert.Equal(1, f.Testing);    // data_entry 无 reportCode
        Assert.Equal(0, f.Reporting);  // data_entry 有 reportCode 才进编制段
        Assert.Equal(1, f.Reviewing);
        Assert.Equal(1, f.Issued);
    }
}
