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
}
