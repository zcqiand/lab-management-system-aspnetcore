# CHANGELOG — lab-management-system-aspnetcore

格式参照 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

## [0.2.6] — 2026-08-27

- fix(menus): saas /me/menus 响应形状适配 — `Record<EffectiveMenuNode[]>`
  → 反序列化为 `Dictionary<string, List<SaasMenuNode>>`，按 appCode 查表。
  复现 prod bug：原实现反序列化 `SaasMenuNode[]` 对 map 抛 JsonException →
  CacheMenus catch 不写快照 → lab 前端 `/menus` miss 503。修后 lab-aspnetcore
  prod 拉 saas /me/menus 拿到的菜单树能正确进 MenuSnapshotCache。
- 新增 `tests/Auth/Sso/SaasMeClientTest.cs`：3 用例 TDD 红→绿
  （map 形状按 appCode 返 list / 未知 appCode 返空 / query 串含 appCode）

## [0.2.5] — 2026-08-27

- fix(cors): deploy 漂移老 key — 切 flat `LAB_CORS_ALLOWED_ORIGINS`
  （同 7d47f48；prod 跨源 SSO 全挂 CORS 头缺失）

## [0.2.4] — 2026-08-27

- `M01.F04.I01` 动态菜单 demo 兜底删除（与 springboot/nextjs 同步）：
  `Menus()` miss 抛 `MenusUnavailableException` → 503 `MENUS_UNAVAILABLE`；
  `Login()` 成功后用 saas 服务账号（`LAB_SAAS_SERVICE_USER/PASSWORD`）登
  `saas /api/v1/auth/login` 换 token 拉 `/me/menus` 快照。
  新增 `MenuSnapshotCache` / `MenusUnavailableException`；
  删除 `FALLBACK_MENUS` 常量；`ISaasAuthClient.ServiceLoginAsync` 客户端补全。

## [0.2.2?] — 2026-08-27

- 初始化台账：ASP.NET Core 8 后端。历史变更见 git log 与 `.state/session.json`。
