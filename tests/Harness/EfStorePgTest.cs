namespace Lab.AspNetCore.Tests.Harness;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// lab_test 真库语义测试：EF 路径的真 SQL 行为（ILIKE 匹配 / tenant 隔离 / 排序 / 级联）。
/// 硬依赖共享 PG —— 连不上即失败（TestDb.RequireReachable），不 skip：
/// memory 分支测试掩盖 prod 问题是 v0.2.25/26 两起事故的根因。
///
/// 分层（CI=翻译性 / gate=真库）：[Trait("Category", "RealDb")] 标记本测试集，
/// ci.yml 用 --filter 排除（GitHub runner 够不到内网 PG）；suite gate L4 全量跑。
/// 数据隔离：独立 tenant_id 前缀（PG-T-），全部用例走该前缀；fixture 末尾按前缀清理。
/// 不挂 [Trait("Fn", ...)]：脚手架级（LabDbContextModelTest 同约定）。
/// </summary>
[Trait("Category", "RealDb")]
public sealed class EfStorePgTest : IDisposable
{
    private const string Tenant = "PG-T-EFSTORE";
    private const string ObjCode = "OBJ-PG-T";

    private readonly LabDbContext db = TestDb.CreateContext();

    public EfStorePgTest()
    {
        TestDb.RequireReachable();
        Cleanup();
        SeedParents();
    }

    public void Dispose() => Cleanup();

    private void Cleanup()
    {
        db.SampleReceipts.Where(r => r.TenantId == Tenant).ExecuteDelete();
        db.Samples.Where(s => s.TenantId == Tenant).ExecuteDelete();
        db.TestRecords.Where(t => t.TenantId == Tenant).ExecuteDelete();
        db.Contracts.Where(c => c.TenantId == Tenant).ExecuteDelete();
        db.InspectionBrands.Where(b => b.TenantId == Tenant).ExecuteDelete();
        db.InspectionModels.Where(m => m.TenantId == Tenant).ExecuteDelete();
        db.InspectionParameters.Where(p => p.Code == "PAR-583").ExecuteDelete();
        db.InspectionReportNames.Where(r => r.Code == "RN-583").ExecuteDelete();
        // 父行（objects/specialties 是平台级字典，同 code 复用；清业务行即够）
        db.SaveChanges();
    }

    /// <summary>真库 FK：inspection_models.inspection_object_code → inspection_objects.code，
    /// 测试数据须先种父行（这正是 memory 分支测不出的约束之一）。</summary>
    private void SeedParents()
    {
        if (!db.InspectionSpecialties.Any(s => s.Code == "SP-SMK-001"))
        {
            db.InspectionSpecialties.Add(new InspectionSpecialty
            {
                Code = "SP-SMK-001",
                OfficialNo = "OFFICIAL-PG-T",
                Name = "PG 测试专项",
                SortOrder = 0,
                CreatedAt = "2026-01-01T00:00:00Z",
                UpdatedAt = "2026-01-01T00:00:00Z",
            });

            // 两次 SaveChanges：DTO 实体间无 EF navigation（FK 只在 DB 层），
            // 单批双插入 EF 拓扑排序不到依赖，objects 会先于 specialties 落库 → 23503。
            db.SaveChanges();
        }

        if (!db.InspectionObjects.Any(o => o.Code == ObjCode))
        {
            db.InspectionObjects.Add(new InspectionObject
            {
                Code = ObjCode,
                InspectionSpecialtyCode = "SP-SMK-001",
                SourceProjectNo = "SRC-PG-T",
                SourceProjectName = "PG 测试项目",
                Name = "PG 测试对象",
                // 5.74：父行常驻 lab_test（Cleanup 有意不清），SortOrder 必须压尾——
                // 曾用 0 抢占 lab-react CategoryDictList 默认选中位（客户端按
                // sortOrder 升序取 [0]），令 categoryDictPages M04.F06.I01 确定性红。
                SortOrder = 9_999,
                CreatedAt = "2026-01-01T00:00:00Z",
                UpdatedAt = "2026-01-01T00:00:00Z",
            });
        }

        db.SaveChanges();
    }

