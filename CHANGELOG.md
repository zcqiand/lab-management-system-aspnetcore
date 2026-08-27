# CHANGELOG — lab-management-system-aspnetcore

格式参照 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

## [0.2.3] — 2026-08-27

- `M01.F04.I01` 动态菜单 demo 兜底删除（与 springboot/nextjs 同步）：
  `Menus()` miss 抛 `MenusUnavailableException` → 503 `MENUS_UNAVAILABLE`；
  `Login()` 成功后用 saas 服务账号（`LAB_SAAS_SERVICE_USER/PASSWORD`）登
  `saas /api/v1/auth/login` 换 token 拉 `/me/menus` 快照。
  新增 `MenuSnapshotCache` / `MenusUnavailableException`；
  删除 `FALLBACK_MENUS` 常量；`ISaasAuthClient.ServiceLoginAsync` 客户端补全。

## [0.2.2?] — 2026-08-27

- 初始化台账：ASP.NET Core 8 后端。历史变更见 git log 与 `.state/session.json`。
