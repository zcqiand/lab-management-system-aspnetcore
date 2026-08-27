namespace Lab.AspNetCore.Services;

/// <summary>
/// 菜单快照不可用（GET /api/auth/menus miss）。
///
/// 2026-08-27 起 demo 兜底菜单删除：快照 miss（密码登录未拉到 saas 菜单 / TTL 过期 / 进程重启）
/// 不再返回假树，而是抛本异常由 Program.cs 异常映射 503（MENUS_UNAVAILABLE 语义）。
/// 前端 useBackendMenus 失败回退静态菜单（FALLBACK_NAV / MENU_TREE），语义闭环。
/// </summary>
public class MenusUnavailableException : Exception
{
    public MenusUnavailableException(string message) : base(message) { }
}
