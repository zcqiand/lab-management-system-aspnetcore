namespace Lab.AspNetCore.Security;

/// <summary>
/// 租户上下文（B2）。从当前 HTTP 请求的 JWT claim 解 tenant_id，缺失时 dev fallback
/// TENANT-001 —— 镜像 springboot InspectionCatalogController.currentTenantIdOrDefault。
/// scoped：每请求一次。
/// </summary>
public interface ITenantContext
{
    string TenantId { get; }
}

public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    private const string DefaultTenant = "TENANT-001";

    public string TenantId
    {
        get
        {
            var claim = accessor.HttpContext?.User.FindFirst("tenant_id")?.Value;
            return string.IsNullOrEmpty(claim) ? DefaultTenant : claim;
        }
    }
}
