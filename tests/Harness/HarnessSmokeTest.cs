namespace App.Harness;

using Xunit;

/// <summary>
/// Trace 适配器冒烟测试：验证 dotnet test → TRX → gen-trace.py 链路。
///
/// 故意不挂 [Trait("Fn", ...)]：功能 ID 属于项目功能清单，脚手架不预设。
/// 挂了假 ID 反而会在 L5 里悬空引用（tree 里没有那个 ID → 硬红）。
/// 它在 trace.json 里表现为一行 fns 为空数组的记录 —— 完全合法。
/// </summary>
public class HarnessSmokeTest
{
    [Fact]
    public void TrxPipelineIsAlive()
    {
        Assert.Equal(2, 1 + 1);
    }
}
