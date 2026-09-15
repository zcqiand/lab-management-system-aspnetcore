namespace Lab.AspNetCore.Auth.Sso;

using System.Net.Http.Headers;
using System.Net.Http.Json;

/// <summary>
/// SaasMeClient — saas /me/whoami + /me/tenants 调用。
/// </summary>
public interface ISaasMeClient
{
    Task<SaasCurrentUser> WhoamiAsync(string saasAccessToken, CancellationToken ct = default);
    Task<List<SaasTenantMembership>> ListMyTenantsAsync(string saasAccessToken, CancellationToken ct = default);
    /// <summary>saas /me/menus?appCode=...：当前用户在指定 app 下的有效菜单树（EffectiveMenuNode）。</summary>
    Task<List<SaasMenuNode>> ListMyMenusAsync(string saasAccessToken, string appCode, CancellationToken ct = default);
    /// <summary>
    /// 2026-09-15 租户显示名：saas GET /api/v1/admin/tenants 平台租户列表（guard 只验 JWT——
    /// 任何登录用户可读）。memberships 契约只有 tenantId 不带名字，SSO/refresh 瞬时持
    /// accessToken 时调本方法建 tenantId→{name, tenantKey} 映射填真名（lab-nextjs 同款修复）。
    /// </summary>
    Task<List<SaasPlatformTenant>> ListPlatformTenantsAsync(string saasAccessToken, CancellationToken ct = default);
}

/// <summary>saas 平台租户行（id/name/tenantKey）。name/tenantKey 用于 memberships 的 tenantId→显示名映射。</summary>
public sealed class SaasPlatformTenant
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? TenantKey { get; set; }
}

/// <summary>saas GET /api/v1/admin/tenants 分页壳（与家族 list 端点约定同形）。</summary>
public sealed class SaasPlatformTenantPage
{
    public List<SaasPlatformTenant>? Items { get; set; }
    public long? Total { get; set; }
}

/// <summary>saas EffectiveMenuNode（saas /me/menus 返回形状，字段与 saas DB MenuRow 一致）。</summary>
public sealed class SaasMenuNode
{
    public string Id { get; set; } = "";

    /// <summary>
    /// saas 实际下发字段是 <c>title</c>（2026-09-14 实测 /me/menus payload：
    /// id/clientId/parentId/title/type/path/icon/sortOrder/children）。
    /// 原无注解的 Name 绑 "name" 恒反序列化为空串 → 菜单 label 全空白
    /// （与 lab-nextjs menu-snapshot.ts 同一轮 2026-09-08 /apps 重命名漂移）。
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public string Name { get; set; } = "";
    public string? Path { get; set; }
    public string? Icon { get; set; }
    public string? Type { get; set; }
    public int? SortOrder { get; set; }
    public List<SaasMenuNode>? Children { get; set; }
}

public sealed class SaasCurrentUser
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string? DisplayName { get; set; }
    public List<SaasTenantMembership>? Memberships { get; set; }
    public string? CurrentTenantId { get; set; }
}

public sealed class SaasTenantMembership
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string TenantId { get; set; } = "";
    public List<string> RoleIds { get; set; } = new();
    public string Status { get; set; } = "";
    public string? JoinedAt { get; set; }
}

public sealed class HttpSaasMeClient : ISaasMeClient
{
    private readonly HttpClient _http;

    public HttpSaasMeClient(HttpClient http, Microsoft.Extensions.Options.IOptions<Lab.AspNetCore.Auth.Jwt.LabOptions> opts)
    {
        _http = http;
        // ADR-0019：SaasBase 缺失且 BaseAddress 未设时 throw,允许测试用 BaseAddress mock。
        if (_http.BaseAddress is null && string.IsNullOrEmpty(opts.Value.Sso.SaasBase))
        {
            throw new InvalidOperationException(
                "LAB_SAAS_BASE_URL required for SaasMeClient (ADR-0019 禁字面默认值). " +
                "Set in appsettings.Development.json (dev) or env (prod), or pass HttpClient with BaseAddress.");
        }
        _http.BaseAddress ??= new Uri(opts.Value.Sso.SaasBase);
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<SaasCurrentUser> WhoamiAsync(string saasAccessToken, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", saasAccessToken);
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SaasCurrentUser>(cancellationToken: ct)
            ?? throw new SaasAuthException.UpstreamUnavailable("saas /me returned empty");
    }

    public async Task<List<SaasTenantMembership>> ListMyTenantsAsync(string saasAccessToken, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me/tenants");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", saasAccessToken);
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var arr = await resp.Content.ReadFromJsonAsync<SaasTenantMembership[]>(cancellationToken: ct);
        return arr?.ToList() ?? new List<SaasTenantMembership>();
    }

