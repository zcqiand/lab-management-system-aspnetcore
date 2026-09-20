# 已废弃功能子项（历史档案）

> **本文件是 [function-tree.md](./function-tree.md) 的历史档案**——收纳已废弃 I 级 ID，行不删号不回收
>（同 saas-identity-platform-shared/docs/functions/deprecated-items.md 模式；格式同源，本仓条目少故单节）。
>
> **怎么用**：
> - 看活的 ID → function-tree.md
> - 看已废弃 ID + 迁移去向 → 本文件 § 1
> - 改已废弃 ID → 本文件 § 2 维护约定：废弃只改状态，编号不复用

## § 1. 已废弃功能子项

| 子项 ID | 名称 | 类型 | 交付 | 说明 / 迁移去向 | 状态 |
|---|---|---|---|---|---|
| M03.F08.I03 | 归档退回 | 接口 | 前端+后端 | 旧版：POST /api/receipts/flow，archived 下 RETURN→issuance。2026-09-17 /flow 端点删除（shared 13122e9 收敛 7 act 端点）后实现载体随之删除；src grep 零命中（receipts/flow、PostFlow、FlowRequest 全部无匹配，2026-09-21 复核）→ **无死代码可删，如实登记**。端点锚定收敛至 **M03.F08.I05**（POST /api/receipts/archived/act 仅收 SUBMIT 自转移 audit）。2026-09-21 随 SB 5.76 同标 已废弃（springboot 树同行 5.69 批已标，跨树分叉收口） | 已废弃 |
| M05.F02 | 仪表盘统计 | 工作台仪表盘 | 前端+后端 | 旧版：合同/接样/样品计数 + 按 3 桶聚合的报告状态 + 任务计数。模块级（F 级）已废弃，登记留档 | 已废弃 |

## § 2. 维护约定

- 废弃只改状态，编号不复用（CLAUDE.md §2 铁律）
- I 级行废弃时：function-tree.md 行状态改 `已废弃` + 本文件 § 1 同步登记（去向/理由/日期）
- 与 springboot 侧对齐：springboot 树 M03.F08.I03 已于 5.69 批标废弃（去向 M03.F08.I05），本仓 5.76 同标
