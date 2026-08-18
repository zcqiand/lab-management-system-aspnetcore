namespace Lab.AspNetCore.Controllers.Implementation;

using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// M00.F01/F02 + M01.F04/F05 — 认证域（B1），9 端点。
/// 薄层：从 HttpContext.User 取 claims 交给 service（禁止在这里写业务逻辑）。
/// 路由由生成基类的 [Route]/[HttpXxx] 提供；这里只 override 方法体。
/// 无全局 [Authorize] filter：受保护端点显式 [Authorize]，其余默认匿名（镜像
/// spring SecurityConfig permitAll = login/refresh/sso/** 的反向表达）。
/// </summary>
[ApiController]
public sealed class AuthController(AuthService service) : AuthControllerBase
{
    private readonly AuthService _service = service;

    // === M01.F05.I01 密码登录（匿名） ===
    public override Task<LoginResponse> Login([FromBody] LoginRequest body) =>
        Task.FromResult(_service.Login(body));

    // === M01.F05.I05 登出（匿名，无状态 JWT 服务端无 session） ===
    public override Task Logout([FromBody] Body body) => Task.CompletedTask;

    // === M00.F01.I01 当前会话（authenticated） ===
    [Authorize]
    public override Task<CurrentUserSession> GetCurrentUser() =>
        Task.FromResult(_service.Me(ReadClaims()));

    // === M01.F04.I01 动态菜单（authenticated） ===
    [Authorize]
    public override Task<System.Collections.Generic.ICollection<MenuNode>> GetMenus() =>
        Task.FromResult<System.Collections.Generic.ICollection<MenuNode>>(_service.Menus());

    // === M01.F04.I02 权限集（authenticated） ===
    [Authorize]
    public override Task<PermissionSet> GetPermissions() =>
        Task.FromResult(_service.Permissions());

    // === M01.F05.I04 刷新 token（匿名） ===
    public override Task<LoginResponse> Refresh([FromBody] RefreshTokenRequest body) =>
        Task.FromResult(_service.Refresh(body));

    // === M01.F05.I02 SSO 跳转（匿名） ===
    public override Task<SsoRedirect> SsoAuthorize([FromQuery] string redirect) =>
        Task.FromResult(_service.SsoAuthorize(redirect));

    // === M01.F05.I03 SSO 回调（匿名，dev 直发 demo 会话） ===
    public override Task<LoginResponse> SsoCallback([FromBody] SsoCallbackRequest body) =>
        Task.FromResult(_service.SsoCallback());

    // === M00.F02.I01 选租户换发（authenticated） ===
    [Authorize]
    public override Task<LoginResponse> SwitchTenant([FromBody] SwitchTenantRequest body) =>
        Task.FromResult(_service.SwitchTenant(ReadClaims(), body));

    private IReadOnlyDictionary<string, object> ReadClaims() =>
        User.Claims.ToDictionary(c => c.Type, c => (object)c.Value);
}
