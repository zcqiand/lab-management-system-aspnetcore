namespace Lab.AspNetCore.Services;

using System.Security.Authentication;
using System.Text;
using Lab.AspNetCore.Auth.Jwt;
using Lab.AspNetCore.Auth.Sso;
using Lab.AspNetCore.Auth.State;
using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Directory;
using Microsoft.Extensions.Options;

/// <summary>
/// M00.F01/F02 + M01.F04/F05 — 认证域（B1，真后端）。对齐 B1 真后端 OAuth 2.0 + JWT 方案（ADR-0008）：
/// JWT HMAC HS256（{@link LabJwtSigner}）+ 真 OAuth 2.0 authorization_code flow（{@link ISaasAuthClient}）
/// + saas /me/whoami + /me/tenants（{@link ISaasMeClient}）+ CSRF state cookie（{@link StateCookieManager}）。
/// </summary>
public sealed class AuthService
{
    internal static readonly IReadOnlyList<string> DemoPermissions = new[]
    {
        "contract:read", "contract:write", "sample:read", "sample:write",
        "report:read", "report:write", "report:issue",
        "inspection:read", "inspection:write", "audit:read", "*",
    };

    private readonly IUserDirectory _directory;
    private readonly LabJwtSigner _jwt;
    private readonly ISaasAuthClient _saasAuth;
    private readonly ISaasMeClient _saasMe;
    private readonly StateCookieManager _stateMgr;
    private readonly IOptions<LabOptions> _opts;

    public AuthService(
        IUserDirectory directory,
        LabJwtSigner jwt,
        ISaasAuthClient saasAuth,
        ISaasMeClient saasMe,
        StateCookieManager stateMgr,
        IOptions<LabOptions> opts)
    {
        _directory = directory;
        _jwt = jwt;
        _saasAuth = saasAuth;
        _saasMe = saasMe;
        _stateMgr = stateMgr;
        _opts = opts;
    }

    // === M01.F05.I01 密码登录 ===

    public LoginResponse Login(LoginRequest body)
    {
        var username = body.Username ?? "";
        var password = body.Password ?? "";
        if (username.Length == 0 || password.Length == 0)
        {
            throw new ArgumentException("username and password are required");
        }
        if (!_directory.CheckPassword(username, password))
        {
            throw new AuthenticationException("Invalid username or password");
        }
        var user = _directory.FindByUsername(username) ?? throw new AuthenticationException("Invalid username or password");
        return Session(user, null, null);
    }

    // === M01.F05.I04 刷新 token ===

    public LoginResponse Refresh(RefreshTokenRequest body)
    {
        if (body?.RefreshToken is null)
        {
            throw new AuthenticationException("missing refresh_token");
        }
        Dictionary<string, object> claims;
        try
        {
            claims = _jwt.Verify(body.RefreshToken);
        }
        catch (ArgumentException e)
        {
            throw new AuthenticationException("invalid refresh_token: " + e.Message);
        }
        if (claims.GetValueOrDefault("typ")?.ToString() != "refresh")
        {
            throw new AuthenticationException("invalid refresh_token: not a refresh token");
        }
        var tenantId = claims.GetValueOrDefault("tenant_id")?.ToString();
        var saasRefresh = claims.GetValueOrDefault("saas_refresh_token")?.ToString();
        if (string.IsNullOrEmpty(saasRefresh))
        {
            throw new AuthenticationException("invalid refresh_token: missing saas_refresh_token claim");
        }
        var t = _saasAuth.TokenAsync("refresh_token", null, saasRefresh, null).GetAwaiter().GetResult();
        var saasUser = _saasMe.WhoamiAsync(t.AccessToken).GetAwaiter().GetResult();
        var memberships = _saasMe.ListMyTenantsAsync(t.AccessToken).GetAwaiter().GetResult();
        var labUser = _directory.FindByEmail(saasUser.Email)
            ?? throw new AuthenticationException("unknown user");
        return Session(labUser, tenantId, TenantsFrom(memberships), t.RefreshToken);
    }

    // === M01.F05.I05 登出（无状态 JWT,服务端无 session store） ===

    public void Logout()
    {
        // 前端清存储;服务端无操作
    }

    // === M00.F01.I01 当前会话 ===

    public CurrentUserSession Me(IReadOnlyDictionary<string, object> claims)
    {
        var user = ResolveUser(claims);
        var currentTenantId = claims.TryGetValue("tenant_id", out var tenantClaim) && tenantClaim != null
            ? tenantClaim.ToString() ?? ""
            : _directory.DefaultTenant().TenantId;
        return new CurrentUserSession
        {
            User = user,
            Tenants = _directory.TenantsOf(user.Username).ToList(),
            CurrentTenantId = currentTenantId,
        };
    }

    // === M00.F02.I01 选租户换发 ===

    public LoginResponse SwitchTenant(IReadOnlyDictionary<string, object> claims, SwitchTenantRequest body)
    {
        var user = ResolveUser(claims);
        var tenantId = body?.TenantId ?? "";
        var target = _directory.FindByTenantId(tenantId)
            ?? throw new KeyNotFoundException("Tenant not found");
        return Session(user, target.TenantId, null);
    }

    // === M01.F04.I01 动态菜单 / I02 权限集 ===

