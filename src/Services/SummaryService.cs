namespace Lab.AspNetCore.Services;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Data;

/// <summary>
/// M05.F01 报告汇总 + M05.F02 仪表盘统计（B4）。
/// 语义镜像 springboot SummaryService：
///   - categoryCode null/空白 → 哨兵 ALL（不过滤）
///   - dateFrom/dateTo 闭区间（YYYY-MM-DD 字典序），空串无界
///   - 6 列固定顺序，null 渲染 ""
///   - 3 桶：draft=receiving+task_assignment+data_entry；reviewing=review+approval；
///     issued=issuance+archived；pendingTask=task_assignment+data_entry+review
/// 2026-09-04 起扩展 M05.F01.I03（核心指标）+ M05.F01.I04（任务漏斗）4 段：
/// TodayTestCount / QualifiedRateByMaterial{concrete,rebar,sand} /
/// ReportOutputByStatus{generated,pending,issued} /
/// FunnelByStage{pending_collect,received,testing,reporting,reviewing,issued}。
/// </summary>
public sealed class SummaryService(IFlowStore store, IDictionaryStore dictionaryStore)
{
    private static readonly (string Key, string Label)[] Columns =
    {
        ("commissionCode", "委托编号"),
        ("categoryCode", "报告类别"),
        ("projectName", "工程名称"),
        ("flowStatus", "流程状态"),
        ("result", "结论"),
        ("reportCode", "报告编号"),
    };

    // summaryName 关键词 → 材料类型（与 msw / springboot 同款语义）
    private static readonly (string Mat, string Kws)[] MaterialKeywords =
    {
        ("concrete", "混凝土|水泥"),
        ("rebar", "钢筋|钢材|焊接|机械连接|连接"),
        ("sand", "砂|碎（卵）石|轻集料|颗粒级配"),
    };

    public SummaryData GetReportSummary(string tenantId, string? categoryCode, string? dateFrom, string? dateTo)
    {
        var cat = string.IsNullOrWhiteSpace(categoryCode) ? "ALL" : categoryCode;
        var from = dateFrom ?? "";
        var to = dateTo ?? "";

        var rows = store.Summary(tenantId, cat, from, to)
            .Select(r => new Dictionary<string, string>
            {
                ["commissionCode"] = r.CommissionCode ?? "",
                ["categoryCode"] = r.CategoryCode ?? "",
                ["projectName"] = r.ProjectName ?? "",
                ["flowStatus"] = Snake(r.FlowStatus),
                ["result"] = Snake(r.Result),
                ["reportCode"] = r.ReportCode ?? "",
            })
            .ToList<IDictionary<string, string>>();

        return new SummaryData
        {
            SummaryName = $"报告汇总（{cat}）",
            Columns = Columns.Select(c => new SummaryColumn { Key = c.Key, Label = c.Label }).ToList(),
            Rows = rows,
        };
    }

