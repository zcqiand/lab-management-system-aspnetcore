namespace Lab.AspNetCore.Security;

/// <summary>
/// 租户上下文（B2）。从当前 HTTP 请求的 JWT claim 解 tenant_id。
/// ADR-0019：删 "claim 缺失 fallback TENANT-001" 反模式。缺失 throw，
/// 镜像 saas-aspnetcore TenantContext 模板（claim 缺失由 TenantGuard 抛 business exception）。
/// </summary>
public interface ITenantContext
{
    string TenantId { get; }
}

public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public string TenantId
    {
        get
        {
            var claim = accessor.HttpContext?.User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(claim))
            {
                // ADR-0019：删 dev fallback TENANT-001,缺失必须拒,让 TenantGuard 抛 401。
                // 不抛 UnauthorizedAccessException 是因为这是 DTO 属性,
                // 异常由调用方(controller)catch 后映射 HTTP code。
                throw new UnauthorizedAccessException("tenant_id claim missing (ADR-0019 禁 demo 兜底)");
            }
            return claim;
        }
    }
}
