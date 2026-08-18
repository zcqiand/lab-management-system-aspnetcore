namespace Lab.AspNetCore.Data;

/// <summary>
/// 把 CatalogStore.BrandDeleted 事件挂到 RequirementStore.OnBrandDeleted ——
/// 牌号码删除时技术要求 brand 列 SET NULL（镜像 V011 FK ON DELETE SET NULL）。
/// HostedService 只为拿构造时机，不做后台事。
/// </summary>
public sealed class CatalogBrandFkHook(InMemoryCatalogStore catalog, InMemoryRequirementStore requirements)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        catalog.BrandDeleted += requirements.OnBrandDeleted;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
