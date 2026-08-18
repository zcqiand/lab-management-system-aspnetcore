namespace Lab.AspNetCore.Directory;

using Lab.AspNetCore.Controllers.Generated;

/// <summary>
/// 配置式 demo 目录（B1）。数据 1:1 镜像 lab-msw / lab-springboot：
///
///   用户：admin / dev123456（USER-A，roleCode=admin）
///   租户：TENANT-001 city-lab / TENANT-002 district-lab / TENANT-003 third-party
///
/// 口令可用 Lab:Auth:DevPassword 覆盖（避免硬编码扩散到测试）。
/// </summary>
public sealed class ConfigUserDirectory : IUserDirectory
{
    private static readonly CurrentUser DemoUser = new()
    {
        Id = "USER-A",
        Username = "admin",
        DisplayName = "管理员",
        RoleCode = "admin",
    };

    private static readonly IReadOnlyList<MyTenant> Tenants = new[]
    {
        new MyTenant { TenantId = "TENANT-001", Code = "city-lab", Name = "市住建工程质量检测中心", RoleIds = new List<string> { "admin" } },
        new MyTenant { TenantId = "TENANT-002", Code = "district-lab", Name = "区检测站", RoleIds = new List<string> { "technician" } },
        new MyTenant { TenantId = "TENANT-003", Code = "third-party", Name = "第三方检测实验室", RoleIds = new List<string> { "viewer" } },
    };

    private readonly string _devPassword;

    public ConfigUserDirectory(string devPassword)
    {
        _devPassword = devPassword;
    }

    public CurrentUser? FindByUsername(string username) =>
        DemoUser.Username == username ? DemoUser : null;

    public bool CheckPassword(string username, string password) =>
        DemoUser.Username == username && _devPassword == password;

    public IReadOnlyList<MyTenant> TenantsOf(string username) => Tenants;

    public MyTenant DefaultTenant() => Tenants[0];

    public MyTenant? FindByTenantId(string tenantId) =>
        Tenants.FirstOrDefault(t => t.TenantId == tenantId);
}
