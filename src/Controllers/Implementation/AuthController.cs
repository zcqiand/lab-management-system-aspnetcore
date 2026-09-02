namespace Lab.AspNetCore.Controllers.Implementation;

using Lab.AspNetCore.Auth.State;
using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// M00.F01/F02 + M01.F04/F05 — 认证域（B1，真后端）。
/// 薄层：从 HttpContext.User 取 claims + 写 Set-Cookie 头（state CSRF）。业务在 AuthService;Controller 仅转发。
/// </summary>
[ApiController]
public sealed class AuthController(AuthService service) : AuthControllerBase
{
    private readonly AuthService _service = service;

    public override Task<LoginResponse> Login([FromBody] LoginRequest body) =>
        Task.FromResult(_service.Login(body));

    public override Task Logout([FromBody] Body body) => Task.CompletedTask;

    [Authorize]
    public override Task<CurrentUserSession> GetCurrentUser() =>
        Task.FromResult(_service.Me(ReadClaims()));

    [Authorize]
    public override Task<System.Collections.Generic.ICollection<MenuNode>> GetMenus() =>
        Task.FromResult<System.Collections.Generic.ICollection<MenuNode>>(_service.Menus(ReadClaims()));

    [Authorize]
    public override Task<PermissionSet> GetPermissions() =>
        Task.FromResult(_service.Permissions());

    public override Task<LoginResponse> Refresh([FromBody] RefreshTokenRequest body) =>
        Task.FromResult(_service.Refresh(body));

    /** M01.F05.I02 — RFC 6749 §4.1.1：透传前端 state/redirect_uri，写签名 state cookie。 */
    // 参数名必须与生成基类一致（response_type 等 snake_case）：模型绑定按实现方法
    // 的参数名取 query，改成 responseType 会让前端发的 ?response_type= 绑不上 → 400。
    public override Task<SsoRedirect> SsoAuthorize(
        [FromQuery] OAuthResponseType response_type,
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string state)
    {
        var result = _service.SsoAuthorize(redirect_uri, state);
        AppendStateCookie(result.CookieValue);
        return Task.FromResult(result.Redirect);
    }

    /** M01.F05.I03 — 从 cookie 拿 state,跟 body.state 一起交给 service 校验。 */
    public override Task<LoginResponse> SsoCallback([FromBody] SsoCallbackRequest body)
    {
        string cookieValue = Request.Cookies[StateCookieManager.CookieName] ?? "";
        return Task.FromResult(_service.SsoCallback(body, cookieValue));
    }

    [Authorize]
    public override Task<LoginResponse> SwitchTenant([FromBody] SwitchTenantRequest body) =>
        Task.FromResult(_service.SwitchTenant(ReadClaims(), body));

    private IReadOnlyDictionary<string, object> ReadClaims() =>
        User.Claims.ToDictionary(c => c.Type, c => (object)c.Value);

    private void AppendStateCookie(string cookieValue)
    {
        var opts = new CookieOptions
        {
            HttpOnly = true,
            // 跨源 cookie（5202 前端 ↔ 5204 后端）必须 SameSite=None + Secure：
            // Lax 不随跨站 XHR POST 携带，None 要求 Secure（localhost 是浏览器
            // 可信上下文，http://localhost 可写 Secure cookie）。dev/prod 统一。
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/api/auth/sso/callback",
            MaxAge = TimeSpan.FromSeconds(300),
        };
        Response.Cookies.Append(StateCookieManager.CookieName, cookieValue, opts);
    }
}
