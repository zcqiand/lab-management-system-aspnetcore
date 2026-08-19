namespace App.Harness;

using Lab.AspNetCore.Data;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// DI 注册完整性冒烟测试：镜像 Program.cs memory 分支（默认 Lab:Data:Provider），
/// 断言所有 Service 可解析。失败即说明 memory 分支漏注册接口映射。
///
/// 不挂 [Trait("Fn", ...)]：脚手架级，不属于项目功能清单（HarnessSmokeTest 同约定）。
///
/// 维护注意：本测试镜像的是 Program.cs 的内存分支注册代码；任一边增删 Store/Service 须同步。
/// 真正的端到端验证是 F5 启动 + scripts/live_smoke.py + gate.py。
/// </summary>
public class MemoryDiResolutionTest
{
    [Fact]
    public void MemoryBranch_resolvesAllServices()
    {
        var builder = WebApplication.CreateBuilder();

        // === 镜像 Program.cs memory 分支（写红时与 Program.cs 保持一致） ===
        builder.Services.AddSingleton<InMemoryCatalogStore>();
        builder.Services.AddSingleton<InMemoryRuleStore>();
        builder.Services.AddSingleton<InMemoryRequirementStore>();
        builder.Services.AddSingleton<InMemoryFlowStore>();
        builder.Services.AddSingleton<InMemoryDictionaryStore>();
        builder.Services.AddSingleton<InMemoryJunctionStore>();
        builder.Services.AddSingleton<ICatalogStore>(sp => sp.GetRequiredService<InMemoryCatalogStore>());
        builder.Services.AddSingleton<IRuleStore>(sp => sp.GetRequiredService<InMemoryRuleStore>());
        builder.Services.AddSingleton<IRequirementStore>(sp => sp.GetRequiredService<InMemoryRequirementStore>());
        builder.Services.AddSingleton<IFlowStore>(sp => sp.GetRequiredService<InMemoryFlowStore>());
        builder.Services.AddSingleton<IDictionaryStore>(sp => sp.GetRequiredService<InMemoryDictionaryStore>());
        builder.Services.AddSingleton<IJunctionStore>(sp => sp.GetRequiredService<InMemoryJunctionStore>());
        builder.Services.AddSingleton<SummaryService>();
        builder.Services.AddSingleton<ContractService>();
        builder.Services.AddSingleton<SampleReceiptService>();
        builder.Services.AddSingleton<SampleService>();
        builder.Services.AddSingleton<TestRecordService>();
        builder.Services.AddSingleton<ReportFlowService>();
        builder.Services.AddSingleton<DictionaryService>();
        builder.Services.AddSingleton<JunctionService>();
        builder.Services.AddSingleton<CatalogService>();
        builder.Services.AddSingleton<CalculationRuleService>();
        builder.Services.AddSingleton<TechnicalRequirementService>();

        using var sp = builder.Services.BuildServiceProvider();
        // 触达每个 service，强制 DI 构造 ctor 参数（含 I*Store 接口）
        sp.GetRequiredService<SummaryService>();
        sp.GetRequiredService<ContractService>();
        sp.GetRequiredService<SampleReceiptService>();
        sp.GetRequiredService<SampleService>();
        sp.GetRequiredService<TestRecordService>();
        sp.GetRequiredService<ReportFlowService>();
        sp.GetRequiredService<DictionaryService>();
        sp.GetRequiredService<JunctionService>();
        sp.GetRequiredService<CatalogService>();
        sp.GetRequiredService<CalculationRuleService>();
        sp.GetRequiredService<TechnicalRequirementService>();
    }
}