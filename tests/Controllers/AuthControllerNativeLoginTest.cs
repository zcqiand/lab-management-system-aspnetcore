namespace Lab.AspNetCore.Tests.Controllers;

using System.Security.Authentication;
using Lab.AspNetCore.Auth.Jwt;
using Lab.AspNetCore.Auth.Sso;
using Lab.AspNetCore.Auth.State;
using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Directory;
using Lab.AspNetCore.Controllers.Implementation;
using Lab.AspNetCore.Services;
using Lab.AspNetCore.Tests.TestDoubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// M01.F05.I06 原生登录端点单测（REQ-2026-023）：
/// Implementation.AuthController.NativeLogin 与 web 登录同源 service.Login
/// （出站 UserLoginAsync clientId=LAB_SAAS_CLIENT_ID 账密原值透传；
/// saas 4xx→AuthenticationException(401)，5xx/不可达→SaasIdpUnavailableException(502)）。
/// service 层语义已在 AuthServiceLoginProxyTest 全量覆盖（I01/I06 同源），
/// 此处锚定端点层委派契约与错误映射透传。
/// </summary>
public class AuthControllerNativeLoginTest
{
    private const string Secret = "test-lab-jwt-secret-test-lab-jwt-secret-test-lab-jwt-secret"; // ≥32B

    /// <summary>记录出站账密、可注错的 UserLoginAsync 替身（同 AuthServiceLoginProxyTest 形状）。</summary>
    private sealed class RecordingSaasAuthClient : NoopSaasAuthClient
    {
        public string? LastUsername;
        public string? LastPassword;
        public Exception? Failure;

        public override Task<SaasLoginResponse> UserLoginAsync(
            string username, string password, CancellationToken ct = default)
        {
            LastUsername = username;
            LastPassword = password;
            if (Failure is not null)
            {
                throw Failure;
            }
            return Task.FromResult(new SaasLoginResponse
            {
                User = new SaasLoginUser
                {
                    Id = "saas-user-alice",
                    Username = "alice",
                    Email = "alice@saas.local",
                },
                AvailableTenants = new List<SaasAvailableTenant>
                {
                    new() { TenantId = "TENANT-001", RoleIds = new() { "admin" }, Status = "active" },
                },
                AccessToken = "saas-access-token",
                RefreshToken = "saas-refresh-token",
                UserId = "saas-user-alice",
            });
        }
    }

    private static AuthController CreateController(RecordingSaasAuthClient saasAuth)
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
            new ConfigUserDirectory(),
            new LabJwtSigner(Secret, "lab-test", 3600, 604800),
            saasAuth,
            new NoopSaasMeClient(),
            new StateCookieManager(Secret),
            Options.Create(opts));
        return new AuthController(service);
    }

    [Fact]
    [Trait("Fn", "M01.F05.I06")]
    public async Task NativeLogin_success_delegatesToServiceLogin_passthroughCredentials()
    {
        var saasAuth = new RecordingSaasAuthClient();
        var res = await CreateController(saasAuth)
            .NativeLogin(new LoginRequest { Username = "alice", Password = "pw-123" });

        // 出站透传：账密原值进 UserLoginAsync（clientId 由客户端配置装配）
        Assert.Equal("alice", saasAuth.LastUsername);
        Assert.Equal("pw-123", saasAuth.LastPassword);
        // user 映射自 saas，lab 自家 JWT 重签
        Assert.Equal("saas-user-alice", res.User.Id);
        Assert.False(string.IsNullOrWhiteSpace(res.Token));
    }

    [Fact]
    [Trait("Fn", "M01.F05.I06")]
    public async Task NativeLogin_saas4xx_throwsAuthenticationException_invalidCredentials()
    {
        var saasAuth = new RecordingSaasAuthClient
        {
            Failure = new SaasAuthException.InvalidGrant("saas 401 wrong password"),
        };

        await Assert.ThrowsAsync<AuthenticationException>(
            () => CreateController(saasAuth)
                .NativeLogin(new LoginRequest { Username = "alice", Password = "wrong" }));
    }

    [Fact]
    [Trait("Fn", "M01.F05.I06")]
    public async Task NativeLogin_saasUnavailable_throwsSaasIdpUnavailable_502()
    {
        var saasAuth = new RecordingSaasAuthClient
        {
            Failure = new SaasAuthException.UpstreamUnavailable("saas connect failed"),
        };

        await Assert.ThrowsAsync<SaasIdpUnavailableException>(
            () => CreateController(saasAuth)
                .NativeLogin(new LoginRequest { Username = "alice", Password = "pw-123" }));
    }
}
