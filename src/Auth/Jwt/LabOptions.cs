namespace Lab.AspNetCore.Auth.Jwt;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

/// <summary>
/// lab.* 配置（IOptions 注入）。SSO 段 2026-08-28 key 统一:属性 getter 直读
/// flat env key(LAB_SAAS_*/LAB_SSO_*,与 lab-springboot application.yml 占位符
/// 同名),不再依赖 Lab__Sso__* 双下划线段映射 —— 段映射 key 名与 flat 家族
/// 契约不一致,曾导致 deploy 脚本/env 文件两套命名并存。
/// Json 段(appsettings 的 Lab:Sso)作为 dev fallback 保留:flat env 未设时回落。
/// </summary>
public sealed class LabOptions
{
    /// <summary>flat env 读取入口;Program.cs 组合根注入(IConfiguration 由宿主提供)。</summary>
    public IConfiguration? Config { internal get; set; }

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
        /// <summary>flat env 读取入口(经 LabOptions.Config 注入;测试可不设)。</summary>
        public IConfiguration? Config { internal get; set; }

        public string Profile { get; set; } = "no-sso";
        /// <summary>saas 后端 API base（HttpClient 调 /api/v1/oauth/* 与 /api/v1/me/* 用）。</summary>
        public string SaasBase => Config?["LAB_SAAS_BASE_URL"] ?? _saasBase;
        /// <summary>
        /// saas IdP 登录页（资源所有者认证跳板）。authorizeUrl 拼的是 {LoginUrl}/login?code=...，
        /// 该页面由 saas 前端（saas-nextjs /login）提供，不在后端 API 域名上（API /login 404）。
        /// 缺省取 SaasBase 同域（dev 时 saas-nextjs :3000 既是前端也带 API routes）。
        /// </summary>
        public string LoginUrl => Config?["LAB_SSO_LOGIN_URL"] ?? _loginUrl;
        public string ClientId => Config?["LAB_SAAS_CLIENT_ID"] ?? _clientId;
        public string ClientSecret => Config?["LAB_SAAS_CLIENT_SECRET"] ?? _clientSecret;
        public string DefaultTenantId => Config?["LAB_SAAS_DEFAULT_TENANT_ID"] ?? _defaultTenantId;
        public string ServiceUser => Config?["LAB_SAAS_SERVICE_USER"] ?? _serviceUser;
        public string ServicePassword => Config?["LAB_SAAS_SERVICE_PASSWORD"] ?? _servicePassword;
        public string CallbackRedirectBase => Config?["LAB_SSO_CALLBACK_REDIRECT"] ?? _callbackRedirectBase;

        // Lab:Sso json 段绑定字段(appsettings*.json dev 值;flat env 优先)
        public string _saasBase { get; set; } = "http://localhost:5000";
        public string _loginUrl { get; set; } = "";
        public string _clientId { get; set; } = "";
        public string _clientSecret { get; set; } = "";
        public string _defaultTenantId { get; set; } = "";
        public string _serviceUser { get; set; } = "alice";
        public string _servicePassword { get; set; } = "dev123456";
        public string _callbackRedirectBase { get; set; } = "http://localhost:5080/api/auth/sso/callback";

        /// <summary>有效登录页 base：显式 LoginUrl 优先，缺省回落 SaasBase。</summary>
        public string EffectiveLoginUrl => string.IsNullOrEmpty(LoginUrl) ? SaasBase : LoginUrl;
    }
}