    private static InspectionModel Model(string code, string name, int sortOrder = 0) => new()
    {
        Code = code,
        TenantId = Tenant,
        InspectionObjectCode = ObjCode,
        Name = name,
        SortOrder = sortOrder,
        CreatedAt = "2026-01-01T00:00:00Z",
        UpdatedAt = "2026-01-01T00:00:00Z",
    };

    [Fact]
    public void FilterModels_keywordIsCaseInsensitiveLike()
    {
        db.InspectionModels.Add(Model("M-1", "Fire-型号"));
        db.InspectionModels.Add(Model("M-2", "FIRE-型号"));
        db.InspectionModels.Add(Model("M-0", "无关"));
        db.SaveChanges();

        var outList = new EfCatalogStore(db).FilterModels(Tenant, null, "fire");

        Assert.Equal(2, outList.Count);
        Assert.Contains(outList, m => m.Code == "M-1");
        Assert.Contains(outList, m => m.Code == "M-2");
    }

    [Fact]
    public void FilterModels_isolatedByTenant()
    {
        db.InspectionModels.Add(Model("M-ISO", "隔离验证"));
        db.SaveChanges();

        // 另一租户查不到本租户数据（真 SQL WHERE tenant_id 生效）
        var other = new EfCatalogStore(db).FilterModels("PG-T-OTHER", null, null);
        Assert.DoesNotContain(other, m => m.Code == "M-ISO");
    }

    [Fact]
    public void FilterModels_sortedBySortOrderThenCode()
    {
        db.InspectionModels.Add(Model("M-B", "b", 1));
        db.InspectionModels.Add(Model("M-A", "a", 1));
        db.InspectionModels.Add(Model("M-Z", "z", 0));
        db.SaveChanges();

        var outList = new EfCatalogStore(db).FilterModels(Tenant, null, null);

        Assert.Equal(new[] { "M-Z", "M-A", "M-B" }, outList.Select(m => m.Code).ToArray());
    }

    [Fact]
    public void SaveModel_upsertSameKeyUpdates()
    {
        var store = new EfCatalogStore(db);
        store.SaveModel(Model("M-UP", "v1"));
        store.SaveModel(Model("M-UP", "v2")); // 同 tenant+code → SetValues 更新

        var all = store.FilterModels(Tenant, null, "v2");
        Assert.Single(all);
        Assert.Equal("v2", all[0].Name);
    }

