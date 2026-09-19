namespace Lab.AspNetCore.Tests.TestDoubles;

using Lab.AspNetCore.Auth.Sso;

/// <summary>
/// Noop SSO 测试替身（2026-09-20 人裁 no-sso profile 全家族删除后，从 src/Auth/Sso 迁入
/// 测试项目 —— src 恒 real 注册 HttpSaas*Client，离线模拟只属于测试替身）。
/// 语义不变：固定 admin session + 单租户 + 空菜单树 + ACME 平台租户种子。
/// </summary>
public sealed class NoopSaasAuthClient : ISaasAuthClient
{
    public Task<AuthorizeCodeResponse> AuthorizeAsync(string redirectUri, string scope, string state, CancellationToken ct = default)
    {
        return Task.FromResult(new AuthorizeCodeResponse { Code = "dev-code", State = state });
    }

    public Task<TokenResponse> TokenAsync(string grantType, string? code, string? refreshToken, string? redirectUri, CancellationToken ct = default)
    {
        return Task.FromResult(new TokenResponse
        {
            AccessToken = "dev-access-token",
            RefreshToken = "dev-refresh-token",
            TokenType = "Bearer",
            ExpiresIn = 3600,
            Scope = "openid",
        });
    }

    /// noop：与 TokenAsync 同款假 accessToken（服务账号快照路径走通，NoopSaasMeClient 返回空树）
    public Task<TokenResponse> ServiceLoginAsync(string username, string password, CancellationToken ct = default)
    {
        return Task.FromResult(new TokenResponse
        {
            AccessToken = "dev-service-access-token",
            RefreshToken = "dev-service-refresh-token",
            TokenType = "Bearer",
            ExpiresIn = 3600,
            Scope = "openid",
        });
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

    /// noop：与 saas_dev 种子同值（id -001 = ACME Corp / acme），演练 name/tenantKey 注入而非 UUID 充名。
    public Task<List<SaasPlatformTenant>> ListPlatformTenantsAsync(string saasAccessToken, CancellationToken ct = default)
    {
        return Task.FromResult(new List<SaasPlatformTenant>
        {
            new() { Id = "00000000-0000-0000-0000-000000000001", Name = "ACME Corp", TenantKey = "acme" },
        });
    }
}
