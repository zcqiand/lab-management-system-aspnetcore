namespace Lab.AspNetCore.Tests.Services;

using System.Security.Authentication;
using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Directory;
using Lab.AspNetCore.Services;
using Xunit;

/// <summary>
/// B1 认证域 fnTest。语义基准：lab-msw handlers-extra.ts + lab-springboot AuthServiceTest
/// （DEMO_USER admin / 3 租户 / 11 权限 / dev alg=none JWT）。
/// </summary>
public class AuthServiceTest
{
    private readonly AuthService _service =
        new(new ConfigUserDirectory("dev123456"), "http://localhost:3000");

    // === M01.F05.I01 密码登录 ===

    [Fact]
    [Trait("Fn", "M01.F05.I01")]
    public void Login_success_returnsSessionWithTenants()
    {
        var res = _service.Login(new LoginRequest { Username = "admin", Password = "dev123456" });

        Assert.Equal("USER-A", res.User.Id);
        Assert.Equal("admin", res.User.Username);
        Assert.Equal(3, res.Tenants.Count);
        Assert.Equal("TENANT-001", res.Tenants[0].TenantId);
        Assert.NotNull(res.Token);
        Assert.StartsWith("refresh-admin-", res.RefreshToken);
    }

    [Fact]
    [Trait("Fn", "M01.F05.I01")]
    public void Login_wrongPassword_throws()
    {
        Assert.Throws<AuthenticationException>(
            () => _service.Login(new LoginRequest { Username = "admin", Password = "wrong" }));
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
        var login = _service.Login(new LoginRequest { Username = "admin", Password = "dev123456" });
        var res = _service.Refresh(new RefreshTokenRequest { RefreshToken = login.RefreshToken });

        // dev token/refreshToken 都是秒级 epoch，同秒内可能同串 —— 只断言语义：
        // 刷新后拿到完整会话（用户/租户/token 三件套）
        Assert.Equal("admin", res.User.Username);
        Assert.Equal(3, res.Tenants.Count);
        Assert.NotEmpty(res.Token);
        Assert.Matches(@"^refresh-admin-\d+$", res.RefreshToken);
    }

    [Fact]
    [Trait("Fn", "M01.F05.I04")]
    public void Refresh_malformedToken_throws()
    {
        Assert.Throws<AuthenticationException>(
            () => _service.Refresh(new RefreshTokenRequest { RefreshToken = "garbage" }));
        Assert.Throws<AuthenticationException>(
            () => _service.Refresh(new RefreshTokenRequest { RefreshToken = "refresh-bad-user-123" }));
    }

    // === M00.F01.I01 当前会话 ===

    [Fact]
    [Trait("Fn", "M00.F01.I01")]
    public void Me_withTenantClaim_resolvesCurrentTenant()
    {
        var claims = new Dictionary<string, object> { ["sub"] = "admin", ["tenant_id"] = "TENANT-002" };
        var session = _service.Me(claims);

        Assert.Equal("admin", session.User.Username);
        Assert.Equal("TENANT-002", session.CurrentTenantId);
        Assert.Equal(3, session.Tenants.Count);
    }

    [Fact]
    [Trait("Fn", "M00.F01.I01")]
    public void Me_withoutTenantClaim_fallsBackToDefaultTenant()
    {
        var claims = new Dictionary<string, object> { ["sub"] = "admin" };
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
        var claims = new Dictionary<string, object> { ["sub"] = "admin" };
        var res = _service.SwitchTenant(claims, new SwitchTenantRequest { TenantId = "TENANT-003" });

        // token payload 里带 tenant_id claim（解 b64url 中段验证）
        var payload = res.Token.Split('.')[1];
        Assert.Contains("\"tenant_id\":\"TENANT-003\"", DecodeB64Url(payload));
    }

    [Fact]
    [Trait("Fn", "M00.F02.I01")]
    public void SwitchTenant_unknownTenant_throws()
    {
        var claims = new Dictionary<string, object> { ["sub"] = "admin" };
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
        // 试验过程 7 子项（镜像 msw）
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
    public void SsoAuthorize_buildsSaasLoginUrl()
    {
        var res = _service.SsoAuthorize("/receipts");

        Assert.Equal("http://localhost:3000/login?redirect=/receipts&state=mock-state", res.AuthorizeUrl);
        Assert.Equal("mock-state", res.State);
    }

    [Fact]
    [Trait("Fn", "M01.F05.I03")]
    public void SsoCallback_devIssuesDemoSession()
    {
        var res = _service.SsoCallback();

        Assert.Equal("admin", res.User.Username);
        Assert.Equal(3, res.Tenants.Count);
        Assert.NotNull(res.Token);
    }

    // === M01.F05.I05 登出 ===

    [Fact]
    [Trait("Fn", "M01.F05.I05")]
    public void Logout_stateless_noop()
    {
        // 无状态 JWT：服务端无 session store，logout 是 no-op（前端清存储）
        _service.Logout();
    }

    private static string DecodeB64Url(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
