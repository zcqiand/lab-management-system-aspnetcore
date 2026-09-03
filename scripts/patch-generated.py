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


def patch_nullable_query_params(text: str) -> str:
    """NSwag 把 optional query 参数（openapi required:false）生成为非 nullable 的
    `string`，配合 [ApiController] + NRT 的隐式必填（ASPNETCORE_DEFAULT_​REQUIRED
    行为）变成缺参即 400 —— 与 shared 契约「query 可省略」分叉（live contract-test
    的 /api/summary 不带 dateFrom 探针 2026-09-04 抓到）。全部改 `string?`，
    实现层自行处理 null（SummaryService 已按 null→"" 语义）。

    只改 FromQuery string（路径段 id / body 不动 —— body 本就有 Required 特性）。
    """
    pattern = re.compile(
        r"(\[Microsoft\.AspNetCore\.Mvc\.FromQuery\]) string (\w+)"
    )
    def _sub(m: re.Match) -> str:
        return f"{m.group(1)} string? {m.group(2)}"
    patched, n = pattern.subn(_sub, text)
    if n == 0:
        # 幂等：已经是 string? 就无 FromQuery string（非 ?）残留
        if re.search(r"\[Microsoft\.AspNetCore\.Mvc\.FromQuery\]\s+string\?", text):
            return text
        raise SystemExit("patch-generated: 未找到 FromQuery string 模式 -- NSwag 输出变了，请更新修补脚本")
    return patched


def patch_nullable_enable(text: str) -> str:
    """CS8669：生成文件默认无 nullable 上下文，patch_nullable_query_params 产出的
    `string?` 注解需要 `#nullable enable` 头。加在 auto-generated 注释块后。"""
    marker = "//----------------------\n"
    directive = (
        "#nullable enable\n"
        "#pragma warning disable CS8618 // patch-generated: string? 注解需显式上下文（CS8669）；\n"
        "#pragma warning disable CS8618 // 生成 DTO 的 _additionalProperties 惰性初始化与 NRT 全量检查不兼容，只压本文件\n"
    )
    if "#nullable enable" in text and "CS8618" in text:
        return text  # 幂等
    idx = text.find(marker)
    if idx == -1:
        raise SystemExit("patch-generated: 未找到文件头 marker -- NSwag 输出变了，请更新修补脚本")
    # 插在第一处 marker 之后（auto-generated 块之后会再出现一次结尾 marker）
    end = text.find(marker, idx + len(marker))
    insert_at = end + len(marker) if end != -1 else idx + len(marker)
    return text[:insert_at] + directive + text[insert_at:]


def main() -> None:
    if not GENERATED.exists():
        raise SystemExit(f"patch-generated: missing {GENERATED}")
    text = GENERATED.read_text(encoding="utf-8")
    text = patch_state_property(text)
    text = patch_requirement_comparison(text)
    text = patch_enum_converter(text)
    text = patch_nullable_query_params(text)
    text = patch_nullable_enable(text)
    GENERATED.write_text(text, encoding="utf-8")
    print("[patch-generated] OK")


if __name__ == "__main__":
    sys.exit(main())
