# CLAUDE.md — 实验室管理系统ASP.NET-Core后端

> 入口，不是手册。只做三件事：声明技术栈 / 声明禁止事项 / 指向别处。
> 超过 5 行的细则写进 `docs/conventions/`，由 skill 按需引用。L0 门强制上限 60 行。

## 1. 技术栈

`aspnetcore` — ASP.NET Core 8 + xUnit + dotnet format + TRX trace 适配器

门禁命令见 `.harness/stack.json`。**不要改它来让门变松。**

## 2. 禁止事项（硬约束）

- 禁止在 Controller / Program.cs 里写业务逻辑
- 禁止 catch 后吞掉异常（不记录、不 rethrow）
- 禁止把连接串/密钥硬编码进源码（放 appsettings.Development.json 或环境变量）
- 禁止直接修改 `docs/functions/function-tree.md`；走 `/tree-change` 提案，由人批准
- 禁止先改代码后补功能清单；改功能与改功能清单必须同一个 commit
- 禁止删除功能清单里的行来消除告警；废弃只改状态，编号永不复用
- 禁止给 skip 的测试挂功能 ID
- 禁止在本文件里堆积细则

## 3. 指向别处

- 功能清单（唯一锚点） → `docs/functions/function-tree.md`
- 需求 → 任务 → 功能影响 → `docs/requirements/`
- 流程/设计 与功能对齐 → `docs/design/`（人评审，机器只查引用）
- 决策背景 → `docs/adr/`
- 编码细则 → `docs/conventions/`

## 4. 工作循环

0. **开工前分诊**：动手前先过 `using-skills`，判断该激活哪些 skill、把它们的清单落成 todo。
   顺序不可倒：规格(brainstorming)→计划(writing-plans)→测试先红(red-first)→实现(executing-plans)。
1. 读 `.state/session.json` 恢复上下文
2. 最小改动
3. 在 **suite 根目录** 跑 `python scripts/gate.py -p lab-management-system-aspnetcore`
4. exit 0 才算完成；非 0 回到第 2 步；exit 2 停下问人
5. `/handoff` 更新 `.state/session.json`
