# CHANGELOG — lab-management-system-aspnetcore

格式参照 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

## [0.2.25] — 2026-09-04

- fix(ef): B7 EF 镜像 prod 首请求 500 — `CatalogEntity` helper 漏
  `Ignore("AdditionalProperties")`，4 个码表实体（brands/models/specs/grades）
  的 JsonExtensionData 字典被照映射，模型验证在首个 `Set<T>()` 才炸
  （组合根盲区变体：测试走 memory 分支全绿）。
- 新增 `tests/Harness/LabDbContextModelTest.cs`：强制初始化全 EF 模型，
  把「首请求才爆」提前到「测试就爆」；逐实体断言 AdditionalProperties 已 Ignore。

## [0.2.26] — 2026-09-04

- fix(ef): `Kw` 普通 C# 方法在 Where lambda 里不可翻译 —— B7 EF 镜像下
  全部 Filter 端点（contracts/receipts/samples/码表/字典，共 13 处 × 3 store）
  prod 首请求 500。改 `WhereKw` 表达式组合扩展（`EF.Functions.ILike` → PG ILIKE，
  语义 = ToLower().Contains）。
- 测试体系改造（v0.2.25/26 两起事故的共同根因是「测试走 memory 分支」）：
  - 新增 `EfQueryTranslatabilityTest`：store 拆 internal `Build*Query`（18 个查询）
    逐个 `ToQueryString`，翻译失败测试即红。无需连库，CI 可跑。
  - 新增 `EfStorePgTest`（lab_test 真库硬依赖，连不上即败不 skip）：
    ILIKE 匹配 / tenant 隔离 / 排序 / upsert 语义走真 SQL 验证；
    首跑即抓出 memory 分支测不出的 FK（models→objects）。
  - 新增 `TestDb` 基建：连接串 `LAB_TEST_DATABASE_URL` 可覆盖。

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
