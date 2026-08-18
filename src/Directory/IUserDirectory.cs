namespace Lab.AspNetCore.Directory;

using Lab.AspNetCore.Controllers.Generated;

/// <summary>
/// 用户目录抽象（B1）。数据 1:1 镜像 lab-msw / lab-springboot ConfigUserDirectory：
/// admin / dev123456（USER-A，roleCode=admin），3 租户。
/// production 换 saas 身份平台下发实现。
/// </summary>
public interface IUserDirectory
{
    CurrentUser? FindByUsername(string username);

    bool CheckPassword(string username, string password);

    IReadOnlyList<MyTenant> TenantsOf(string username);

    MyTenant DefaultTenant();

    MyTenant? FindByTenantId(string tenantId);
}
