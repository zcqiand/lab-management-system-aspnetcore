namespace Lab.AspNetCore.Directory;

using System.Collections.Concurrent;
using Lab.AspNetCore.Controllers.Generated;

/// <summary>
/// 配置式 demo 目录（B1）。数据 1:1 镜像 lab-msw / lab-springboot：
///
///   用户：admin@lab.local / dev123456（USER-A，roleCode=admin）— ADR-0008 后主键从 username 改为 email
///   租户：TENANT-001 city-lab / TENANT-002 district-lab / TENANT-003 third-party
///   运行时 upsert：不在 seed 里的 SSO 用户落到 _upserted 内存 Dictionary
///
///   2026-08-29: 新增 saas_refresh_token 进程内缓存 (_saasRefreshTokens, by userId)。
///   MenuSnapshotCache 是进程内 ConcurrentDictionary,VPS 重 deploy / 容器重启即清空
///   → /api/auth/menus 503。修复: cache miss 时 GetSaasRefreshToken(userId) →
///   saas /token refresh → 重新调 saas /me/menus → 填 cache。单实例 OK,多实例部署
///   需要替换为 DB / Redis 持久层 (Phase 6+ follow-up)。
///
/// 口令可用 Lab:Auth:DevPassword 覆盖（避免硬编码扩散到测试）。
/// </summary>
public sealed class ConfigUserDirectory : IUserDirectory
{
    private static readonly CurrentUser DemoUser = new()
    {
        Id = "USER-A",
        Username = "admin@lab.local",
        DisplayName = "管理员",
        RoleCode = "admin",
    };

    private static readonly IReadOnlyList<MyTenant> Tenants = new[]
    {
        new MyTenant { TenantId = "TENANT-001", Code = "city-lab", Name = "市住建工程质量检测中心", RoleIds = new List<string> { "admin" } },
        new MyTenant { TenantId = "TENANT-002", Code = "district-lab", Name = "区检测站", RoleIds = new List<string> { "technician" } },
        new MyTenant { TenantId = "TENANT-003", Code = "third-party", Name = "第三方检测实验室", RoleIds = new List<string> { "viewer" } },
    };

    private readonly string _devPassword;
    private readonly Dictionary<string, CurrentUser> _upserted = new();
    private readonly ConcurrentDictionary<string, string> _saasRefreshTokens = new();

    public ConfigUserDirectory(string devPassword)
    {
        _devPassword = devPassword;
    }

    public CurrentUser? FindByUsername(string username)
    {
        if (username == null) return null;
        if (DemoUser.Username == username) return DemoUser;
        foreach (var u in _upserted.Values)
        {
            if (u.Username == username) return u;
        }
        return null;
    }

    public CurrentUser? FindByEmail(string email)
    {
        if (email == null) return null;
        if (email == DemoUser.Username) return DemoUser;
        foreach (var u in _upserted.Values)
        {
            if (u.Username == email) return u;
        }
        return null;
    }

    public CurrentUser? FindById(string id)
    {
        if (id == null) return null;
        if (id == DemoUser.Id) return DemoUser;
        foreach (var u in _upserted.Values)
        {
            if (u.Id == id) return u;
        }
        return null;
    }

    public bool CheckPassword(string username, string password) =>
        DemoUser.Username == username && _devPassword == password;

    public IReadOnlyList<MyTenant> TenantsOf(string username) => Tenants;

    public MyTenant DefaultTenant() => Tenants[0];

    public MyTenant? FindByTenantId(string tenantId) =>
        Tenants.FirstOrDefault(t => t.TenantId == tenantId);

    public CurrentUser Upsert(string id, string email, string displayName, string roleCode)
    {
        if (string.IsNullOrEmpty(email))
        {
            throw new ArgumentException("email required for upsert", nameof(email));
        }
        if (_upserted.TryGetValue(email, out var existing))
        {
            return existing;
        }
        var user = new CurrentUser
        {
            Id = id,
            Username = email,
            DisplayName = displayName,
            RoleCode = string.IsNullOrEmpty(roleCode) ? "viewer" : roleCode,
        };
        _upserted[email] = user;
        return user;
    }

    // 2026-08-29: saas refresh_token 进程内缓存 (MenuSnapshotCache miss reload 用)。
    public void SetSaasRefreshToken(string userId, string saasRefreshToken)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(saasRefreshToken)) return;
        _saasRefreshTokens[userId] = saasRefreshToken;
    }

    public string? GetSaasRefreshToken(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;
        return _saasRefreshTokens.TryGetValue(userId, out var t) ? t : null;
    }
}