    public DashboardStats GetDashboardStats(string tenantId)
    {
        var receipts = store.Summary(tenantId, "ALL", "", "");
        var byStatus = receipts.GroupBy(r => r.FlowStatus).ToDictionary(g => g.Key, g => g.Count());
        int Count(FlowStatus s) => byStatus.TryGetValue(s, out var n) ? n : 0;
        int issued = Count(FlowStatus.Issuance) + Count(FlowStatus.Archived);
        int reviewing = Count(FlowStatus.Review) + Count(FlowStatus.Approval);

        // ─── M05.F01.I03 今日试验总数 ───
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)).ToString("yyyy-MM-dd"); // Asia/Shanghai 近似
        int todayTestCount = receipts.Count(r =>
        {
            var c = r.CreatedAt ?? "";
            var t = r.TestStartDate ?? "";
            return c.StartsWith(today) || t == today;
        });

        // ─── M05.F01.I03 按材料类型合格率 ───
        // 码表全量预载（categoryCode → summaryName），避免逐条查库的 N+1。
        var summaryNameByCode = dictionaryStore.FilterReportNames(null)
            .GroupBy(rn => rn.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().SummaryName ?? "", StringComparer.Ordinal);
        var matTotal = new Dictionary<string, int> { ["concrete"] = 0, ["rebar"] = 0, ["sand"] = 0 };
        var matPass = new Dictionary<string, int> { ["concrete"] = 0, ["rebar"] = 0, ["sand"] = 0 };
        foreach (var r in receipts)
        {
            var mat = MaterialOf(r.CategoryCode, summaryNameByCode);
            if (mat == null) continue;
            matTotal[mat]++;
            if (r.Result == ReceiptResult.Pass) matPass[mat]++;
        }
        double Rate(int t, int p) => t > 0 ? Math.Round((double)p / t * 1000) / 1000.0 : 0.0;
        var qrm = new QualifiedRateByMaterial
        {
            Concrete = new MaterialQualifiedRate { Total = matTotal["concrete"], Pass = matPass["concrete"], Rate = Rate(matTotal["concrete"], matPass["concrete"]) },
            Rebar = new MaterialQualifiedRate { Total = matTotal["rebar"], Pass = matPass["rebar"], Rate = Rate(matTotal["rebar"], matPass["rebar"]) },
            Sand = new MaterialQualifiedRate { Total = matTotal["sand"], Pass = matPass["sand"], Rate = Rate(matTotal["sand"], matPass["sand"]) },
        };

        // ─── M05.F01.I03 报告产出量 ───
        var ros = new ReportOutputByStatus
        {
            Generated = receipts.Count(r => r.ReportCode != null),
            Pending = reviewing,
            Issued = issued,
        };

        // ─── M05.F01.I04 任务状态漏斗（6 段）───
        int dataEntryNoReport = receipts.Count(r => r.FlowStatus == FlowStatus.Data_entry && r.ReportCode == null);
        int dataEntryWithReport = receipts.Count(r => r.FlowStatus == FlowStatus.Data_entry && r.ReportCode != null);
        var funnel = new FunnelByStage
        {
            Pending_collect = Count(FlowStatus.Receiving),
            Received = Count(FlowStatus.Task_assignment),
            Testing = dataEntryNoReport,
            Reporting = dataEntryWithReport,
            Reviewing = reviewing,
            Issued = issued,
        };

        return new DashboardStats
        {
            ContractCount = store.CountContracts(tenantId),
            ReceiptCount = receipts.Count,
            SampleCount = store.CountSamples(tenantId),
            ReportCountByStatus = new ReportCountByStatus
            {
                Draft = Count(FlowStatus.Receiving) + Count(FlowStatus.Task_assignment) + Count(FlowStatus.Data_entry),
                Reviewing = reviewing,
                Issued = issued,
            },
            PendingTaskCount = Count(FlowStatus.Task_assignment) + Count(FlowStatus.Data_entry) + Count(FlowStatus.Review),
            TodayTestCount = todayTestCount,
            QualifiedRateByMaterial = qrm,
            ReportOutputByStatus = ros,
            FunnelByStage = funnel,
        };
    }

    /// <summary>categoryCode → summaryName（码表预载）→ 材料类型关键词匹配。与 msw / springboot 同款语义。</summary>
    private static string? MaterialOf(string? categoryCode, IReadOnlyDictionary<string, string> summaryNameByCode)
    {
        if (string.IsNullOrEmpty(categoryCode)) return null;
        var summaryName = summaryNameByCode.GetValueOrDefault(categoryCode, "");
        for (int i = 0; i < MaterialKeywords.Length; i++)
        {
            foreach (var kw in MaterialKeywords[i].Kws.Split('|'))
            {
                if (summaryName.Contains(kw, StringComparison.Ordinal)) return MaterialKeywords[i].Mat;
            }
        }
        return null;
    }

    /// <summary>枚举 → 契约小写蛇形值（FlowStatus.Data_entry → "data_entry"）。</summary>
    private static string Snake<T>(T e) where T : struct, Enum
    {
        var name = e.ToString();
        return name.Length == 0 ? "" : char.ToLowerInvariant(name[0]) + name[1..];
    }
}