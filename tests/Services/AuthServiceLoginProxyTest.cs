namespace Lab.AspNetCore.Tests.Services;

using System.Security.Authentication;
using System.Text;
using Lab.AspNetCore.Auth.Jwt;
using Lab.AspNetCore.Auth.Sso;
using Lab.AspNetCore.Auth.State;
using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Directory;
using Lab.AspNetCore.Services;
using Lab.AspNetCore.Tests.TestDoubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// 登录账密校验代理 saas IdP 单测（REQ-2026-023 T-3，M01.F05.I01/I06 同源 service.login）。
/// 锁五仓统一语义：出站 UserLoginAsync（clientId=LAB_SAAS_CLIENT_ID，账密原值透传）；
/// saas 200 → user/tenants 映射自 saas 响应（租户名富化 best-effort 降级 name=tenantId）；
/// lab 自家 JWT 重签（tenant_id=saas currentTenantId ?? 首租户）；refresh 维持 dev-placeholder；
/// 错误映射：saas 4xx → AuthenticationException(401)，5xx/不可达 → SaasIdpUnavailableException(502 SAAS_UNAVAILABLE)。
/// </summary>
public class AuthServiceLoginProxyTest
{
    private const string Secret = "test-lab-jwt-secret-test-lab-jwt-secret-test-lab-jwt-secret"; // ≥32B

