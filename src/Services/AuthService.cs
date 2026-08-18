namespace Lab.AspNetCore.Services;

using System.Security.Authentication;
using System.Text;
using Lab.AspNetCore.Controllers.Generated;
using Lab.AspNetCore.Directory;

/// <summary>
/// M00.F01/F02 + M01.F04/F05 — 认证域（B1）。
///
/// 语义镜像 lab-msw handlers-extra.ts 与 lab-springboot AuthService（DEMO_USER /
/// 3 租户 / 固定菜单与权限集 / SSO 跳 saas 登录页）。token 是自签 dev alg=none JWT，
/// switch-tenant 换发携带 tenant_id claim 的 token，me 从 token claim 解 currentTenantId。
/// </summary>
public sealed class AuthService
{
    /// <summary>msw 权限集（admin 全量 11 项，handlers-extra.ts:160-175）。</summary>
    internal static readonly IReadOnlyList<string> DemoPermissions = new[]
    {
        "contract:read", "contract:write", "sample:read", "sample:write",
        "report:read", "report:write", "report:issue",
        "inspection:read", "inspection:write", "audit:read", "*",
    };

    private readonly IUserDirectory _directory;
    private readonly string _saasBase;

    public AuthService(IUserDirectory directory, string saasBase)
    {
        _directory = directory;
        _saasBase = saasBase;
    }

    // === M01.F05.I01 密码登录 ===

    public LoginResponse Login(LoginRequest body)
    {
        var username = body.Username is null ? "" : body.Username.Trim();
        var password = body.Password ?? "";
        if (username.Length == 0 || password.Length == 0)
        {
            throw new ArgumentException("username and password are required");
        }

        if (!_directory.CheckPassword(username, password))
        {
            throw new AuthenticationException("Invalid username or password");
        }

        return Session(_directory.FindByUsername(username) ?? throw new AuthenticationException("Invalid username or password"));
    }

    // === M01.F05.I04 刷新 token ===

    public LoginResponse Refresh(RefreshTokenRequest body)
    {
        if (body?.RefreshToken is null)
        {
            throw new AuthenticationException("missing refresh_token");
        }

        // refreshToken 形如 "refresh-<userId>-<epoch>"；userId 自身含 '-'，按前缀 + 末段剥离
        // （saas AuthService.refresh 同款 split bug 的修法）。
        var token = body.RefreshToken;
        const string prefix = "refresh-";
        if (!token.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new AuthenticationException("invalid refresh_token");
        }

        var tokenBody = token[prefix.Length..];
        var lastDash = tokenBody.LastIndexOf('-');
        if (lastDash <= 0)
        {
            throw new AuthenticationException("invalid refresh_token");
        }

        var username = tokenBody[..lastDash];
        var user = _directory.FindByUsername(username);
        if (user is null)
        {
            throw new AuthenticationException("invalid refresh_token");
        }

        return Session(user);
    }

    // === M01.F05.I05 登出（无状态 JWT，服务端无 session store） ===

    public void Logout()
    {
        // 前端清存储；服务端无操作。
    }

    // === M00.F01.I01 当前会话 ===

    public CurrentUserSession Me(IReadOnlyDictionary<string, object> claims)
    {
        var user = _directory.FindByUsername(claims["sub"].ToString() ?? "");
        if (user is null)
        {
            throw new AuthenticationException("unknown user");
        }

        var currentTenantId = claims.TryGetValue("tenant_id", out var tenantClaim)
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
        var user = _directory.FindByUsername(claims["sub"].ToString() ?? "");
        if (user is null)
        {
            throw new AuthenticationException("unknown user");
        }

        var tenantId = body?.TenantId ?? "";
        var target = _directory.FindByTenantId(tenantId);
        if (target is null)
        {
            throw new KeyNotFoundException("Tenant not found");
        }

        return Session(user, target.TenantId);
    }

    // === M01.F04.I01 动态菜单 / I02 权限集 ===

