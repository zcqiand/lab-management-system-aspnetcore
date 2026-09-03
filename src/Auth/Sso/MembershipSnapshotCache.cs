namespace Lab.AspNetCore.Auth.Sso;

using System.Collections.Concurrent;
using Lab.AspNetCore.Controllers.Generated;

/// <summary>
/// MembershipSnapshotCache — SSO/refresh 时到手的 saas memberships 租户快照，按 userId 进程内缓存。
/// 2026-09-03 租户体系对齐（docs/superpowers/specs/2026-09-03-me-tenant-alignment-design.md）：
/// Me() 对 SSO 用户必须返回 saas memberships 租户（与 SsoCallback 同体系），否则前端
/// hydrateAuth 的 tenants.find(localStorage.activeTenantId=saas UUID) 跨体系失配 →
/// awaiting_tenant → 卡「检查登录态…」。miss 时 Me() 抛 401 由前端 refresh 链自愈。
/// 与 MenuSnapshotCache 同款局限：进程内、单实例、TTL 后需 refresh 重新填充。
/// </summary>
public sealed class MembershipSnapshotCache
{
    private sealed record Snapshot(List<MyTenant> Tenants, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<string, Snapshot> _store = new();
    private readonly TimeSpan _ttl;

    public MembershipSnapshotCache() : this(TimeSpan.FromMinutes(30)) { }

    /// <summary>测试用构造器（可注入短 TTL）。</summary>
    public MembershipSnapshotCache(TimeSpan ttl) => _ttl = ttl;

    /// <summary>写入/覆盖某用户的租户快照。空参静默忽略。</summary>
    public void Put(string? userId, List<MyTenant>? tenants)
    {
        if (userId is null || tenants is null) return;
        _store[userId] = new Snapshot(tenants, DateTime.UtcNow + _ttl);
    }

    /// <summary>读某用户的未过期快照；miss/过期返回 null。</summary>
    public List<MyTenant>? Get(string? userId)
    {
        if (userId is null) return null;
        if (!_store.TryGetValue(userId, out var snap)) return null;
        if (DateTime.UtcNow > snap.ExpiresAt)
        {
            _store.TryRemove(userId, out _);
            return null;
        }
        return snap.Tenants;
    }

    /// <summary>当前缓存条目数（监控/测试用）。</summary>
    public int Size => _store.Count;
}
