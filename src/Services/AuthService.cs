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
    private readonly MenuSnapshotCache _menuCache;

    public AuthService(
        IUserDirectory directory,
        LabJwtSigner jwt,
        ISaasAuthClient saasAuth,
        ISaasMeClient saasMe,
        StateCookieManager stateMgr,
        IOptions<LabOptions> opts,
        MenuSnapshotCache? menuCache = null)
    {
        _directory = directory;
        _jwt = jwt;
        _saasAuth = saasAuth;
        _saasMe = saasMe;
        _stateMgr = stateMgr;
        _opts = opts;
        _menuCache = menuCache ?? new MenuSnapshotCache();
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
        // 密码登录的 dev 用户无 saas 身份 -> 用服务账号拉菜单快照（demo 兜底已删，
        // miss 时 Menus() 抛 503）。失败不阻塞登录（warn），重登/SSO 可补。
        try
        {
            var t = _saasAuth.ServiceLoginAsync(_opts.Value.Sso.ServiceUser, _opts.Value.Sso.ServicePassword).GetAwaiter().GetResult();
            CacheMenus(user.Id, t.AccessToken);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[login] service-account menu snapshot failed for {user.Id}: {e.Message}");
        }
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
        // 2026-08-29: Refresh 端点也要更新 saas refresh_token (rotate-once 语义,
        // saas /token 返回新 refresh_token) + CacheMenus 重填 cache。
        _directory.SetSaasRefreshToken(labUser.Id, t.RefreshToken);
        CacheMenus(labUser.Id, t.AccessToken);
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

    /// <summary>
    /// 动态菜单：SSO/refresh/密码登录时缓存的 saas 快照。miss（快照过期/拉取失败/重启）抛
    /// MenusUnavailableException（Program.cs 映射 503）-- 2026-08-27 起 demo 兜底删除，
    /// 假树不再下发；前端 useBackendMenus 失败回退静态菜单。
    /// </summary>
    public List<MenuNode> Menus(IReadOnlyDictionary<string, object>? claims)
    {
        var sub = claims?.GetValueOrDefault("sub")?.ToString();
        if (sub is null)
        {
            throw new MenusUnavailableException("missing sub claim; re-login required");
        }
        // 2026-08-29 修 prod 503: MenuSnapshotCache 是进程内 ConcurrentDictionary,
        // VPS 重 deploy / 容器重启即清空 → 旧 token 调 /api/auth/menus 503。
        // miss 时主动用 IUserDirectory 持久化的 saas refresh_token 走 saas /token refresh,
        // 拿新 saas access_token 调 saas /me/menus 填 cache。单实例 OK,多实例部署
        // 需要把 saas refresh_token 持久化到 DB / Redis (Phase 6+ follow-up)。
        var cached = _menuCache.Get(sub);
        if (cached is not null) return cached;
        var saasRefresh = _directory.GetSaasRefreshToken(sub);
        if (string.IsNullOrEmpty(saasRefresh))
        {
            throw new MenusUnavailableException(
                $"menu snapshot unavailable for user {sub} (cache miss + no saas refresh_token); re-login required");
        }
        try
        {
            var t = _saasAuth.TokenAsync("refresh_token", null, saasRefresh, null).GetAwaiter().GetResult();
            CacheMenus(sub, t.AccessToken);
            return _menuCache.Get(sub)
                ?? throw new MenusUnavailableException(
                    $"menu snapshot unavailable for user {sub} (post-refresh still empty)");
        }
        catch (Exception e)
        {
            throw new MenusUnavailableException(
                $"menu snapshot reload failed for user {sub}: {e.Message}; re-login required");
        }
    }

    public PermissionSet Permissions() => new() { Permissions = DemoPermissions.ToList() };

    // === M01.F05.I02 SSO 跳转 / I03 SSO 回调 ===

    public SsoAuthResult SsoAuthorize(string redirectUri, string state)
    {
        // RFC 6749 §4.1.1：redirect_uri + state 由客户端（前端）发起，原样透传给授权服务器。
        // lab 后端是 confidential client,但 OAuth 2.0 标准 authorize 端点必须 saas session
        // 验证资源所有者(用户必须在 saas 端登录,写 saasSession cookie),lab 后端 server-to-server
        // 调不通(HttpOnly + SameSite cookie 无法跨后端获取)。所以 lab 后端不再代理 authorize。
        //
        // 2026-08-29 修 prod 401: 不再调 _saasAuth.AuthorizeAsync(预拿 code),改成直接
        // 302 跳 saas 登录页(带 redirect_uri + state)。saas-vue/saas-react LoginPage 已支持
        // ?redirect_uri=&state= 参数处理:用户登录后自动调 saas-aspnetcore /api/v1/oauth/authorize
        // 拿 code(此时已有 saas session),302 跳回 redirect_uri?code=&state= 给 lab 前端。
        // lab 前端 POST /api/auth/sso/callback {code, state} → SsoCallback 用 clientSecret
        // 调 saas /token (v0.3.20 已放宽不需 session) 拿 access_token + refresh_token。
        if (string.IsNullOrEmpty(redirectUri)) throw new ArgumentException("missing redirect_uri");
        if (string.IsNullOrEmpty(state)) throw new ArgumentException("missing state");
        var ss = _stateMgr.Issue(redirectUri, state);
        // RFC 6749 §4.1.1: OAuth 2.0 授权请求必带 client_id。
        // saas-vue/saas-react LoginPage 的 OAuth 跳板分支需要 client_id 调
        // /api/v1/oauth/authorize 拿 code 跳回 RP。缺 client_id 时跳板分支
        // early return → 用户登录后落到 /tenants → 阻断 OAuth 流程。
        var authorizeUrl = $"{_opts.Value.Sso.EffectiveLoginUrl}/login" +
            $"?redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&client_id={Uri.EscapeDataString(_opts.Value.Sso.ClientId)}";
        return new SsoAuthResult(
            new SsoRedirect { AuthorizeUrl = authorizeUrl, State = state },
            ss.CookieValue);
    }

    public LoginResponse SsoCallback(SsoCallbackRequest body, string cookieValue)
    {
        if (body == null) throw new ArgumentException("missing body");
        // RFC 6749 §4.1.3：grant_type=authorization_code + code + redirect_uri（须与
        // authorize 时一致，saas 侧校验）+ state（§10.12，与 authorize 响应一致）。
        if (body.Grant_type != OAuthGrantType.Authorization_code)
            throw new AuthenticationException("unsupported grant_type");
        // cookie 内 state 与 body.state 配对校验（CSRF）；redirect 一致性由 saas token 端点校验
        _stateMgr.Verify(cookieValue, body.State);
        var t = _saasAuth.TokenAsync("authorization_code", body.Code, null, body.Redirect_uri).GetAwaiter().GetResult();
        var saasUser = _saasMe.WhoamiAsync(t.AccessToken).GetAwaiter().GetResult();
        var memberships = _saasMe.ListMyTenantsAsync(t.AccessToken).GetAwaiter().GetResult();
        var labUser = _directory.FindByEmail(saasUser.Email)
            ?? _directory.Upsert(saasUser.Id, saasUser.Email, saasUser.DisplayName ?? "", "viewer");
        // 2026-08-29: 持久化 saas refresh_token 按 userId → MenuSnapshotCache miss reload 用。
        // 菜单快照：瞬时持有 saas accessToken 的唯一时点，顺手拉菜单进缓存（失败不阻塞登录）
        _directory.SetSaasRefreshToken(labUser.Id, t.RefreshToken);
        CacheMenus(labUser.Id, t.AccessToken);
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

    /// <summary>lab 家族在 saas 注册的 appCode（seeds apps.json）。</summary>
    internal const string LabAppCode = "lab-management";

    /// <summary>
    /// 拉菜单进快照缓存。失败只 warn 不抛 -- 菜单不可用不应阻塞登录主流程
    /// （miss 时 Menus() 503 由前端兜底），下次 refresh 重试。
    /// </summary>
    private void CacheMenus(string? userId, string? saasAccessToken)
    {
        if (userId is null || saasAccessToken is null) return;
        try
        {
            var snapshot = _saasMe.ListMyMenusAsync(saasAccessToken, LabAppCode).GetAwaiter().GetResult();
            _menuCache.Put(userId, snapshot.Select(MapSaasMenu).ToList());
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[menu] snapshot fetch failed for {userId}: {e.Message}");
        }
    }

    /// <summary>saas EffectiveMenuNode -> 契约 MenuNode（name->label；icon 空时按 type 兜底，镜像 springboot SaasMenuMapper）。</summary>
    private static MenuNode MapSaasMenu(SaasMenuNode src)
    {
        var children = src.Children ?? new List<SaasMenuNode>();
        var icon = string.IsNullOrEmpty(src.Icon)
            ? (src.Type == "group" ? "resource" : "file")
            : src.Icon;
        return new MenuNode
        {
            Id = src.Id,
            Label = src.Name,
            Path = src.Path,
            Icon = icon,
            Children = children.Select(MapSaasMenu).ToList(),
        };
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
