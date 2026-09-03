# 设计：Me() 租户体系对齐（SSO 用户）— 四后端家族推进

> 2026-09-03。修复 prod「SSO 登录成功后刷新页面卡『检查登录态…』」。
> 本文档为四仓（aspnetcore/springboot/nextjs/msw）共用设计，落在 aspnetcore 仓，
> 其余三仓实现时引用本文件。

## 1. 背景 / 根因

- SSO 链路：`SsoCallback` / `Refresh` 返回 **saas memberships 租户**（UUID），
  前端 `settleLogin` 把它写进 `localStorage.activeTenantId`。
- 刷新页面：`hydrateAuth` 调 `/api/auth/me`，后端 `Me()` 返回 **demo 目录租户**
  （`TENANT-001/002/003` 硬编码）。
- `tenants.find(tenantId === <saas UUID>)` 跨体系失配 → FSM 落 `awaiting_tenant`
  → 守卫踢 /login → LoginPage 无该态分支 → 卡「检查登录态…」死锁。
- 四后端同构中招；lab-react / lab-vue 前端同构中招。

### 存量地雷（本次一并修）

`Menus()` cache-miss reload（AuthService.cs:187）用 stored saas refresh_token
调 saas `/token`，但**没有把新 refresh_token 存回**。saas 是 rotate-once 语义
（旧 token 即刻作废）→ 菜单缓存过期一次 → stored token 已被消费 → 后续所有
reload / refresh 永久 `INVALID_GRANT` → 用户被迫重登。

## 2. 决策（已批准）

**方案 A + 选项 1**：Me() 对 SSO 用户按 memberships 缓存分流；miss 抛 401，
由前端 `hydrateAuth` 的 catch 走**现成的** `/api/auth/refresh` 链路自愈
（该端点已正确：rotate + Upsert + 返回 saas tenants → `settleLogin` 落位）。

不选 Me() 内部同步走 saas（选项 2）：避免在 Me() 重写 saas 调用序列、
避免高频 miss 场景疯狂 rotate saas token。

## 3. 语义表

| 用户 | memberships 缓存 | Me() 返回 |
|---|---|---|
| SSO 用户 | hit | saas memberships 租户（同 SsoCallback 体系）|
| SSO 用户 | miss | **401**（AuthenticationException）→ 前端 refresh 自愈 |
| 密码登录（DemoUser / service account） | — | demo TENANT-00x（现状不变）|

**SSO 用户判据**：`GetSaasRefreshToken(sub)` 非空。密码登录的 DemoUser 走
service account 拉菜单、无 per-user token，天然二分。

**前端零改动**（lab-react / lab-vue）：自愈链路现成。

## 4. 各仓改动

### 4.1 lab-aspnetcore（本仓）

1. 新增 `MembershipSnapshotCache`（进程内，与 `MenuSnapshotCache` 并行）：
   SsoCallback / Refresh 时用已到手的 `memberships` 顺手填（`TenantsFrom` 后的
   `List<MyTenant>` 按 labUser.Id 存）。
2. `Me()` 按语义表分流；SSO+miss 抛 `AuthenticationException`（Program.cs 现有
   401 映射）。
3. **rotate-once 雷修复**：`Menus()` reload 成功后
   `_directory.SetSaasRefreshToken(sub, t.RefreshToken ?? "")`。
4. `SsoCallback` 的 `Session(labUser, null, …)` 改传 whoami 的
   `currentTenantId`（saas /me 返回）→ token 带 tenant_id claim →
   Me() 的 currentTenantId 不再落 demo 默认值。

### 4.2 lab-springboot

镜像 4.1 全部四点（目录 `directory/ConfigUserDirectory.java` +
`service/AuthService.java`，机制同名同构）。

### 4.3 lab-nextjs

`src/app/api/auth/me/route.ts`：读 Authorization Bearer —— menu-snapshot /
memberships 缓存有该 sub 会话则返回 saas memberships 租户；无 token 或 miss →
401。DEMO_TENANTS 仅保留给无 Bearer 的 demo 路径（如契约允许）。

### 4.4 lab-msw

核对 me 与 sso/callback handler 的租户一致性；mock 预计已一致或无需区分。
仅当发现不一致才改。

## 5. 测试（TDD，挂 M00.F01.I01 既有 fn-ID）

每后端先写失败测试再实现：

1. SSO 用户 memberships hit → Me() 返回 saas tenants（UUID 体系）
2. SSO 用户 miss → 401
3. 密码用户 → demo tenants 不变（不掉登录）
4. Menus() reload 后 stored refresh token 已更新（rotate-once 回归测试）
5. SsoCallback 签出的 token 含 tenant_id claim

## 6. 风险与边界

- 已卡死的存量用户：refresh token 未被烧 → 自愈恢复；已被旧雷烧掉 → 401 →
  ANON → 重走 SSO（一次重登，可接受）。
- Me() 高频 miss 会反复打 refresh，但 refresh 填 cache 后第二次刷新起 hit，
  不构成风暴。
- 多实例部署 cache 一致性是既有已知限制（menu cache 同款），不新增劣化。