    public List<MenuNode> Menus() => new()
    {
        // 镜像 lab-msw handlers-extra.ts:178-225（5 根节点）
        new() { Id = "menu-dashboard", Label = "工作台", Path = "/dashboard", Icon = "dashboard" },
        new()
        {
            Id = "menu-m02", Label = "资源管理", Icon = "resource",
            Children = new List<MenuNode> { Menu("menu-contracts", "合同管理", "/contracts") },
        },
        new()
        {
            Id = "menu-m03", Label = "试验过程", Icon = "flow",
            Children = new List<MenuNode>
            {
                Menu("menu-receipts", "接样管理", "/receipts"),
                Menu("menu-task", "任务分配", "/receipts?stage=task_assignment"),
                Menu("menu-entry", "数据录入", "/receipts?stage=data_entry"),
                Menu("menu-review", "报告审核", "/receipts?stage=review"),
                Menu("menu-approve", "报告批准", "/receipts?stage=approval"),
                Menu("menu-issue", "报告发放", "/receipts?stage=issuance"),
                Menu("menu-archive", "报告归档", "/receipts?stage=archived"),
            },
        },
        new()
        {
            Id = "menu-m04", Label = "基础数据", Icon = "data",
            Children = new List<MenuNode>
            {
                Menu("menu-techreq", "技术要求", "/technical-requirements"),
                Menu("menu-models", "型号维护", "/catalog/models"),
                Menu("menu-specs", "规格维护", "/catalog/specs"),
                Menu("menu-grades", "等级维护", "/catalog/grades"),
                Menu("menu-brands", "牌号维护", "/catalog/brands"),
            },
        },
        new()
        {
            Id = "menu-m05", Label = "数据统计", Icon = "stats",
            Children = new List<MenuNode> { Menu("menu-summary", "报告汇总", "/summary") },
        },
    };

    public PermissionSet Permissions() => new() { Permissions = DemoPermissions.ToList() };

    // === M01.F05.I02 SSO 跳转 / I03 SSO 回调 ===

    public SsoRedirect SsoAuthorize(string redirect)
    {
        // v0.1.x 语义（msw 同款）：authorizeUrl 直接指 saas /login?redirect=...，
        // 浏览器真能跳过去；state 用 dev 固定值（真对接待 saas 端点就绪后换随机 + 校验）。
        var target = string.IsNullOrEmpty(redirect) ? "/" : redirect;
        return new SsoRedirect
        {
            AuthorizeUrl = _saasBase + "/login?redirect=" + target + "&state=mock-state",
            State = "mock-state",
        };
    }

    public LoginResponse SsoCallback()
    {
        // dev 直发 demo 会话（msw 同款）；真 code/state 校验待 saas 端点可用。
        return Session(_directory.FindByUsername("admin") ?? throw new AuthenticationException("unknown user"));
    }

    // === token 签发（dev alg=none，镜像 saas AuthService.issueAccessToken） ===

    private LoginResponse Session(CurrentUser user) => Session(user, null);

    private LoginResponse Session(CurrentUser user, string? tenantId)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new LoginResponse
        {
            Token = IssueAccessToken(user.Username, tenantId, now),
            RefreshToken = "refresh-" + user.Username + "-" + now,
            User = user,
            Tenants = _directory.TenantsOf(user.Username).ToList(),
        };
    }

    /// <summary>dev alg=none JWT。sub 放 username（me/switchTenant 据此查目录）；tenant_id 仅在选过租户后携带。</summary>
    private static string IssueAccessToken(string username, string? tenantId, long now)
    {
        var header = B64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var tenantClaim = tenantId is null ? "" : ",\"tenant_id\":\"" + tenantId + "\"";
        var payload = B64Url("{\"sub\":\"" + username + "\"" + tenantClaim + ",\"iat\":" + now + ",\"exp\":" + (now + 3600) + "}");
        return header + "." + payload + ".dev-placeholder";
    }

    private static string B64Url(string s) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static MenuNode Menu(string id, string label, string path) => new() { Id = id, Label = label, Path = path };
}
