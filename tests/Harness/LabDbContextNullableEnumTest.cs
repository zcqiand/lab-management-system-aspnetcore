namespace Lab.AspNetCore.Tests.Harness;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xunit;

/// <summary>
/// 5.52 修：可选枚举属性物化炸点（GET /api/receipts 5204 实测 500）。
///
/// 根因链：shared schema.ts sample_receipts.result 可空（receiving 行合法 NULL），
/// tsp 已声明 `result?: ReceiptResult`，但 NSwag 生成的 DTO 是非空值类型
/// `ReceiptResult Result` → EF 按 NRT 推断 required + Wire 值转换器遇 NULL 物化抛
/// `Column 'result' is null`。
///
/// 修复 = 人裁方案①：NSwag `generateOptionalPropertiesAsNullable` 开关翻转生成物
/// （ReceiptResult? Result）+ LabDbContext 可空值转换器适配（DB NULL ↔ CLR null 直通，
/// 禁止兜底成枚举字面量）。本测试把两侧都锁进红绿：
///   1. 模型侧：Result 属性 ClrType 必须是 ReceiptResult?（翻转前 = ReceiptResult，红）
///   2. 转换器侧：wire round-trip + null 直通（兜底字面量 = 红）
/// 不挂 [Trait("Fn", ...)]：脚手架级（LabDbContextConfigTest 同约定）。
/// 不依赖 PG：仅 metadata 验证，本机可跑。
/// </summary>
public class LabDbContextNullableEnumTest
{
    private static (IEntityType Entity, IProperty Result) BuildReceiptModel()
    {
        var options = new DbContextOptionsBuilder<LabDbContext>()
            .UseLabNpgsql("Host=stub;Database=stub")
            .Options;

        using var ctx = new LabDbContext(options);
        var entity = ctx.Model.FindEntityType(typeof(SampleReceipt))!;
        return (entity, entity.FindProperty(nameof(SampleReceipt.Result))!);
    }

    [Fact]
    public void ReceiptResult_property_is_nullable_in_model()
    {
        var (_, result) = BuildReceiptModel();

        // receiving 行 result 列合法 NULL（schema.ts 无 notNull）——非空 ClrType 物化即炸
        Assert.Equal(typeof(ReceiptResult?), result.ClrType);
        Assert.True(result.IsNullable);
    }

    [Fact]
    public void ReceiptResult_converter_roundTrips_wire_values_and_passes_null_through()
    {
        var (_, result) = BuildReceiptModel();

        var converter = Assert.IsType<ValueConverter<ReceiptResult?, string>>(
            result.GetValueConverter());

        // 契约小写串 round-trip（[EnumMember] wire 值）
        var wire = Assert.IsType<string>(converter.ConvertToProvider(ReceiptResult.Pass));
        Assert.Equal("pass", wire);
        Assert.Equal(ReceiptResult.Pass, converter.ConvertFromProvider(wire));

        // DB NULL → CLR null 直通：禁止兜底成任何枚举字面量
        Assert.Null(converter.ConvertFromProvider(null));
    }
}
