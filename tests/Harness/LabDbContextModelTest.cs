namespace App.Harness;

using Lab.AspNetCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// EF 模型完整性冒烟测试：Lab:Data:Provider=ef 下首个请求才触发模型验证，
/// 漏配实体（如 AdditionalProperties 未 Ignore）在 prod 炸 500 而 dotnet test 全绿
/// （aspnetcore-composition-root-blind-spot 同类盲区）。
/// 本测试强制初始化全模型，把「首个请求才爆」提前到「测试就爆」。
///
/// 不挂 [Trait("Fn", ...)]：脚手架级，不属于项目功能清单（MemoryDiResolutionTest 同约定）。
/// </summary>
public class LabDbContextModelTest
{
    [Fact]
    public void Model_initializes_withoutUnmappedProperties()
    {
        var options = new DbContextOptionsBuilder<LabDbContext>()
            .UseNpgsql("Host=localhost;Database=lab_dev;Username=probe;Password=probe")
            .UseSnakeCaseNamingConvention()
            .Options;

        using var ctx = new LabDbContext(options);
        // 触碰 Model 触发全模型构建 + ModelValidator 验证（不打开连接）；
        // 漏 Ignore 的属性在这里就抛 InvalidOperationException
        var model = ctx.Model;

        // 全实体逐个确认：AdditionalProperties 必须被 Ignore（JsonExtensionData 字典不可映射）
        foreach (var entityType in model.GetEntityTypes())
        {
            Assert.False(
                entityType.FindProperty("AdditionalProperties") is not null,
                $"{entityType.DisplayName()} 照映射进了 AdditionalProperties —— 须在 OnModelCreating Ignore");
        }
    }
}
