namespace Lab.AspNetCore.Services;

/// <summary>
/// 登录路径 saas IdP 不可达/5xx（REQ-2026-023：五仓统一 502 SAAS_UNAVAILABLE）。
/// 与 SSO 路径的 SaasAuthException.UpstreamUnavailable（Program.cs 按 Status 透传 502）分流：
/// 原生登录的错误码族与 nextjs/springboot/rails/fastapi 对齐，不透传 saas 内部详情。
/// </summary>
public sealed class SaasIdpUnavailableException : Exception
{
    public SaasIdpUnavailableException(string message) : base(message)
    {
    }
}