    public List<MenuNode> Menus() => new() { /* mirror springboot */
        new() { Id = "menu-dashboard", Label = "工作台", Path = "/dashboard", Icon = "dashboard" },
        new() { Id = "menu-m02", Label = "资源管理", Icon = "resource",
            Children = new List<MenuNode> { Menu("menu-contracts", "合同管理", "/contracts") } },
        new() { Id = "menu-m03", Label = "试验过程", Icon = "flow",
            Children = new List<MenuNode> {
                Menu("menu-receipts", "接样管理", "/receipts"),
                Menu("menu-task", "任务分配", "/receipts?stage=task_assignment"),
                Menu("menu-entry", "数据录入", "/receipts?stage=data_entry"),
                Menu("menu-review", "报告审核", "/receipts?stage=review"),
                Menu("menu-approve", "报告批准", "/receipts?stage=approval"),
                Menu("menu-issue", "报告发放", "/receipts?stage=issuance"),
                Menu("menu-archive", "报告归档", "/receipts?stage=archived"),
            } },
        new() { Id = "menu-m04", Label = "基础数据", Icon = "data",
            Children = new List<MenuNode> {
                Menu("menu-techreq", "技术要求", "/technical-requirements"),
                Menu("menu-models", "型号维护", "/catalog/models"),
                Menu("menu-specs", "规格维护", "/catalog/specs"),
                Menu("menu-grades", "等级维护", "/catalog/grades"),
                Menu("menu-brands", "牌号维护", "/catalog/brands"),
            } },
        new() { Id = "menu-m05", Label = "数据统计", Icon = "stats",
            Children = new List<MenuNode> { Menu("menu-summary", "报告汇总", "/summary") } },
    };

    public PermissionSet Permissions() => new() { Permissions = DemoPermissions.ToList() };

    // === M01.F05.I02 SSO 跳转 / I03 SSO 回调 ===

    public SsoAuthResult SsoAuthorize(string businessRedirect)
    {
        var target = string.IsNullOrEmpty(businessRedirect) ? "/" : businessRedirect;
        var ss = _stateMgr.Issue(target);
        var resp = _saasAuth.AuthorizeAsync(
            _opts.Value.Sso.CallbackRedirectBase,
            "openid profile email",
            ss.Nonce).GetAwaiter().GetResult();
        var authorizeUrl = $"{_opts.Value.Sso.SaasBase}/login?code={resp.Code}&state={resp.State}&redirect_uri={_opts.Value.Sso.CallbackRedirectBase}";
        return new SsoAuthResult(
            new SsoRedirect { AuthorizeUrl = authorizeUrl, State = ss.Nonce },
            ss.CookieValue);
    }

    public LoginResponse SsoCallback(SsoCallbackRequest body, string cookieValue)
    {
        if (body == null) throw new ArgumentException("missing body");
        // verify validates cookie + state 配对;redirect 返回值只为校验 context
        _stateMgr.Verify(cookieValue, body.State);
        var redirectUri = body.Redirect_uri ?? _opts.Value.Sso.CallbackRedirectBase;
        var t = _saasAuth.TokenAsync("authorization_code", body.Code, null, redirectUri).GetAwaiter().GetResult();
        var saasUser = _saasMe.WhoamiAsync(t.AccessToken).GetAwaiter().GetResult();
        var memberships = _saasMe.ListMyTenantsAsync(t.AccessToken).GetAwaiter().GetResult();
        var labUser = _directory.FindByEmail(saasUser.Email)
            ?? _directory.Upsert(saasUser.Id, saasUser.Email, saasUser.DisplayName ?? "", "viewer");
        return Session(labUser, null, TenantsFrom(memberships), t.RefreshToken);
    }

    // === helpers ===

    private CurrentUser ResolveUser(IReadOnlyDictionary<string, object> claims)
    {
        if (!claims.TryGetValue("sub", out var sub) || sub == null)
        {
            throw new AuthenticationException("missing sub claim");
        }
        var subStr = sub.ToString() ?? "";
        return _directory.FindById(subStr)
            ?? _directory.FindByEmail(subStr)
            ?? _directory.FindByUsername(subStr)
            ?? throw new AuthenticationException("unknown user: " + subStr);
    }

    private LoginResponse Session(CurrentUser user, string? tenantId, string? saasRefreshToken) =>
        Session(user, tenantId, null, saasRefreshToken);

    private LoginResponse Session(CurrentUser user, string? tenantId, List<MyTenant>? tenants, string? saasRefreshToken)
    {
        var accessToken = _jwt.Issue(user.Id, tenantId);
        var refreshToken = saasRefreshToken == null
            ? _jwt.IssueRefresh(user.Id, "dev-placeholder")
            : _jwt.IssueRefresh(user.Id, saasRefreshToken);
        var useTenants = tenants ?? _directory.TenantsOf(user.Username).ToList();
        return new LoginResponse
        {
            Token = accessToken,
            RefreshToken = refreshToken,
            User = user,
            Tenants = useTenants,
        };
    }

    private static List<MyTenant> TenantsFrom(List<SaasTenantMembership> memberships)
    {
        return memberships.Select(m => new MyTenant
        {
            TenantId = m.TenantId,
            Code = m.TenantId,
            Name = m.TenantId,
            RoleIds = m.RoleIds?.ToList() ?? new List<string>(),
        }).ToList();
    }

    private static MenuNode Menu(string id, string label, string path) => new()
    {
        Id = id,
        Label = label,
        Path = path,
    };

    public sealed class SsoAuthResult
    {
        public SsoRedirect Redirect { get; }
        public string CookieValue { get; }
        public SsoAuthResult(SsoRedirect redirect, string cookieValue)
        {
            Redirect = redirect;
            CookieValue = cookieValue;
        }
    }
}
