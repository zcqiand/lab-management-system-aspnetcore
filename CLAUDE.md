# CLAUDE.md — 实验室管理系统ASP.NET-Core后端

> 书稿配套仓 + harness 门禁仓双身份。入口，不是手册。L0 门强制上限 60 行。
> 本仓为《（书稿信息待补）》案例（待补）的可运行配套工程，是书稿代码块的 **source of truth**。

## 1. 项目定位

实验室管理系统的 C# 后端（与 springboot 仓对称的真实后端镜像）。
NSwag 读 shared 仓 OpenAPI 生成 abstract Controllers；手写 partial 实现类承接业务逻辑。

## 2. 铁律

- **TDD**：先写失败测试 → 确认红 → 实现 → 确认绿 → commit
- **版本钉死**：依赖与 `version-lock.json` 的 `version_lock` 一致；不引入 lock 外的库
- **tag 即放行**：全量回归绿后打 `v<MAJOR>.<MINOR>.<PATCH>-<YYYYMMDD>`（如 `v0.1.14-20260826`）
- **功能清单是锚点**：改 function-tree 走 `/tree-change`；同 commit；废弃只改状态，编号不复用
- 禁止在 Controller / Program.cs 里写业务逻辑
- 禁止 catch 后吞掉异常（不记录、不 rethrow）
- 禁止把连接串/密钥硬编码进源码（放 appsettings.Development.json 或环境变量）
- 禁止直接编辑 NSwag 产物（下次 gen-shared 重写）

## 3. 技术栈与版本（钉死于 version-lock.json）

ASP.NET Core 8 + xUnit + JwtBearer + NSwag codegen + TRX trace 适配器。明细见 `version-lock.json`。

门禁命令见 `.harness/stack.json`。**不要改它来让门变松。**

## 4. 验收

- suite 根目录跑 `python scripts/gate.py -p lab-management-system-aspnetcore`
- 改了 shared → `bash scripts/gen-shared.sh` 再跑门禁

## 5. 指向别处

- 契约真源 → `../lab-management-system-shared`
- 决策 → `docs/adr/`；细则 → `docs/conventions/`；待办 → `PLAN.md`；版本 → `CHANGELOG.md`

## 6. 工作循环

1. 改业务实现 → `src/Controllers/Implementation/`
2. gate exit 1 修；exit 2 停下问人
3. `/handoff` 更新 `.state/session.json`
