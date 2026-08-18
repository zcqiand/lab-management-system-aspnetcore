#!/usr/bin/env python3
"""NSwag 生成代码的确定性修补（gen-shared.sh 每次生成后跑）。

镜像 lab-springboot gen-shared.sh 的 sed 修补哲学：生成器对 shared 契约里的
两处结构产不出合法 C#，修补必须留在脚本里，重跑 codegen 不丢。

已知缺陷（NSwag 14.7 + 本契约）：
  1. AuthContext.state 引用不存在的类型 State —— AuthState 是 oneOf 判别联合
     （4 态），NSwag 把属性类型解析成了不存在的 `State`。后端不消费该 DTO
     （frontend-bind 契约，前端 react/vue 用），改成 object 保持可编译。
  2. RequirementComparison 枚举值 "≥"/"≤" 清洗后都成 `_`，"="/"eq" 都成 `Eq`
     —— C# 标识符冲突。EnumMember 值保持线上格式不变，只改标识符。
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GENERATED = ROOT / "src" / "Controllers" / "Generated" / "Controllers.cs"


def patch_state_property(text: str) -> str:
    old = "public State State { get; set; } = new State();"
    # CS8669：生成文件无 #nullable 上下文，不能用 object? —— 用 object + 注释
    new = "public object State { get; set; } // oneOf AuthState 判别联合，NSwag 无法映射，后端不消费"
    if old in text:
        return text.replace(old, new)
    if new in text:
        return text  # 已修补（幂等）
    raise SystemExit(f"patch-generated: 未找到 State 属性模式 —— NSwag 输出变了，请更新修补脚本")


def patch_requirement_comparison(text: str) -> str:
    enum_block_re = re.compile(r"public enum RequirementComparison\b.*?\n    \}", re.DOTALL)
    m = enum_block_re.search(text)
    if not m:
        raise SystemExit("patch-generated: 未找到 RequirementComparison —— NSwag 输出变了，请更新修补脚本")
    block = m.group(0)
    # 按出现顺序重命名冲突成员；EnumMember Value 保持线上格式不变
    patched = block.replace("_ = 0,", "Ge = 0,", 1)
    patched = patched.replace("_ = 1,", "Le = 1,", 1)
    patched = patched.replace("Eq = 4,", "EqLower = 4,", 1)
    return text.replace(block, patched)


def patch_enum_converter(text: str) -> str:
    """net8.0 的 JsonStringEnumConverter<T> 不认 [EnumMember]（.NET 9 才支持），
    枚举线上格式会走 C# 成员名（"Manual" 而非契约 "manual"）-- 统一换
    Lab.AspNetCore.Serialization.EnumMemberEnumConverter<T>（live smoke 发现）。"""
    old = "System.Text.Json.Serialization.JsonStringEnumConverter<"
    new = "Lab.AspNetCore.Serialization.EnumMemberEnumConverter<"
    if old in text:
        return text.replace(old, new)
    if new in text:
        return text  # 已修补（幂等）
    raise SystemExit("patch-generated: 未找到 JsonStringEnumConverter 模式 -- NSwag 输出变了，请更新修补脚本")


def main() -> None:
    if not GENERATED.exists():
        raise SystemExit(f"patch-generated: missing {GENERATED}")
    text = GENERATED.read_text(encoding="utf-8")
    text = patch_state_property(text)
    text = patch_requirement_comparison(text)
    text = patch_enum_converter(text)
    GENERATED.write_text(text, encoding="utf-8")
    print("[patch-generated] OK")


if __name__ == "__main__":
    sys.exit(main())