    /// <summary>与 AuthServiceTest.CreateOpts 同路径：flat env 经 InMemory Configuration 注入。</summary>
    private static LabOptions CreateOpts()
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
        return opts;
    }

    /// <summary>记录出站账密、可注错的 UserLoginAsync 替身。</summary>
    private sealed class RecordingSaasAuthClient : NoopSaasAuthClient
    {
        public string? LastUsername;
        public string? LastPassword;
        public Exception? Failure;
        public SaasLoginResponse Response = LoginBody();

        public override Task<SaasLoginResponse> UserLoginAsync(
            string username, string password, CancellationToken ct = default)
        {
            LastUsername = username;
            LastPassword = password;
            if (Failure is not null)
            {
                throw Failure;
            }
            return Task.FromResult(Response);
        }
    }

    /// <summary>saas 密码登录 200 响应替身（user + availableTenants，无 currentTenantId → 落首租户）。</summary>
    private static SaasLoginResponse LoginBody()
    {
        return new SaasLoginResponse
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
                new() { TenantId = "TENANT-002", RoleIds = new() { "technician" }, Status = "active" },
            },
            AccessToken = "saas-access-token",
            RefreshToken = "saas-refresh-token",
            UserId = "saas-user-alice",
        };
    }

    /// <summary>平台租户列表替身：TENANT-001 富化真名/tenantKey，TENANT-002 不在列表（降级路径）。</summary>
    private sealed class EnrichingMeClient : NoopSaasMeClient
    {
        public override Task<List<SaasPlatformTenant>> ListPlatformTenantsAsync(
            string saasAccessToken, CancellationToken ct = default)
        {
            return Task.FromResult(new List<SaasPlatformTenant>
            {
                new() { Id = "TENANT-001", Name = "市住建工程质量检测中心", TenantKey = "city-lab" },
            });
        }
    }

    private static AuthService NewService(
        ISaasAuthClient saasAuth, ISaasMeClient? saasMe = null)
    {
        return new AuthService(
            new ConfigUserDirectory(),
            new LabJwtSigner(Secret, "lab-test", 3600, 604800),
            saasAuth,
            saasMe ?? new NoopSaasMeClient(),
            new StateCookieManager(Secret),
            Options.Create(CreateOpts()));
    }

    [Fact]
    [Trait("Fn", "M01.F05.I01")]
    public void Login_success_mapsSaasIdentityAndTenants_resignsLabJwt()
    {
        var saasAuth = new RecordingSaasAuthClient();
        var jwt = new LabJwtSigner(Secret, "lab-test", 3600, 604800);
        var res = NewService(saasAuth, new EnrichingMeClient())
            .Login(new LoginRequest { Username = "alice", Password = "pw-123" });

        // 出站透传：账密原值进 UserLoginAsync
        Assert.Equal("alice", saasAuth.LastUsername);
        Assert.Equal("pw-123", saasAuth.LastPassword);
        // user 映射自 saas（id/username 直取；roleCode=首租户 roleIds[0]）
        Assert.Equal("saas-user-alice", res.User.Id);
        Assert.Equal("alice", res.User.Username);
        Assert.Equal("admin", res.User.RoleCode);
        // tenants 四字段 + 富化真名/tenantKey（TENANT-002 不在平台列表 → 降级 tenantId）
        Assert.Equal(2, res.Tenants.Count);
        Assert.Equal("TENANT-001", res.Tenants[0].TenantId);
        Assert.Equal("city-lab", res.Tenants[0].Code);
        Assert.Equal("市住建工程质量检测中心", res.Tenants[0].Name);
        Assert.Equal("TENANT-002", res.Tenants[1].Code);
        Assert.Equal("TENANT-002", res.Tenants[1].Name);
        // lab 自家 JWT 重签：可被本仓 signer 验签，tenant_id 落首租户
        var claims = jwt.Verify(res.Token);
        Assert.Equal("saas-user-alice", claims["sub"]);
        Assert.Equal("TENANT-001", claims["tenant_id"]);
        // refresh 维持 dev-placeholder（不透传 saas refreshToken）
        Assert.Contains(
            "\"saas_refresh_token\":\"dev-placeholder\"",
            DecodeJwtPayload(res.RefreshToken));
    }

    [Fact]
    [Trait("Fn", "M01.F05.I01")]
    public void Login_tenantNameLookupFails_degradesAndStillSucceeds()
    {
        var saasMe = new FailingPlatformTenantsMeClient();
        var res = NewService(new RecordingSaasAuthClient(), saasMe)
            .Login(new LoginRequest { Username = "alice", Password = "pw-123" });

        Assert.Equal(2, res.Tenants.Count);
        Assert.Equal("TENANT-001", res.Tenants[0].Code);
        Assert.Equal("TENANT-001", res.Tenants[0].Name);
        Assert.NotEmpty(res.Token);
    }

    [Fact]
    [Trait("Fn", "M01.F05.I01")]
    public void Login_saas4xx_throwsAuthenticationException_invalidCredentials()
    {
        var saasAuth = new RecordingSaasAuthClient
        {
            Failure = new SaasAuthException.InvalidGrant("saas 401 wrong password"),
        };

        Assert.Throws<AuthenticationException>(
            () => NewService(saasAuth).Login(new LoginRequest { Username = "alice", Password = "wrong" }));
    }

    [Fact]
    [Trait("Fn", "M01.F05.I01")]
    public void Login_saasUnavailable_throwsSaasIdpUnavailable_502()
    {
        var saasAuth = new RecordingSaasAuthClient
        {
            Failure = new SaasAuthException.UpstreamUnavailable("saas connect failed"),
        };

        Assert.Throws<SaasIdpUnavailableException>(
            () => NewService(saasAuth).Login(new LoginRequest { Username = "alice", Password = "pw-123" }));
    }

    private sealed class FailingPlatformTenantsMeClient : NoopSaasMeClient
    {
        public override Task<List<SaasPlatformTenant>> ListPlatformTenantsAsync(
            string saasAccessToken, CancellationToken ct = default)
        {
            throw new SaasAuthException.UpstreamUnavailable("saas /admin/tenants 5xx");
        }
    }

    private static string DecodeJwtPayload(string token)
    {
        var seg = token.Split('.')[1];
        var padded = seg.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    // === REQ-2026-023 live 回归（2026-10-11 五目标同时失败指纹） ===
    // 现场：登录 sub=saas user id / tenant_id claim=saas 租户，但登录链不落
    // SsoCallback 同款 Upsert + membership 快照 → Me/SwitchTenant ResolveUser
    // 「unknown user」401；SwitchTenant 归属校验写死 demo 字面量 → saas 租户 404。

    /// <summary>saas 真实租户是 UUID 形（live 实测），刻意不用 TENANT-00x 字面量——会与 demo 目录撞键掩盖失配。</summary>
    private static SaasLoginResponse SaasUuidLoginBody()
    {
        return new SaasLoginResponse
        {
            User = new SaasLoginUser
            {
                Id = "00000000-0000-0000-0000-b00000000001",
                Username = "alice",
                Email = "alice@saas.local",
            },
            AvailableTenants = new List<SaasAvailableTenant>
            {
                new() { TenantId = "00000000-0000-0000-0000-000000000001", RoleIds = new() { "admin" }, Status = "active" },
                new() { TenantId = "00000000-0000-0000-0000-000000000002", RoleIds = new() { "technician" }, Status = "active" },
            },
            AccessToken = "saas-access-token",
            RefreshToken = "saas-refresh-token",
            UserId = "00000000-0000-0000-0000-b00000000001",
        };
    }

    [Fact]
    [Trait("Fn", "M00.F01.I01")]
    public void Login_sessionCoherence_meAndSwitchTenant_stayInSaasTenantSystem()
    {
        var saasAuth = new RecordingSaasAuthClient { Response = SaasUuidLoginBody() };
        var svc = NewService(saasAuth, new EnrichingMeClient());
        var res = svc.Login(new LoginRequest { Username = "alice", Password = "pw-123" });
        var sub = res.User!.Id;
        var tid = res.Tenants[0].TenantId;

        // Me：ResolveUser 必须能解析密码登录签发的 sub；租户与登录响应同体系
        var me = svc.Me(new Dictionary<string, object> { ["sub"] = sub });
        Assert.Equal(tid, me.Tenants[0].TenantId);
        Assert.Equal(res.Tenants.Count, me.Tenants.Count);

        // SwitchTenant：登录响应里的租户可切 → 200 + 3 段真 JWT
        var switched = svc.SwitchTenant(
            new Dictionary<string, object> { ["sub"] = sub },
            new SwitchTenantRequest { TenantId = tid });
        Assert.Equal(3, switched.Token.Split('.').Length);
    }
}
