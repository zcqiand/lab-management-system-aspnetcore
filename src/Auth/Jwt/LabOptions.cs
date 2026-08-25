namespace Lab.AspNetCore.Auth.Jwt;

using Microsoft.Extensions.Options;

/// <summary>
/// lab.* 配置（IOptions 注入）。覆盖 jwt + sso 两段。
/// </summary>
public sealed class LabOptions
{
    public JwtSection Jwt { get; set; } = new();
    public SsoSection Sso { get; set; } = new();

    public sealed class JwtSection
    {
        public string Issuer { get; set; } = "lab-management-system";
        public int TtlSeconds { get; set; } = 3600;
        public int RefreshTtlSeconds { get; set; } = 604800;
        public string Secret { get; set; } = "";
    }

    public sealed class SsoSection
    {
        public string Profile { get; set; } = "no-sso";
        /// <summary>saas 后端 API base（HttpClient 调 /api/v1/oauth/* 与 /api/v1/me/* 用）。</summary>
        public string SaasBase { get; set; } = "http://localhost:5000";
        /// <summary>
        /// saas IdP 登录页（资源所有者认证跳板）。authorizeUrl 拼的是 {LoginUrl}/login?code=...，
        /// 该页面由 saas 前端（saas-nextjs /login）提供，不在后端 API 域名上（API /login 404）。
        /// 缺省取 SaasBase 同域（dev 时 saas-nextjs :3000 既是前端也带 API routes）。
        /// </summary>
        public string LoginUrl { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string DefaultTenantId { get; set; } = "";
        public string CallbackRedirectBase { get; set; } = "http://localhost:5080/api/auth/sso/callback";

        /// <summary>有效登录页 base：显式 LoginUrl 优先，缺省回落 SaasBase。</summary>
        public string EffectiveLoginUrl => string.IsNullOrEmpty(LoginUrl) ? SaasBase : LoginUrl;
    }
}
