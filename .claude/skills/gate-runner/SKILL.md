---
name: gate-runner
description: 在任何"我改完了"的时刻使用。跑门禁链并按 exit code 决定下一步。用户说跑门禁、检查、能提交了吗，也用它。
---

# Gate Runner

## 唯一真相

`python scripts/gate.py -p <项目>` 的 exit code。不是测试输出的措辞，不是「看起来没问题」。

## 门禁链的归属

```
L0 结构完整性   suite 拥有   语言无关   项目无权跳过
L1 格式         项目提供     .harness/stack.json
L2 静态检查     项目提供
L3 类型         项目提供（无类型系统的栈可省略）
L4 测试         项目提供
L5 引用完整性   suite 拥有   语言无关   项目无权跳过
```

## 步骤

1. `python scripts/gate.py -p <项目>`
2. 读 exit code：
   - `0` → 更新 `.state/session.json`，报告完成
   - `1` → 读 stderr 的修复提示，最小修复，回第 1 步
   - `2` → **契约或环境问题。停下来问人。** 不要修改 `.harness/stack.json`

## exit 2 是特殊的

它意味着「问题不在代码里」：缺 `stack.json`、缺依赖、门定义非法、trace.json 声称
skip 的测试有覆盖。这些都不该由你自行「修好」——你能做的修法（改契约）恰恰是最危险的。

## 反循环

同一道门连续失败 3 次 → 停下，报告：门的名字、原始输出、你试过的三种改法。不要第 4 次。

## 绝对禁止

- 改 `scripts/gate.py` 或 `.harness/stack.json` 让门变松
- 给断言加 skip/xfail 让 L4 变绿
- 用 `# noqa` / `@ts-ignore` / `eslint-disable` 让 L2/L3 变绿（除非同行注释给理由并写进 ADR）
- 手写 `.state/trace.json`

以上任何一条，都是把「通过门禁」当成了目标。门禁不是目标，它是目标的**度量**。

## 分级定位

```bash
python scripts/gate.py -p <项目> --only L3    # 单跑一道门，输出更干净
python scripts/gate.py -p <项目> --list       # 看清每道门归谁、跑什么命令
python scripts/gate.py --all                  # 所有项目
```