    public async Task<List<SaasMenuNode>> ListMyMenusAsync(string saasAccessToken, string appCode, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/me/menus?appCode={Uri.EscapeDataString(appCode)}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", saasAccessToken);
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        // saas /me/menus 按 shared 契约（saas-shared tsp `Record<EffectiveMenuNode[]>`）
        // 返 `{appCode: [EffectiveMenuNode...]}` map。2026-08-27 复现 prod bug：
        // 原实现反序列化为 `SaasMenuNode[]` 抛 JsonException → CacheMenus catch
        // 不写快照 → Menus() miss 503。本修法：反序列化为 Dictionary，按 appCode
        // 查表返 list；map 缺 appCode 返空（不要写零长度假数组进缓存 — 与 msw noop 一致）。
        var map = await resp.Content.ReadFromJsonAsync<Dictionary<string, List<SaasMenuNode>>>(cancellationToken: ct);
        if (map is null || !map.TryGetValue(appCode, out var list) || list is null)
        {
            return new List<SaasMenuNode>();
        }
        return list;
    }

    public async Task<List<SaasPlatformTenant>> ListPlatformTenantsAsync(string saasAccessToken, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/tenants?page=0&pageSize=100");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", saasAccessToken);
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var page = await resp.Content.ReadFromJsonAsync<SaasPlatformTenantPage>(cancellationToken: ct);
        return page?.Items ?? new List<SaasPlatformTenant>();
    }
}

public sealed class NoopSaasMeClient : ISaasMeClient
{
    /// <summary>
    /// 2026-09-03 租户体系对齐：saas 侧租户用 UUID 体系（与 prod 一致），
    /// 与 lab demo 目录（TENANT-00x）可区分 —— 否则 Me() 对齐测试区分不出两套体系。
    /// whoami id 也改为 UUID（SSO upsert 用户 sub=UUID ≠ DemoUser USER-A）。
    /// </summary>
    public Task<SaasCurrentUser> WhoamiAsync(string saasAccessToken, CancellationToken ct = default)
    {
        return Task.FromResult(new SaasCurrentUser
        {
            Id = "00000000-0000-0000-0000-b00000000001",
            Email = "admin@lab.local",
            DisplayName = "管理员",
            CurrentTenantId = "00000000-0000-0000-0000-000000000001",
            Memberships = new List<SaasTenantMembership>
            {
                new() { Id = "mem-1", UserId = "00000000-0000-0000-0000-b00000000001", TenantId = "00000000-0000-0000-0000-000000000001", RoleIds = new() { "admin" }, Status = "active" },
            },
        });
    }

    public Task<List<SaasTenantMembership>> ListMyTenantsAsync(string saasAccessToken, CancellationToken ct = default)
    {
        return Task.FromResult(new List<SaasTenantMembership>
        {
            new() { Id = "mem-1", UserId = "00000000-0000-0000-0000-b00000000001", TenantId = "00000000-0000-0000-0000-000000000001", RoleIds = new() { "admin" }, Status = "active" },
        });
    }

    /// noop：空菜单树（快照写入空树，Menus() 命中不抛 -- 与 springboot noop 同语义）
    public Task<List<SaasMenuNode>> ListMyMenusAsync(string saasAccessToken, string appCode, CancellationToken ct = default)
    {
        return Task.FromResult(new List<SaasMenuNode>());
    }

    /// noop：与 saas_dev 种子同值（id -001 = ACME Corp / acme），让 no-sso
    /// profile 也能演练 name/tenantKey 注入而非 UUID 充名（springboot Noop 同款）。
    public Task<List<SaasPlatformTenant>> ListPlatformTenantsAsync(string saasAccessToken, CancellationToken ct = default)
    {
        return Task.FromResult(new List<SaasPlatformTenant>
        {
            new() { Id = "00000000-0000-0000-0000-000000000001", Name = "ACME Corp", TenantKey = "acme" },
        });
    }
}
