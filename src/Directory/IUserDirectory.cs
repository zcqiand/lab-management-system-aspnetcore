namespace Lab.AspNetCore.Directory;

using Lab.AspNetCore.Controllers.Generated;

/// <summary>
/// 用户目录抽象（B1）。数据 1:1 镜像 lab-msw / lab-springboot ConfigUserDirectory：
/// admin@lab.local / dev123456（USER-A，roleCode=admin），3 租户。
/// production 换 saas 身份平台下发实现。
///
/// <p>username 与 email 桥接（ADR-0008）：saas {@code CurrentUser} 用 {@code email} 作为外部标识，lab
/// 端仍以 username 为内部主键；SSO 回调时按 email 回查。JWT 内 {@code sub} claim 是 user.id（saas
/// uuid），me/switchTenant 路径用 {@link #FindById(string)} 解析。
/// </summary>
public interface IUserDirectory
{
    CurrentUser? FindByUsername(string username);

    CurrentUser? FindByEmail(string email);

    CurrentUser? FindById(string id);

    bool CheckPassword(string username, string password);

    IReadOnlyList<MyTenant> TenantsOf(string username);

    MyTenant DefaultTenant();

    MyTenant? FindByTenantId(string tenantId);

    /**
     * 首次 SSO 落地时按 saas 用户 upsert 到 lab 目录。ConfigUserDirectory 仅在内存中维护；
     * 真实 DB 实现（V014+）走 SQL upsert。
     */
    CurrentUser Upsert(string id, string email, string displayName, string roleCode);
}
