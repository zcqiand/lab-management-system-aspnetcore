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
/// </summary>
public sealed class SummaryService(InMemoryFlowStore store)
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
                // ReceiptResult 是非空枚举（生成 DTO 无 null 态）；springboot 的 null→""
                // 语义只存在于 Optional 列，此处直接输出枚举小写值
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

        return new DashboardStats
        {
            ContractCount = store.CountContracts(tenantId),
            ReceiptCount = receipts.Count,
            SampleCount = store.CountSamples(tenantId),
            ReportCountByStatus = new ReportCountByStatus
            {
                Draft = Count(FlowStatus.Receiving) + Count(FlowStatus.Task_assignment) + Count(FlowStatus.Data_entry),
                Reviewing = Count(FlowStatus.Review) + Count(FlowStatus.Approval),
                Issued = Count(FlowStatus.Issuance) + Count(FlowStatus.Archived),
            },
            PendingTaskCount = Count(FlowStatus.Task_assignment) + Count(FlowStatus.Data_entry) + Count(FlowStatus.Review),
        };
    }

    /// <summary>枚举 → 契约小写蛇形值（FlowStatus.Data_entry → "data_entry"）。</summary>
    private static string Snake<T>(T e) where T : struct, Enum
    {
        var name = e.ToString();
        return name.Length == 0 ? "" : char.ToLowerInvariant(name[0]) + name[1..];
    }
}
