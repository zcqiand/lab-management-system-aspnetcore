namespace Lab.AspNetCore.Tests.Auth.Sso;

using System.Net;
using System.Net.Http.Json;
using Lab.AspNetCore.Auth.Jwt;
using Lab.AspNetCore.Auth.Sso;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// M01.F04.I01 — ISaasMeClient.ListMyMenusAsync 响应形状适配：
/// saas /me/menus 按 shared 契约（saas-shared tsp `Record<EffectiveMenuNode[]>`
/// = `Record<string, EffectiveMenuNode[]>` map）返 `{appCode: [...]}`。
///
/// 复现 prod 报错：lab-aspnetcore 原实现反序列化为 `SaasMenuNode[]`，对 map 形状
/// 响应解成空数组 → 快照被空写入 → lab 前端 `/menus` 返 `[]`。
///
/// 修复后按 appCode 查表（找不到返空）。
/// </summary>
public class SaasMeClientTest
{
    /// <summary>HttpMessageHandler mock：固定 JSON 响应 + 记录请求。 </summary>
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

    [Fact]
    [Trait("Fn", "M01.F04.I01")]
    public async Task ListMyMenusAsync_mapShape_byAppCode_returnsList()
    {
        // saas /me/menus 按契约返 { appCode: [EffectiveMenuNode...] }
        var body = """
        {
          "lab-management": [
            { "id": "g1", "name": "资源管理", "icon": "resource",
              "children": [ { "id": "c1", "name": "合同管理", "path": "/contracts", "icon": "clipboard" } ] }
          ]
        }
        """;
        var handler = new StubHandler { ResponseBody = body };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://saas.example.com") };
        var client = new HttpSaasMeClient(http, Options.Create(new LabOptions()));

        var menus = await client.ListMyMenusAsync("dev-token", "lab-management");

        Assert.NotEmpty(menus);
        Assert.Equal("g1", menus[0].Id);
        Assert.Equal("资源管理", menus[0].Name);
        Assert.NotNull(menus[0].Children);
        Assert.Single(menus[0].Children!);
        Assert.Equal("/contracts", menus[0].Children![0].Path);
    }

    [Fact]
    [Trait("Fn", "M01.F04.I01")]
    public async Task ListMyMenusAsync_mapShape_unknownAppCode_returnsEmpty()
    {
        // saas 返了 map 但请求 appCode 不在 map 里 → 返空（不是抛错）
        var body = """
        { "some-other-app": [ { "id": "x", "name": "x" } ] }
        """;
        var handler = new StubHandler { ResponseBody = body };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://saas.example.com") };
        var client = new HttpSaasMeClient(http, Options.Create(new LabOptions()));

        var menus = await client.ListMyMenusAsync("dev-token", "lab-management");

        Assert.Empty(menus);
    }

    [Fact]
    [Trait("Fn", "M01.F04.I01")]
    public async Task ListMyMenusAsync_appCodeAsQueryString()
    {
        var handler = new StubHandler
        {
            ResponseBody = """{ "lab-management": [] }""",
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://saas.example.com") };
        var client = new HttpSaasMeClient(http, Options.Create(new LabOptions()));

        await client.ListMyMenusAsync("dev-token", "lab-management");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("/api/v1/me/menus?appCode=lab-management", handler.LastRequest!.RequestUri!.PathAndQuery);
    }
}