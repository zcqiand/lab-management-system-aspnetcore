namespace Lab.AspNetCore.Tests.Controllers;

using Lab.AspNetCore.Auth.Jwt;
using Lab.AspNetCore.Auth.Sso;
using Lab.AspNetCore.Auth.State;
using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Directory;
using Lab.AspNetCore.Controllers.Implementation;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// SSO state cookie 属性按部署形态分档（2026-09-14 浏览器实测回归）：
/// dev（http://localhost 明文）签 `Secure; SameSite=None` 会在部分浏览器下不落盘，
/// callback 恒 "missing lab_sso_state cookie" 500。localhost/127.0.0.1 → Lax 无 Secure
/// （端口不参与 same-site 计算，Lax 随 XHR 携带）；其余 host → None + Secure（https 反代）。
/// </summary>
public class AuthControllerStateCookieTest
{
    private static AuthController CreateController(string requestHost)
    {
        var config = new ConfigurationBuilder()
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
        var service = new AuthService(
            new ConfigUserDirectory("dev123456"),
            new LabJwtSigner("test-lab-jwt-secret-test-lab-jwt-secret-test-lab-jwt-secret", "lab-test", 3600, 604800),
            new NoopSaasAuthClient(),
            new NoopSaasMeClient(),
            new StateCookieManager("test-lab-jwt-secret-test-lab-jwt-secret-test-lab-jwt-secret"),
            Options.Create(opts));

        var controller = new AuthController(service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        controller.ControllerContext.HttpContext.Request.Host = new HostString(requestHost);
        return controller;
    }

    private static string SetCookieOf(AuthController controller) =>
        controller.ControllerContext.HttpContext.Response.Headers["Set-Cookie"].ToString();

    [Fact]
    public async Task LocalhostDev_setsLaxWithoutSecure()
    {
        var controller = CreateController("localhost:5204");
        await controller.SsoAuthorize(OAuthResponseType.Code, "test-client-id", "http://localhost:5201/login", "st-1");

        var cookie = SetCookieOf(controller);
        Assert.Contains("lab_sso_state=", cookie);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoopbackIpDev_setsLaxWithoutSecure()
    {
        var controller = CreateController("127.0.0.1:5204");
        await controller.SsoAuthorize(OAuthResponseType.Code, "test-client-id", "http://localhost:5201/login", "st-1");

        var cookie = SetCookieOf(controller);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicHost_setsNoneWithSecure()
    {
        var controller = CreateController("lab-aspnetcore.xiangru.uk");
        await controller.SsoAuthorize(OAuthResponseType.Code, "test-client-id", "http://localhost:5201/login", "st-1");

        var cookie = SetCookieOf(controller);
        Assert.Contains("samesite=none", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }
}
