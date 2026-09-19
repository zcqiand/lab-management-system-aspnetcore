namespace Lab.AspNetCore.Tests.Auth.Sso;

using System.Net;
using Lab.AspNetCore.Auth.Jwt;
using Lab.AspNetCore.Auth.Sso;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// M01.F05.I01 — HttpSaasAuthClient.ServiceLoginAsync 请求体形状：
/// saas LoginRequest = {username, password, clientId}（saas-shared tsp
/// routes/sessions.tsp，clientId 必填、无 tenantCode）。
///
/// 2026-09-19 5.33：修前 body 缺 clientId 且带契约已删的 tenantCode →
/// saas 400 fieldErrors.clientId → 菜单快照 503。
/// </summary>
public class SaasAuthClientTest
{
    /// <summary>HttpMessageHandler mock：固定 JSON 响应 + 记录请求（与 SaasMeClientTest 同款）。</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        public string ResponseBody { get; set; } = "{}";
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            LastRequest = request;
            var msg = new HttpResponseMessage(Status)
            {
                Content = new StringContent(ResponseBody),
            };
            msg.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return Task.FromResult(msg);
        }
    }

    private static (HttpSaasAuthClient Client, StubHandler Handler) NewClient()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://saas.example.com") };
        var opts = Options.Create(new LabOptions
        {
            Sso = new LabOptions.SsoSection
            {
                _saasBase = "https://saas.example.com",
                _clientId = "11111111-1111-1111-1111-111111111111",
                _clientSecret = "sec",
                _defaultTenantId = "00000000-0000-0000-0000-000000000001",
                _serviceClientId = "lab-management",
            },
        });
        return (new HttpSaasAuthClient(http, opts), handler);
    }

    [Fact]
    [Trait("Fn", "M01.F05.I01")]
    public async Task ServiceLoginAsync_bodyHasClientId_noTenantCode()
    {
        var (client, handler) = NewClient();
        handler.ResponseBody =
            """{"accessToken":"svc-at","refreshToken":"svc-rt","tokenType":"Bearer","expiresIn":3600}""";

        var resp = await client.ServiceLoginAsync("alice", "dev123456");

        Assert.Equal("svc-at", resp.AccessToken);
        Assert.NotNull(handler.LastRequest);
        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"username\":\"alice\"", body);
        // LoginRequest clientId 必填（saas sessions.tsp 契约），修前缺 clientId 致 saas 400 fieldErrors
        Assert.Contains("\"clientId\":\"lab-management\"", body);
        // tenantCode 是契约已不存在的陈旧字段，不得再发
        Assert.DoesNotContain("tenantCode", body);
    }

    [Fact]
    [Trait("Fn", "M01.F05.I01")]
    public async Task ServiceLoginAsync_postsToAuthLogin()
    {
        var (client, handler) = NewClient();
        handler.ResponseBody = """{"accessToken":"svc-at"}""";

        await client.ServiceLoginAsync("alice", "dev123456");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("/api/v1/auth/login", handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    [Fact]
    [Trait("Fn", "M01.F05.I01")]
    public void Constructor_missingServiceClientId_throws()
    {
        // 5.33：clientId 是业务身份字段（ADR-0019），缺失必须 fail-fast
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://saas.example.com") };
        var opts = Options.Create(new LabOptions
        {
            Sso = new LabOptions.SsoSection
            {
                _saasBase = "https://saas.example.com",
                _clientId = "11111111-1111-1111-1111-111111111111",
                _clientSecret = "sec",
                _defaultTenantId = "00000000-0000-0000-0000-000000000001",
                _serviceClientId = "",
            },
        });

        Assert.Throws<InvalidOperationException>(() => new HttpSaasAuthClient(http, opts));
    }
}
