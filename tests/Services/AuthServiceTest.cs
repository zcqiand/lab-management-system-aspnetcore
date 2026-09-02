namespace Lab.AspNetCore.Tests.Services;

using System.Security.Authentication;
using Lab.AspNetCore.Auth.Jwt;
using Lab.AspNetCore.Auth.Sso;
using Lab.AspNetCore.Auth.State;
using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Directory;
using Lab.AspNetCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// B1 认证域 fnTest（真后端）。用 NoopSaasAuthClient/NoopSaasMeClient,无需 saas 联通。JWT 走真 HMAC HS256。
/// </summary>
public class AuthServiceTest
{
    private const string Secret = "test-lab-jwt-secret-test-lab-jwt-secret-test-lab-jwt-secret"; // ≥32B

    private static readonly LabOptions Opts = CreateOpts();

    /// <summary>
    /// 2026-08-28 key 统一后 SSO 属性 getter 直读 flat env(LAB_SAAS_*/LAB_SSO_*),
    /// 测试经 InMemory Configuration 注入(与 Program.cs PostConfigure 组合根同路径),
    /// 不再直接赋值只读属性。
    /// </summary>
    private static LabOptions CreateOpts()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LAB_SAAS_BASE_URL"] = "http://localhost:3000",
                ["LAB_SAAS_CLIENT_ID"] = "test-client-id",
                ["LAB_SAAS_CLIENT_SECRET"] = "test-client-secret",
                ["LAB_SAAS_DEFAULT_TENANT_ID"] = "00000000-0000-0000-0000-000000000001",
                ["LAB_SSO_CALLBACK_REDIRECT"] = "http://localhost:5080/api/auth/sso/callback",
            })
            .Build();
        var opts = new LabOptions();
        opts.Config = config;
        opts.Sso.Config = config;
        return opts;
    }

    private readonly AuthService _service = new(
        new ConfigUserDirectory("dev123456"),
        new LabJwtSigner(Secret, "lab-test", 3600, 604800),
        new NoopSaasAuthClient(),
        new NoopSaasMeClient(),
        new StateCookieManager(Secret),
        Microsoft.Extensions.Options.Options.Create(Opts));

    // === M01.F05.I01 密码登录 ===

    [Fact]
    [Trait("Fn", "M01.F05.I01")]
    public void Login_success_returnsSessionWithTenants()
    {
        var res = _service.Login(new LoginRequest { Username = "alice", Password = "dev123456" });
        Assert.Equal("USER-A", res.User.Id);
        Assert.Equal("alice", res.User.Username);
        Assert.Equal(3, res.Tenants.Count);
        Assert.Equal("TENANT-001", res.Tenants[0].TenantId);
        Assert.NotNull(res.Token);
        Assert.NotNull(res.RefreshToken);
    }

    [Fact]
    [Trait("Fn", "M01.F05.I01")]
    public void Login_wrongPassword_throws()
    {
        Assert.Throws<AuthenticationException>(
            () => _service.Login(new LoginRequest { Username = "alice", Password = "wrong" }));
    }

    [Fact]
    [Trait("Fn", "M01.F05.I01")]
    public void Login_missingFields_throws()
    {
        Assert.Throws<ArgumentException>(
            () => _service.Login(new LoginRequest { Username = "", Password = "" }));
    }

    // === M01.F05.I04 刷新 token ===

    [Fact]
    [Trait("Fn", "M01.F05.I04")]
    public void Refresh_validToken_returnsNewSession()
    {
        var login = _service.Login(new LoginRequest { Username = "alice", Password = "dev123456" });
        var res = _service.Refresh(new RefreshTokenRequest { RefreshToken = login.RefreshToken });

        Assert.Equal("USER-A", res.User.Id);
        Assert.Equal(3, res.Tenants.Count);
        Assert.NotEmpty(res.Token);
        Assert.NotEmpty(res.RefreshToken);
    }

    [Fact]
    [Trait("Fn", "M01.F05.I04")]
    public void Refresh_malformedToken_throws()
    {
        Assert.Throws<AuthenticationException>(
            () => _service.Refresh(new RefreshTokenRequest { RefreshToken = "garbage" }));
    }

    // === M00.F01.I01 当前会话 ===

    [Fact]
    [Trait("Fn", "M00.F01.I01")]
    public void Me_withTenantClaim_resolvesCurrentTenant()
    {
        var claims = new Dictionary<string, object> { ["sub"] = "USER-A", ["tenant_id"] = "TENANT-002" };
        var session = _service.Me(claims);

        Assert.Equal("USER-A", session.User.Id);
        Assert.Equal("TENANT-002", session.CurrentTenantId);
        Assert.Equal(3, session.Tenants.Count);
    }

    [Fact]
    [Trait("Fn", "M00.F01.I01")]
    public void Me_withoutTenantClaim_fallsBackToDefaultTenant()
    {
        var claims = new Dictionary<string, object> { ["sub"] = "USER-A" };
        var session = _service.Me(claims);

        Assert.Equal("TENANT-001", session.CurrentTenantId);
    }

    [Fact]
    [Trait("Fn", "M00.F01.I01")]
    public void Me_unknownUser_throws()
    {
        var claims = new Dictionary<string, object> { ["sub"] = "ghost" };
        Assert.Throws<AuthenticationException>(() => _service.Me(claims));
    }

    // === M00.F02.I01 选租户换发 ===

    [Fact]
    [Trait("Fn", "M00.F02.I01")]
    public void SwitchTenant_validTenant_reissuesTokenWithClaim()
    {
        var claims = new Dictionary<string, object> { ["sub"] = "USER-A" };
        var res = _service.SwitchTenant(claims, new SwitchTenantRequest { TenantId = "TENANT-003" });

        var payload = res.Token.Split('.')[1];
        Assert.Contains("\"tenant_id\":\"TENANT-003\"", DecodeB64Url(payload));
    }

    [Fact]
    [Trait("Fn", "M00.F02.I01")]
    public void SwitchTenant_unknownTenant_throws()
    {
        var claims = new Dictionary<string, object> { ["sub"] = "USER-A" };
        Assert.Throws<KeyNotFoundException>(
            () => _service.SwitchTenant(claims, new SwitchTenantRequest { TenantId = "TENANT-999" }));
    }

    // === M01.F04.I01 动态菜单 / I02 权限集 ===

    [Fact]
    [Trait("Fn", "M01.F04.I01")]
    public void Menus_snapshotMiss_throwsMenusUnavailable()
    {
        // 2026-08-27 起 demo 兜底删除：无 saas 快照（快照过期/拉取失败/重启）->
        // MenusUnavailableException（Program.cs 映射 503），前端回退静态菜单
        var claims = new Dictionary<string, object> { ["sub"] = "USER-A" };
        Assert.Throws<MenusUnavailableException>(() => _service.Menus(claims));
    }

    [Fact]
    [Trait("Fn", "M01.F04.I01")]
    public void Login_passwordFlow_cachesServiceAccountSnapshot()
    {
        // 密码登录也拉快照：login() 成功后用 saas 服务账号换 token 拉 /me/menus。
        // Noop saas 返回空菜单树 -> 空快照也写入（Menus() 不再抛）
        var res = _service.Login(new LoginRequest { Username = "alice", Password = "dev123456" });
        Assert.Equal("USER-A", res.User.Id);
        // login 副作用：快照已写入 -> Menus() 命中（空树也算命中，不 503）
        var claims = new Dictionary<string, object> { ["sub"] = res.User.Id };
        Assert.Empty(_service.Menus(claims));
    }

    [Fact]
    [Trait("Fn", "M01.F04.I02")]
    public void Permissions_returnsAdminFullSet()
    {
        var perms = _service.Permissions();
        Assert.Equal(11, perms.Permissions.Count);
        Assert.Contains("*", perms.Permissions);
    }

    // === M01.F05.I02 SSO 跳转 / I03 SSO 回调 ===

    [Fact]
    [Trait("Fn", "M01.F05.I02")]
    public void SsoAuthorize_returnsAuthorizeUrlAndState()
    {
        // RFC 6749 §4.1.1：前端 state 原样透传，SsoRedirect.state 返回同一值
        // 2026-08-29: lab 后端不再代理 saas authorize 预拿 code,
        // 改成 302 跳 saas 登录页(带 redirect_uri + state)。
        var res = _service.SsoAuthorize("http://localhost:5173/login", "client-state-abc");
        Assert.NotNull(res.Redirect.AuthorizeUrl);
        Assert.Contains("/login?redirect_uri=", res.Redirect.AuthorizeUrl);
        Assert.Contains("state=client-state-abc", res.Redirect.AuthorizeUrl);
        Assert.Equal("client-state-abc", res.Redirect.State);
        Assert.NotEmpty(res.CookieValue);
    }

    [Fact]
    [Trait("Fn", "M01.F05.I03")]
    public void SsoCallback_returnsDemoSession()
    {
        var auth = _service.SsoAuthorize("http://localhost:5173/login", "client-state-abc");
        var body = new SsoCallbackRequest
        {
            Grant_type = OAuthGrantType.Authorization_code,
            Code = "dev-code",
            Redirect_uri = "http://localhost:5173/login",
            State = "client-state-abc",
        };
        var res = _service.SsoCallback(body, auth.CookieValue);

        Assert.Equal("USER-A", res.User.Id);
        Assert.Equal(3, res.Tenants.Count);
        Assert.NotNull(res.Token);
        // refresh token 嵌 saas refresh token
        var refreshPayload = res.RefreshToken!.Split('.')[1];
        Assert.Contains("\"saas_refresh_token\":\"dev-refresh-token\"", DecodeB64Url(refreshPayload));
    }

    [Fact]
    [Trait("Fn", "M01.F05.I03")]
    public void SsoCallback_mismatchedState_throws()
    {
        var auth = _service.SsoAuthorize("http://localhost:5173/login", "client-state-abc");
        var body = new SsoCallbackRequest
        {
            Grant_type = OAuthGrantType.Authorization_code,
            Code = "dev-code",
            Redirect_uri = "http://localhost:5173/login",
            State = "forged-state",
        };
        Assert.Throws<InvalidOperationException>(() => _service.SsoCallback(body, auth.CookieValue));
    }

    // === M01.F05.I05 登出 ===

    [Fact]
    [Trait("Fn", "M01.F05.I05")]
    public void Logout_stateless_noop()
    {
        _service.Logout();
    }

    private static string DecodeB64Url(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
