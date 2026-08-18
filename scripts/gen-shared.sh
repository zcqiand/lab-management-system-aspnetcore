#!/bin/bash
# Generate ASP.NET Core Controllers + DTOs from lab-shared's OpenAPI.yaml.
# 镜像 saas-identity-platform-aspnetcore/scripts/gen-shared.sh（v0.2.0 模式）：
#   shared 仓 = TypeSpec → OpenAPI.yaml 纯契约源，本仓用 NSwag CLI 现生成。
#
# 产物：
#   - src/Controllers/Generated/{Tag}Controller.cs — abstract 基类，
#     方法 stub 抛 NotImplementedException
#   - src/Models/Generated/*.cs — DTO record
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

echo "[gen-shared] step 2/2 — NSwag → src/Controllers/Generated/ + src/Models/Generated/..."
mkdir -p "$ROOT/src/Controllers/Generated" "$ROOT/src/Models/Generated"

(cd "$ROOT" && nswag run "$NSWAG_CONFIG")

echo "[gen-shared] patch — NSwag 已知缺陷确定性修补（State / RequirementComparison）..."
python "$ROOT/scripts/patch-generated.py"

echo "[gen-shared] OK"
