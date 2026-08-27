namespace Lab.AspNetCore.Auth.Sso;

using System.Collections.Concurrent;

/// <summary>
/// MenuSnapshotCache - SSO/refresh/密码登录时拉取的 saas 菜单快照，按 userId 进程内缓存。
/// 镜像 springboot MenuSnapshotCache / nextjs menu-snapshot.ts（ADR-0009 方案 B）。
///
/// 2026-08-27 起 demo 兜底删除：miss 不再回退假树，调用方（AuthService.Menus）抛
/// MenusUnavailableException（503），前端回退静态菜单。
/// 局限（单实例部署下可接受）：进程内缓存多实例不共享；TTL 30min 后需 refresh 或重登重新填充。
/// </summary>
public sealed class MenuSnapshotCache
{
    private sealed record Snapshot(List<Controllers.Generated.MenuNode> Menus, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<string, Snapshot> _store = new();
    private readonly TimeSpan _ttl;

    public MenuSnapshotCache() : this(TimeSpan.FromMinutes(30)) { }

    /// <summary>测试用构造器（可注入短 TTL）。</summary>
    public MenuSnapshotCache(TimeSpan ttl) => _ttl = ttl;

    /// <summary>写入/覆盖某用户的菜单快照。空参静默忽略。</summary>
    public void Put(string? userId, List<Controllers.Generated.MenuNode>? menus)
    {
        if (userId is null || menus is null) return;
        _store[userId] = new Snapshot(menus, DateTime.UtcNow + _ttl);
    }

    /// <summary>读某用户的未过期快照；miss/过期返回 null。</summary>
    public List<Controllers.Generated.MenuNode>? Get(string? userId)
    {
        if (userId is null) return null;
        if (!_store.TryGetValue(userId, out var snap)) return null;
        if (DateTime.UtcNow > snap.ExpiresAt)
        {
            _store.TryRemove(userId, out _);
            return null;
        }
        return snap.Menus;
    }

    /// <summary>当前缓存条目数（监控/测试用）。</summary>
    public int Size => _store.Count;
}
