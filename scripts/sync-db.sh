#!/usr/bin/env bash
# scripts/sync-db.sh — DB-First schema 同步验证 + ADR-0026 marker（ADR-0025/ADR-0033）
#
# 设计（对 lab 仓架构的适配，偏差已记录在 ADR-0033 执行说明）：
# - 本仓运行时 EF 实体 = NSwag DTO（LabDbContext 手写映射，DTO==entity 零转换层），
#   不走 saas 式 `dotnet ef dbcontext scaffold`（scaffold 产物是另一套 string 属性
#   实体，毁 Wire 枚举转换器与 DTO 复用）。
# - 漂移防线 = tests/Harness/LabDbContextSchemaTest.cs：EF 全模型 ↔ 共享 PG 物化态
#   逐列对照（模型列 ⊆ 库列 / NOT NULL 无 default 列必映射 / PK 集合全等）。
#   DB 没改 → 测试绿（与 HEAD 同步）；shared schema.ts 真演进 + lab_test 已
#   migrate → 测试红 = 标准工作流，同步 LabDbContext 映射后 commit。
# - 本脚本：跑该测试（默认 lab_test，LAB_TEST_DATABASE_URL 可切 lab_dev），
#   绿 → 写 .state/last-gen-shared.json 的 db_synced_* 字段。
#
# 用法：
#   bash scripts/sync-db.sh
#   LAB_TEST_DATABASE_URL='Host=...;Database=lab_dev;...' bash scripts/sync-db.sh
#
# 退出码：
#   0 — schema 同步绿 + marker 已落盘
#   1 — 漂移（测试红）/ 测试基建失败

set -euo pipefail

# 不要用 `git rev-parse --show-toplevel` —— 本仓是 submodule，
# 该命令返回外层 xr-code-suite 根而不是本仓根。
cd "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/.."

echo "[sync-db] step 1/2 — dotnet test --filter LabDbContextSchemaTest"
if ! dotnet test tests/Lab.AspNetCore.Tests.csproj --filter "FullyQualifiedName~LabDbContextSchemaTest" --nologo -v minimal; then
  echo "[sync-db] FATAL: EF 模型与共享库物化 schema 漂移" >&2
  echo "[sync-db]        DB 没改时出现 = 本仓 LabDbContext 映射漂移，修映射；" >&2
  echo "[sync-db]        shared schema.ts 刚演进 = 标准工作流，同步映射后 commit" >&2
  exit 1
fi

echo "[sync-db] step 2/2 — ADR-0026 marker"
ROOT="$(pwd)"
SHARED_DIR="$(cd "${ROOT}/../lab-management-system-shared" && pwd)"
SHARED_SHA=$(cd "$SHARED_DIR" && git rev-parse HEAD)
MARKER="$ROOT/.state/last-gen-shared.json"
mkdir -p "$ROOT/.state"

# 注意 cmd 前缀：marker python 按 startswith 分流（"gen-shared" → api 字段，
# "scaffold"/"sync-db" → db 字段）。sync-db.sh 写 db 类别。
if python3 - "$MARKER" "$SHARED_SHA" "$(basename "$0")" "$(basename "$ROOT")" <<'PYEOF'
import datetime, json, sys

marker_path, shared_sha, cmd, repo = sys.argv[1:5]
try:
    with open(marker_path, encoding="utf-8") as f:
        marker = json.load(f)
except (FileNotFoundError, json.JSONDecodeError):
    marker = {}

now = datetime.datetime.now(datetime.timezone.utc).isoformat()
if cmd.startswith("gen-shared"):
    marker["api_synced_sha"] = shared_sha
    marker["api_synced_at"] = now
    marker["api_synced_cmd"] = cmd
elif cmd.startswith("scaffold") or cmd.startswith("sync-db"):
    marker["db_synced_sha"] = shared_sha
    marker["db_synced_at"] = now
    marker["db_synced_cmd"] = cmd

# shared_sha 取「最近一次同步」对应的 sha：ISO-8601 UTC 时间戳字典序==时间序。
# 勿用 max(sha)——SHA 字典序不是 git 时间序（5.21 事故）。
entries = [
    (marker.get(k + "_at", ""), marker[k + "_sha"])
    for k in ("api_synced", "db_synced")
    if marker.get(k + "_sha")
]
marker["shared_sha"] = max(entries)[1] if entries else shared_sha
marker["consumer_repo"] = repo

with open(marker_path, "w", encoding="utf-8") as f:
    json.dump(marker, f, ensure_ascii=False, indent=2)
    f.write("\n")
PYEOF
then
  echo "[sync-db]    ADR-0026 marker 已落盘: $MARKER (shared HEAD ${SHARED_SHA:0:7})"
else
  echo "[sync-db]    WARN: marker 写失败（python3 缺失？）—— staleness 将报 UNKNOWN" >&2
fi

echo "[sync-db] OK — EF 模型与共享库物化 schema 同步绿"
