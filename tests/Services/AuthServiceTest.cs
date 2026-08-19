namespace Lab.AspNetCore.Tests.Services;

using System.Security.Authentication;
using Lab.AspNetCore.Auth.Jwt;
using Lab.AspNetCore.Auth.Sso;
using Lab.AspNetCore.Auth.State;
using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Directory;
using Lab.AspNetCore.Services;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// B1 认证域 fnTest（真后端）。用 NoopSaasAuthClient/NoopSaasMeClient,无需 saas 联通。JWT 走真 HMAC HS256。
/// </summary>
public class AuthServiceTest
{
    private const string Secret = "test-lab-jwt-secret-test-lab-jwt-secret-test-lab-jwt-secret"; // ≥32B

    private static readonly LabOptions Opts = new()
    {
        Sso = new LabOptions.SsoSection
        {
            SaasBase = "http://localhost:3000",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            DefaultTenantId = "00000000-0000-0000-0000-000000000001",
            CallbackRedirectBase = "http://localhost:5080/api/auth/sso/callback",
        },
    };

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
        var res = _service.Login(new LoginRequest { Username = "admin@lab.local", Password = "dev123456" });
        Assert.Equal("USER-A", res.User.Id);
        Assert.Equal("admin@lab.local", res.User.Username);
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
            () => _service.Login(new LoginRequest { Username = "admin@lab.local", Password = "wrong" }));
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
        var login = _service.Login(new LoginRequest { Username = "admin@lab.local", Password = "dev123456" });
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
    public void Menus_returns5RootNodes()
    {
        var menus = _service.Menus();
        Assert.Equal(5, menus.Count);
        Assert.Equal("menu-dashboard", menus[0].Id);
        Assert.Equal("工作台", menus[0].Label);
        var flow = menus.First(m => m.Id == "menu-m03");
        Assert.Equal(7, flow.Children!.Count);
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
        var res = _service.SsoAuthorize("/receipts");
        Assert.NotNull(res.Redirect.AuthorizeUrl);
        Assert.Contains("code=dev-code", res.Redirect.AuthorizeUrl);
        Assert.NotNull(res.Redirect.State);
        Assert.NotEmpty(res.CookieValue);
    }

    [Fact]
    [Trait("Fn", "M01.F05.I03")]
    public void SsoCallback_returnsDemoSession()
    {
        var auth = _service.SsoAuthorize("/receipts");
        var body = new SsoCallbackRequest
        {
            Grant_type = OAuthGrantType.Authorization_code,
            Code = "dev-code",
            Redirect_uri = "http://localhost:5080/api/auth/sso/callback",
            State = auth.Redirect.State,
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
        var auth = _service.SsoAuthorize("/receipts");
        var body = new SsoCallbackRequest
        {
            Grant_type = OAuthGrantType.Authorization_code,
            Code = "dev-code",
            Redirect_uri = "http://localhost:5080/api/auth/sso/callback",
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
