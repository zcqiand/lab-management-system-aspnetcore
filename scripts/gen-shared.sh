#!/bin/bash
# Generate ASP.NET Core Controllers + DTOs from lab-shared's OpenAPI.yaml.
# 镜像 saas-identity-platform-aspnetcore/scripts/gen-shared.sh（v0.2.0 模式）：
#   shared 仓 = TypeSpec → OpenAPI.yaml 纯契约源，本仓用 NSwag CLI 现生成。
#
# 产物：
#   - src/Controllers/Generated/{Tag}Controller.cs — abstract 基类（§2.2 按 tag 拆，14 个）
#   - src/Models/Generated/*.cs — DTO + enum（§2.2 split 自 AllGenerated.cs，151 个）
#
# §2.2 决策（2026-09-17）：NSwag 单次 run 产 AllGenerated.cs（中间产物）→ patch-generated.py
# 修补 → split-nswag-output.py 按 ClassDeclarationSyntax 切分 14 controllers + 151 models。
# AllGenerated.cs 在 split 后删除。NSwag 原生不支持 per-tag 拆分（multipleClients+outputPerOperation
# 实测无效，{controller} token 不会按 tag 替换）。
#
# 手写 controller 放 src/Controllers/Implementation/{Tag}Controller.cs，
# partial 继承生成基类提供业务逻辑（镜像 springboot 的 api/controller 分层）。
#
# DB：本仓与 lab_dev 共库但 EF 不 Migrate（shared SQL 是 SSOT，启动只校验），
# 与 springboot 仓的 Flyway baseline-v13 冻结策略同一哲学——不重复建表。
set -euo pipefail

SHARED_DIR="$(cd "$(dirname "$0")/../../lab-management-system-shared" && pwd)"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OPENAPI="$SHARED_DIR/generated/openapi/openapi.yaml"
NSWAG_CONFIG="$ROOT/aspnetcore.nswag"

echo "[gen-shared] step 1/2 — lab-shared: emit OpenAPI.yaml..."
(cd "$SHARED_DIR" && npm run emit:openapi)

if [ ! -f "$OPENAPI" ]; then
  echo "[gen-shared] ERROR: missing $OPENAPI" >&2
  exit 1
fi

echo "[gen-shared] step 2/2 — NSwag → AllGenerated.cs → patch → split..."
mkdir -p "$ROOT/src/Controllers/Generated" "$ROOT/src/Models/Generated"

(cd "$ROOT" && nswag run "$NSWAG_CONFIG")

echo "[gen-shared] patch — NSwag 已知缺陷确定性修补（State / RequirementComparison / nullable query）..."
python "$ROOT/scripts/patch-generated.py"

# §2.2（2026-09-17）：NSwag 单文件 AllGenerated.cs → 按类拆为多文件
# （14 controllers + 151 models）。split 必须在 patch 之后，确保 patch 注入的
# State/nullable/#nullable enable 等修补随类落到对应文件。
python3 "$ROOT/scripts/split-nswag-output.py" "$ROOT/src/Controllers/Generated/AllGenerated.cs" \
  "$ROOT/src/Controllers/Generated" "$ROOT/src/Models/Generated" \
  || { echo "[gen-shared] ERROR: split failed" >&2; exit 1; }

# 删除合并前的大文件（已拆出 14+151 个 per-class 文件）
rm -f "$ROOT/src/Controllers/Generated/AllGenerated.cs"

# §X.1（2026-09-17）漂移自检：Implementation 是手写 partial 业务，与 NSwag 重生成的
# abstract 不同源（见 docs/conventions/codegen-impl-drift.md）。regen 后立即 build
# 失败即 fail-fast（人工看 build 红在 regen 完成后才发现是迟发现，已修契约
# 见 §4.2）。
echo "[gen-shared] drift self-check — build 验证 Implementation ↔ Generated 对齐..."
if ! dotnet build "$ROOT/src/Lab.AspNetCore.csproj" --nologo -v quiet > "$ROOT/.gen-shared-build.log" 2>&1; then
  echo "[gen-shared] ERROR: build failed after regen — Implementation 漂移" >&2
  echo "[gen-shared] tail of build log:" >&2
  tail -40 "$ROOT/.gen-shared-build.log" >&2
  echo "" >&2
  echo "[gen-shared] 修复方向见 docs/conventions/codegen-impl-drift.md §4.2" >&2
  echo "[gen-shared] （修 Implementation + Service，不能改 Generated/abstract）" >&2
  exit 1
fi
rm -f "$ROOT/.gen-shared-build.log"

echo "[gen-shared] OK"