    // 5.75 现场回归：act 写路径 = FindReceipt（tracked 实例）原地 FlowHistory.Add + SaveReceipt。
    // EF 默认 ValueComparer 对 List 属性做引用快照 → 同实例 mutate 侦测不到 → jsonb 不落库
    // （症状：act 200 + lastSubmittedBy 已写，但 GET history 恒 []）。真库才能测出。
    [Fact]
    public void SaveReceipt_persistsInPlaceFlowHistoryMutation()
    {
        var store = new EfFlowStore(db);
        var categoryCode = db.InspectionReportNames.OrderBy(x => x.Code).Select(x => x.Code).First();
        db.Contracts.Add(new Contract
        {
            Id = "C-FH",
            TenantId = Tenant,
            ContractCode = "HT-FH",
            ClientUnit = "cu",
            ProjectName = "pn",
            ConstructionUnit = "csu",
            WitnessUnit = "wu",
            Witness = "w",
            Status = ContractStatus.Active,
            CreatedAt = "t",
            UpdatedAt = "t",
        });
        db.SaveChanges();

        store.SaveReceipt(new SampleReceipt
        {
            Id = "R-FH",
            TenantId = Tenant,
            ContractId = "C-FH",
            CommissionCode = "CM-FH",
            CommissionDate = "2026-09-21",
            CategoryCode = categoryCode,
            ReceivedBy = "alice",
            SampleSource = "client",
            TestCategory = "concrete",
            FlowStatus = FlowStatus.Receiving,
            FlowHistory = new List<FlowHistoryEntry>(),
            CreatedAt = "t",
            UpdatedAt = "t",
        });

        // act 写路径同款：FindReceipt 返回 tracked 实例，原地 mutate，再 SaveReceipt
        var tracked = store.FindReceipt(Tenant, "R-FH");
        Assert.NotNull(tracked);
        tracked!.FlowHistory.Add(new FlowHistoryEntry
        {
            Action = FlowAction.Return,
            From = FlowStatus.Task_assignment,
            To = FlowStatus.Receiving,
            Operator = "ct",
            At = "t",
            Reason = "",
        });
        store.SaveReceipt(tracked);

        // 全新 context 重读（同 context 命中已跟踪实例，测不出持久化）
        using var fresh = TestDb.CreateContext();
        var reread = fresh.SampleReceipts.First(r => r.TenantId == Tenant && r.Id == "R-FH");
        Assert.Single(reread.FlowHistory);
        Assert.Equal(FlowAction.Return, reread.FlowHistory[0].Action);
    }

    // 5.83 潜伏面回归：Upsert 的 jsonb List 属性（InspectionParameter.Aliases /
    // InspectionReportName.ExtFields）与 5.75 SaveReceipt 同根——EF ValueComparer 引用快照
    // 令「tracked 实例原地 mutate 再 Save(自身)」的 jsonb 变更静默不落库。现行写路径
    // （DictionaryService）全为新实例赋值无活 bug，本组测试锁 Upsert 防御性新实例拷贝语义。
    [Fact]
    public void SaveParameter_persistsInPlaceAliasesMutation()
    {
        var store = new EfDictionaryStore(db);
        store.SaveParameter(new InspectionParameter
        {
            Code = "PAR-583",
            Name = "n",
            RawName = "n",
            CanonicalName = "n",
            MethodText = "",
            Aliases = new List<string> { "别名-1" },
            Unit = "",
            SourceType = InspectionParameterSourceType.Official,
            SortOrder = 0,
            CreatedAt = "t",
            UpdatedAt = "t",
        });

        var tracked = store.FindParameter("PAR-583");
        Assert.NotNull(tracked);
        tracked!.Aliases.Add("别名-2");
        store.SaveParameter(tracked);

        using var fresh = TestDb.CreateContext();
        var reread = fresh.InspectionParameters.Find("PAR-583");
        Assert.NotNull(reread);
        Assert.Equal(new[] { "别名-1", "别名-2" }, reread!.Aliases);
    }

    [Fact]
    public void SaveReportName_persistsInPlaceExtFieldsMutation()
    {
        var store = new EfDictionaryStore(db);
        store.SaveReportName(new InspectionReportName
        {
            Code = "RN-583",
            Name = "n",
            FullName = "",
            TemplatePath = "",
            SummaryName = "",
            ExtFields = new List<ExtFieldDef>
            {
                new() { Key = "k1", Label = "l1", Type = ExtFieldDefType.Text },
            },
            Description = "",
            SortOrder = 0,
            CreatedAt = "t",
            UpdatedAt = "t",
        });

        var tracked = store.FindReportName("RN-583");
        Assert.NotNull(tracked);
        tracked!.ExtFields.Add(new ExtFieldDef { Key = "k2", Label = "l2", Type = ExtFieldDefType.Text });
        store.SaveReportName(tracked);

        using var fresh = TestDb.CreateContext();
        var reread = fresh.InspectionReportNames.Find("RN-583");
        Assert.NotNull(reread);
        Assert.Equal(new[] { "k1", "k2" }, reread!.ExtFields.Select(f => f.Key));
    }
}
