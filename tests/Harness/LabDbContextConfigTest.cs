namespace Lab.AspNetCore.Tests.Harness;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// EF column mapping 锁住 prod 配置 chain 必须挂 UseSnakeCaseNamingConvention。
///
/// 历史 (2026-09-04 prod incident)：
/// tests/Harness/TestDb.cs:28 + LabDbContextModelTest.cs:22 都挂了 convention，
/// src/Program.cs:141 (LAB_DATA_PROVIDER=ef 路径) 漏挂 → prod 首查 SampleReceipt
/// 触发 Npgsql 42703 column s."Id" does not exist (DB 列 snake_case id)。
/// memory 分支测试全绿 prod 首请求 500 是典型盲区。
///
/// 抽到 LabDbContextConfig.UseLabNpgsql 后 prod (Program.cs) 与 test (TestDb) 同源，
/// 未来谁去掉 convention 这套测试立刻红。
/// 不挂 [Trait("Fn", ...)]：脚手架级（LabDbContextModelTest 同约定）。
/// 不依赖 PG：仅 metadata 验证，本机可跑。
/// </summary>
public class LabDbContextConfigTest
{
    [Fact]
    public void UseLabNpgsql_maps_SampleReceipt_columns_to_snake_case()
    {
        var options = new DbContextOptionsBuilder<LabDbContext>()
            .UseLabNpgsql("Host=stub;Database=stub")
            .Options;

        using var ctx = new LabDbContext(options);
        var entity = ctx.Model.FindEntityType(typeof(SampleReceipt))!;

        Assert.Equal("id", entity.FindProperty(nameof(SampleReceipt.Id))!.GetColumnName());
        Assert.Equal("tenant_id", entity.FindProperty(nameof(SampleReceipt.TenantId))!.GetColumnName());
        Assert.Equal("flow_status", entity.FindProperty(nameof(SampleReceipt.FlowStatus))!.GetColumnName());
        Assert.Equal("contract_id", entity.FindProperty(nameof(SampleReceipt.ContractId))!.GetColumnName());
        Assert.Equal("created_at", entity.FindProperty(nameof(SampleReceipt.CreatedAt))!.GetColumnName());
    }

    [Fact]
    public void UseLabNpgsql_maps_Contract_columns_to_snake_case()
    {
        // Contract 是 B3 流程域另一张表，V002 列同样 snake_case
        var options = new DbContextOptionsBuilder<LabDbContext>()
            .UseLabNpgsql("Host=stub;Database=stub")
            .Options;

        using var ctx = new LabDbContext(options);
        var entity = ctx.Model.FindEntityType(typeof(Contract))!;

        Assert.Equal("id", entity.FindProperty(nameof(Contract.Id))!.GetColumnName());
        Assert.Equal("tenant_id", entity.FindProperty(nameof(Contract.TenantId))!.GetColumnName());
        Assert.Equal("status", entity.FindProperty(nameof(Contract.Status))!.GetColumnName());
    }

    [Fact]
    public void Without_convention_columns_default_to_PascalCase_proving_bug()
    {
        // 反向测试：证明「不挂 convention = 42703」。这条必须绿（即 PascalCase 是 EF 默认），
        // 才能佐证正向上面的 UseLabNpgsql 锁住 convention 的必要性。
        var options = new DbContextOptionsBuilder<LabDbContext>()
            .UseNpgsql("Host=stub;Database=stub")
            .Options;

        using var ctx = new LabDbContext(options);
        var entity = ctx.Model.FindEntityType(typeof(SampleReceipt))!;

        Assert.Equal("Id", entity.FindProperty(nameof(SampleReceipt.Id))!.GetColumnName());
        Assert.Equal("TenantId", entity.FindProperty(nameof(SampleReceipt.TenantId))!.GetColumnName());
    }
}