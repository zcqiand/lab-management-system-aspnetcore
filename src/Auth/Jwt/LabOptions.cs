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
        public string SaasBase { get; set; } = "http://localhost:3000";
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string DefaultTenantId { get; set; } = "";
        public string CallbackRedirectBase { get; set; } = "http://localhost:5080/api/auth/sso/callback";
    }
}
