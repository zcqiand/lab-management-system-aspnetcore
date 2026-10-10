namespace Lab.AspNetCore.Auth.Sso;

using System.Net.Http.Json;

/// <summary>
/// SaasAuthClient — 真对接 saas-identity-platform 的 OAuth 2.0 端点。
/// </summary>
public interface ISaasAuthClient
{
    Task<AuthorizeCodeResponse> AuthorizeAsync(string redirectUri, string scope, string state, CancellationToken ct = default);
    Task<TokenResponse> TokenAsync(string grantType, string? code, string? refreshToken, string? redirectUri, CancellationToken ct = default);
    /// <summary>saas /api/v1/auth/login 密码登录（服务账号用，替密码登录用户拉菜单快照）。</summary>
    Task<TokenResponse> ServiceLoginAsync(string username, string password, CancellationToken ct = default);

    /// <summary>
    /// REQ-2026-023：原生登录账密校验代理 —— 用终端用户账密调 saas /api/v1/auth/login，
    /// 返回完整 body（user/availableTenants/userId/currentTenantId）供映射 LoginResponse。
    /// </summary>
    Task<SaasLoginResponse> UserLoginAsync(string username, string password, CancellationToken ct = default);
}

public sealed class AuthorizeCodeResponse
{
    public string Code { get; set; } = "";
    public string State { get; set; } = "";
}

public sealed class TokenResponse
{
    public string AccessToken { get; set; } = "";
    public string? RefreshToken { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string Scope { get; set; } = "";
}

/// <summary>
/// REQ-2026-023：saas /api/v1/auth/login 完整响应（原生登录代理用）。
/// 注意 saas user 无 displayName、availableTenants 无租户名（富化走 /admin/tenants）。
/// </summary>
public sealed class SaasLoginResponse
{
    public SaasLoginUser? User { get; set; }
    public List<SaasAvailableTenant>? AvailableTenants { get; set; }
    public string AccessToken { get; set; } = "";
    public string? RefreshToken { get; set; }
    public string? UserId { get; set; }
    public string? CurrentTenantId { get; set; }
}

public sealed class SaasLoginUser
{
    public string? Id { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? Status { get; set; }
}

public sealed class SaasAvailableTenant
{
    public string? TenantId { get; set; }
    public List<string>? RoleIds { get; set; }
    public string? Status { get; set; }
}

/// <summary>
/// 真 HTTP 实现：HttpClient + IOptions + SaasErrorMappingHandler。
/// </summary>
public sealed class HttpSaasAuthClient : ISaasAuthClient
{
    private readonly HttpClient _http;
    private readonly Lab.AspNetCore.Auth.Jwt.LabOptions.SsoSection _sso;

    public HttpSaasAuthClient(HttpClient http, Microsoft.Extensions.Options.IOptions<Lab.AspNetCore.Auth.Jwt.LabOptions> opts)
    {
        _http = http;
        _sso = opts.Value.Sso;
        if (string.IsNullOrEmpty(_sso.SaasBase)) throw new InvalidOperationException("LAB_SAAS_BASE_URL required");
        if (string.IsNullOrEmpty(_sso.ClientId)) throw new InvalidOperationException("LAB_SAAS_CLIENT_ID required");
        if (string.IsNullOrEmpty(_sso.ClientSecret)) throw new InvalidOperationException("LAB_SAAS_CLIENT_SECRET required");
        if (string.IsNullOrEmpty(_sso.DefaultTenantId)) throw new InvalidOperationException("LAB_SAAS_DEFAULT_TENANT_ID required");
        // 5.33：服务账号登录 clientId 是业务身份字段（ADR-0019），缺失 fail-fast 不兜底
        if (string.IsNullOrEmpty(_sso.ServiceClientId)) throw new InvalidOperationException("LAB_SAAS_SERVICE_CLIENT_ID required");
        _http.BaseAddress ??= new Uri(_sso.SaasBase);
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<AuthorizeCodeResponse> AuthorizeAsync(string redirectUri, string scope, string state, CancellationToken ct = default)
    {
        var body = new Dictionary<string, string>
        {
            ["clientId"] = _sso.ClientId,
            ["redirectUri"] = redirectUri,
            ["responseType"] = "code",
            ["scope"] = scope,
            ["state"] = state,
            ["tenantId"] = _sso.DefaultTenantId,
        };
        var resp = await _http.PostAsJsonAsync("/api/v1/oauth/authorize", body, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<AuthorizeCodeResponse>(cancellationToken: ct);
        return result ?? throw new SaasAuthException.UpstreamUnavailable("saas returned empty authorize response");
    }

    public async Task<TokenResponse> TokenAsync(string grantType, string? code, string? refreshToken, string? redirectUri, CancellationToken ct = default)
    {
        var body = new Dictionary<string, string>
        {
            ["grantType"] = grantType,
            ["clientId"] = _sso.ClientId,
            ["clientSecret"] = _sso.ClientSecret,
            ["tenantId"] = _sso.DefaultTenantId,
        };
        if (code != null) body["code"] = code;
        if (refreshToken != null) body["refreshToken"] = refreshToken;
        if (redirectUri != null) body["redirectUri"] = redirectUri;
        var resp = await _http.PostAsJsonAsync("/api/v1/oauth/token", body, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        return result ?? throw new SaasAuthException.UpstreamUnavailable("saas returned empty token response");
    }

    public async Task<TokenResponse> ServiceLoginAsync(string username, string password, CancellationToken ct = default)
    {
        // 2026-09-19 5.33：body 对齐 saas LoginRequest {username, password, clientId}——clientId 取
        // LAB_SAAS_SERVICE_CLIENT_ID；陈旧字段 tenantCode 删（契约无此字段，修前 saas 400 fieldErrors）。
        var body = new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password,
            ["clientId"] = _sso.ServiceClientId,
        };
        var resp = await _http.PostAsJsonAsync("/api/v1/auth/login", body, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        return result ?? throw new SaasAuthException.UpstreamUnavailable("saas returned empty login response");
    }

    public async Task<SaasLoginResponse> UserLoginAsync(string username, string password, CancellationToken ct = default)
    {
        // REQ-2026-023：原生登录代理，body 同 {username, password, clientId}——但 clientId 取
        // OAuth client-id（LAB_SAAS_CLIENT_ID），不是服务账号的 ServiceClientId（另一键另一值语义）。
        var body = new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password,
            ["clientId"] = _sso.ClientId,
        };
        var resp = await _http.PostAsJsonAsync("/api/v1/auth/login", body, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<SaasLoginResponse>(cancellationToken: ct);
        return result ?? throw new SaasAuthException.UpstreamUnavailable("saas returned empty login response");
    }
}
