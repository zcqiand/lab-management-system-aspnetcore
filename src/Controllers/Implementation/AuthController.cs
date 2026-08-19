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
        Task.FromResult<System.Collections.Generic.ICollection<MenuNode>>(_service.Menus());

    [Authorize]
    public override Task<PermissionSet> GetPermissions() =>
        Task.FromResult(_service.Permissions());

    public override Task<LoginResponse> Refresh([FromBody] RefreshTokenRequest body) =>
        Task.FromResult(_service.Refresh(body));

    /** M01.F05.I02 — 写 HttpOnly Secure Cookie 包装 state,body 给前端跳转 URL。 */
    public override Task<SsoRedirect> SsoAuthorize(
        [FromQuery] OAuthResponseType responseType,
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string state)
    {
        var result = _service.SsoAuthorize(redirect_uri);
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
            Secure = false, // dev:false;prod 切 true（环境变量控制）
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth/sso/callback",
            MaxAge = TimeSpan.FromSeconds(300),
        };
        Response.Cookies.Append(StateCookieManager.CookieName, cookieValue, opts);
    }
}
