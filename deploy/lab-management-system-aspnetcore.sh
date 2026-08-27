#!/bin/sh
# Usage: lab-management-system-aspnetcore.sh <DOCKER_USERNAME> <DOCKER_PASSWORD> [VERSION]
#
# 由 .github/workflows/ci.yml 的 deploy job 远程调用:
#   ssh deploy@vps -- cd /home/deploy/lab-management-system-aspnetcore
#                    && sh lab-management-system-aspnetcore.sh $DOCKER_USERNAME $DOCKER_PASSWORD $VERSION
#
# VERSION 默认是 latest。tag-based deploy 时显式传 tag 名（v0.1.x-YYYYMMDD）。
# CI 同时 push :latest + :<tag> 两份镜像,回滚只要手动指定旧 tag 再跑一次本脚本。
#
# 与姊妹仓 saas-identity-platform-aspnetcore.sh 的差异:
#   - 数据库：PostgreSQL 远程, ConnectionStrings__Postgres 从 env 注入
#     （EF Core / Npgsql 无 ./data 卷, 程序重启数据不丢；与 lab-springboot 共用 lab_dev/lib 库）
#   - 容器内是 ASP.NET Core 8 监听 :8080 → -p 127.0.0.1:8014:8080
#     （lab 家族 801x: vue=8010 react=8011 nextjs=8012 springboot=8013 aspnetcore=8014）
#   - 密钥走 ./aspnetcore.env (ConnectionStrings__Postgres + Lab__Jwt__Secret + Lab__Cors__AllowedOrigins),
#     setup-vps.sh 不预生成（fail-fast 不便）,本脚本首启自举。
#   - LAB_JWT_SECRET（HS256 ≥32B, 签 lab 自家 JWT）**不**写入默认 dev 值: 生产路径 = 必填;
#     缺则 fail-fast（lab-springboot 同模式）。
#
# 前置: deploy 用户需在 docker 组中(sudo usermod -aG docker deploy)。
#        aspnetcore.env 必须由 setup-vps.sh 或本脚本首启生成
#        (ConnectionStrings__Postgres + Lab__Jwt__Secret 必填)。

set -eu

USERNAME="${1:-}"
PASSWORD="${2:-}"
VERSION="${3:-latest}"
IMAGE="${USERNAME}/lab-management-system-aspnetcore:${VERSION}"
BASE="/home/deploy/lab-management-system-aspnetcore"
CONTAINER_NAME="lab-management-system-aspnetcore"
HOST_PORT=8014

# nginx domain（deploy 脚本渲染 nginx vhost 时用）
NGINX_DOMAIN="${NGINX_DOMAIN:-lab-aspnetcore.xiangru.uk}"
NGINX_CERT_BASENAME="${NGINX_CERT_BASENAME:-xiangru-uk}"

if [ -z "$USERNAME" ] || [ -z "$PASSWORD" ]; then
  echo "Usage: $0 <DOCKER_USERNAME> <DOCKER_PASSWORD> [VERSION]" >&2
  exit 2
fi

# aspnetcore.env 自举保护: 缺失时, 如 $DATABASE_URL + $DATABASE_USER + $DATABASE_PASSWORD + $LAB_JWT_SECRET
# + $LAB_SAAS_CLIENT_SECRET 在环境里, 自动生成（含 CORS 默认白名单 + SSO 配置）; 否则 fail fast。
# setup-vps.sh 仍是首推（VPS 一次性, 生成 nginx vhost + 目录 + sudoers）, 本分支仅给
# "先有 DATABASE_URL 临时上线"的场景。
if [ ! -f "$BASE/aspnetcore.env" ]; then
  if [ -n "${DATABASE_URL:-}" ] && [ -n "${DATABASE_USER:-}" ] && [ -n "${DATABASE_PASSWORD:-}" ] && [ -n "${LAB_JWT_SECRET:-}" ] && [ -n "${LAB_SAAS_CLIENT_SECRET:-}" ]; then
    echo "→ bootstrapping $BASE/aspnetcore.env from env DATABASE_URL/USER/PASSWORD + LAB_JWT_SECRET + LAB_SAAS_*"
    umask 077
    {
      printf 'ConnectionStrings__Postgres=%s\n' "$DATABASE_URL"
      # lab 仓 PostgreSQL 路径（与 lab-springboot 同库）：Provider=ef + ConnectionString
      printf 'Lab__Data__Provider=ef\n'
      printf 'Lab__Data__ConnectionString=%s\n' "$DATABASE_URL"
      # JWT 签名密钥（HS256 ≥32B）。prod 必填 —— 不落 dev 默认值。
      # StateCookieManager 也复用同一密钥（HS256 签 SSO state），所以只填这一个。
      printf 'Lab__Jwt__Secret=%s\n' "$LAB_JWT_SECRET"
      printf 'Lab__Jwt__Issuer=lab-management-system\n'
      printf 'Lab__Jwt__TtlSeconds=3600\n'
      printf 'Lab__Jwt__RefreshTtlSeconds=604800\n'
      # CORS 白名单：lab 前端三仓 + 同域（与 lab-springboot.springboot.env 同源集合）。
      # Phase 4 env 对称化（2026-08-26 起）：Program.cs 只读 flat key LAB_CORS_ALLOWED_ORIGINS，
      # 老 key Lab__Cors__AllowedOrigins 已废弃。两行都写兼容旧容器/旧镜像。
      printf 'Lab__Cors__AllowedOrigins=https://%s,https://lab-vue.xiangru.uk,https://lab-react.xiangru.uk,https://lab-nextjs.xiangru.uk,http://localhost:5173,http://localhost:5174\n' "$NGINX_DOMAIN"
      printf 'LAB_CORS_ALLOWED_ORIGINS=https://%s,https://lab-vue.xiangru.uk,https://lab-react.xiangru.uk,https://lab-nextjs.xiangru.uk,http://localhost:5173,http://localhost:5174\n' "$NGINX_DOMAIN"
      # SSO 跳板：v0.1.9 接 saas-aspnetcore v0.2.0 真 OAuth IdP（同栈匹配 —— ADR xxc-cuddling 决策 §1）
      # client_id 是固定 UUID (11111111-...) 不是字符串 'lab-mgmt', 因为 shared/openapi.yaml
      # TypeSpec @format("uuid") 给 saas-aspnetcore/saas-springboot NSwag codegen 生成 Guid/UUID,
      # saas-nextjs 走 string. 固定 UUID 是跨 3 saas 后端的最小公约数. (后续 PR 改 TypeSpec
      # 移除 @format 后可改回 'lab-mgmt')
      printf 'Lab__Sso__Profile=real\n'
      printf 'Lab__Sso__SaasBase=https://saas-aspnetcore.xiangru.uk\n'
      printf 'Lab__Sso__LoginUrl=https://saas-nextjs.xiangru.uk\n'
      printf 'Lab__Sso__ClientId=11111111-1111-1111-1111-111111111111\n'
      printf 'Lab__Sso__ClientSecret=%s\n' "$LAB_SAAS_CLIENT_SECRET"
      printf 'Lab__Sso__DefaultTenantId=%s\n' "${LAB_SAAS_DEFAULT_TENANT_ID:-00000000-0000-0000-0000-000000000001}"
    } > "$BASE/aspnetcore.env"
    chown deploy:deploy "$BASE/aspnetcore.env" 2>/dev/null || true
    chmod 600 "$BASE/aspnetcore.env"
  else
    echo "ERROR: $BASE/aspnetcore.env missing. Set DATABASE_URL/USER/PASSWORD + LAB_JWT_SECRET + LAB_SAAS_CLIENT_SECRET env (e.g. DATABASE_URL='Host=100.79.128.25;Port=5432;Database=lab_prod;Username=postgres;Password=...' DATABASE_USER=postgres DATABASE_PASSWORD=... LAB_JWT_SECRET=<32B+ random> LAB_SAAS_CLIENT_SECRET=<saas-aspnetcore V014 seeded client secret> sudo -E sh deploy/setup-vps.sh lab-aspnetcore.example.com) or run setup-vps.sh first." >&2
    exit 1
  fi
fi
# 校验 aspnetcore.env 里有 ConnectionStrings__Postgres + Lab__Jwt__Secret
# （即使 env-file 已存在, 内容可能是上一次失败留下的）
if ! grep -q '^ConnectionStrings__Postgres=' "$BASE/aspnetcore.env"; then
  echo "ERROR: $BASE/aspnetcore.env has no ConnectionStrings__Postgres line" >&2
  exit 1
fi
if ! grep -q '^Lab__Jwt__Secret=' "$BASE/aspnetcore.env"; then
  echo "ERROR: $BASE/aspnetcore.env has no Lab__Jwt__Secret line" >&2
  exit 1
fi
# v0.1.9: SSO 配置校验（缺失则降级到 no-sso profile，不阻断 deploy）
# 已有部署可能没装 SSO env, deploy.sh append-only 写缺失的 4 行, 不覆盖运维手工的。
if ! grep -q '^Lab__Sso__Profile=' "$BASE/aspnetcore.env"; then
  if [ -n "${LAB_SAAS_CLIENT_SECRET:-}" ]; then
    echo "→ append Lab SS_SO_* to existing $BASE/aspnetcore.env"
    umask 077
    {
      printf 'Lab__Sso__Profile=real\n'
      printf 'Lab__Sso__SaasBase=https://saas-aspnetcore.xiangru.uk\n'
      printf 'Lab__Sso__LoginUrl=https://saas-nextjs.xiangru.uk\n'
      printf 'Lab__Sso__ClientId=11111111-1111-1111-1111-111111111111\n'
      printf 'Lab__Sso__ClientSecret=%s\n' "$LAB_SAAS_CLIENT_SECRET"
      printf 'Lab__Sso__DefaultTenantId=%s\n' "${LAB_SAAS_DEFAULT_TENANT_ID:-00000000-0000-0000-0000-000000000001}"
    } >> "$BASE/aspnetcore.env"
    chmod 600 "$BASE/aspnetcore.env"
  else
    echo "→ WARNING: LAB_SAAS_CLIENT_SECRET missing, SSO env not appended (lab-aspnetcore will run with Lab__Sso__Profile=no-sso, /api/auth/sso/authorize returns 500)" >&2
  fi
fi

# v0.1.13 起: LoginUrl（IdP 登录页 = saas 前端域名）。早期 env 只有 SaasBase（API 域名），
# authorizeUrl 曾拼出 {API}/login 404。已有 env append-only 补这一行。
if ! grep -q '^Lab__Sso__LoginUrl=' "$BASE/aspnetcore.env"; then
  echo "→ append Lab__Sso__LoginUrl to existing $BASE/aspnetcore.env"
  umask 077
  printf 'Lab__Sso__LoginUrl=https://saas-nextjs.xiangru.uk\n' >> "$BASE/aspnetcore.env"
fi

# nginx vhost 自举（缺时创建, 不 reload —— reload 要 root）:
# 检测 /etc/nginx/sites-enabled/<NGINX_DOMAIN> 是否存在; 缺时从 nginx-vps.conf.example
# 模板渲染, 做 symlink。reload 需 sudo, 留给手工:
#   sudo nginx -t && sudo systemctl reload nginx
NGINX_SITES_AVAILABLE="/etc/nginx/sites-available"
NGINX_SITES_ENABLED="/etc/nginx/sites-enabled"
NGINX_VHOST_FILE="${NGINX_SITES_AVAILABLE}/${NGINX_DOMAIN}"
NGINX_VHOST_LINK="${NGINX_SITES_ENABLED}/${NGINX_DOMAIN}"
NGINX_TEMPLATE="${BASE}/nginx-vps.conf.example"

# 拉模板（deploy/ 目录随仓库 deploy 脚本一起, 但首次拉时可能不存在, 补一下）
if [ ! -f "${NGINX_TEMPLATE}" ]; then
  echo "→ fetching nginx-vps.conf.example template"
  curl -fsSL "https://raw.githubusercontent.com/zcqiand/lab-management-system-aspnetcore/refs/heads/master/deploy/nginx-vps.conf.example" -o "${NGINX_TEMPLATE}"
fi

if [ -e "${NGINX_VHOST_LINK}" ] || [ -e "${NGINX_VHOST_FILE}" ]; then
  echo "→ nginx vhost ${NGINX_VHOST_FILE} already exists, skip bootstrap"
else
  echo "→ nginx vhost missing, bootstrapping ${NGINX_VHOST_FILE} (domain=${NGINX_DOMAIN} cert=${NGINX_CERT_BASENAME})"
  if [ -w "${NGINX_SITES_AVAILABLE}" ]; then
    umask 022
    sed \
      -e "s/lab.YOUR_DOMAIN/${NGINX_DOMAIN}/g" \
      -e "s|/etc/nginx/ssl/your-cert.cert|/etc/nginx/ssl/${NGINX_CERT_BASENAME}.cert|g" \
      -e "s|/etc/nginx/ssl/your-cert.key|/etc/nginx/ssl/${NGINX_CERT_BASENAME}.key|g" \
      "${NGINX_TEMPLATE}" > "${NGINX_VHOST_FILE}"
    echo "→ wrote ${NGINX_VHOST_FILE} (direct, deploy user has write perms)"
  else
    echo "→ ${NGINX_SITES_AVAILABLE} not writable by $(id -un); need sudo (ensure /etc/sudoers.d/deploy-nginx allows: deploy ALL=(ALL) NOPASSWD: /bin/cp /bin/ln)"
    TMP_VHOST="$(mktemp)"
    sed \
      -e "s/lab.YOUR_DOMAIN/${NGINX_DOMAIN}/g" \
      -e "s|/etc/nginx/ssl/your-cert.cert|/etc/nginx/ssl/${NGINX_CERT_BASENAME}.cert|g" \
      -e "s|/etc/nginx/ssl/your-cert.key|/etc/nginx/ssl/${NGINX_CERT_BASENAME}.key|g" \
      "${NGINX_TEMPLATE}" > "${TMP_VHOST}"
    sudo cp "${TMP_VHOST}" "${NGINX_VHOST_FILE}" \
      && echo "→ wrote ${NGINX_VHOST_FILE} (via sudo cp)" \
      || { echo "→ ERROR: failed to write ${NGINX_VHOST_FILE}"; exit 1; }
    rm -f "${TMP_VHOST}"
  fi
  if [ -w "${NGINX_SITES_ENABLED}" ]; then
    ln -sf "${NGINX_VHOST_FILE}" "${NGINX_VHOST_LINK}"
    echo "→ linked ${NGINX_VHOST_LINK} (direct)"
  else
    sudo ln -sf "${NGINX_VHOST_FILE}" "${NGINX_VHOST_LINK}" \
      && echo "→ linked ${NGINX_VHOST_LINK} (via sudo ln)" \
      || { echo "→ ERROR: failed to link ${NGINX_VHOST_LINK}"; exit 1; }
  fi
  echo "→ nginx vhost created. To enable: sudo nginx -t && sudo systemctl reload nginx"
fi

# 必要时补 CORS 白名单（已有则不覆盖, 运维手工补的 prod origin 不会丢）。
# Phase 4 env 对称化后：Program.cs 只读 flat key LAB_CORS_ALLOWED_ORIGINS。
# 两行都检查 + append（兼容 .NET env 双 key provider）。
if ! grep -q '^Lab__Cors__AllowedOrigins=' "$BASE/aspnetcore.env"; then
  echo "→ append Lab__Cors__AllowedOrigins to existing $BASE/aspnetcore.env"
  umask 077
  printf 'Lab__Cors__AllowedOrigins=https://%s,https://lab-vue.xiangru.uk,https://lab-react.xiangru.uk,https://lab-nextjs.xiangru.uk\n' "$NGINX_DOMAIN" >> "$BASE/aspnetcore.env"
fi
if ! grep -q '^LAB_CORS_ALLOWED_ORIGINS=' "$BASE/aspnetcore.env"; then
  echo "→ append LAB_CORS_ALLOWED_ORIGINS to existing $BASE/aspnetcore.env"
  umask 077
  printf 'LAB_CORS_ALLOWED_ORIGINS=https://%s,https://lab-vue.xiangru.uk,https://lab-react.xiangru.uk,https://lab-nextjs.xiangru.uk\n' "$NGINX_DOMAIN" >> "$BASE/aspnetcore.env"
fi

echo "→ image: $IMAGE"
echo "→ docker login"
printf '%s' "$PASSWORD" | docker login -u "$USERNAME" --password-stdin

echo "→ docker pull"
docker pull "$IMAGE"

echo "→ docker stop & rm $CONTAINER_NAME"
docker stop "$CONTAINER_NAME" 2>/dev/null || true
docker rm "$CONTAINER_NAME" 2>/dev/null || true

echo "→ docker run"
docker run -d \
  --name "$CONTAINER_NAME" \
  --restart unless-stopped \
  -p "127.0.0.1:${HOST_PORT}:8080" \
  --env-file "$BASE/aspnetcore.env" \
  "$IMAGE"

echo "→ docker image prune"
docker image prune -f

echo "→ docker ps"
docker ps --filter name="$CONTAINER_NAME"

# 健康检查: 直接 wget /health 探 200, 不依赖 Docker HEALTHCHECK 语义。
# Program.cs 末尾 MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous()
# —— 匿名端点, JwtBearer middleware 不阻挡。
# 与 saas-aspnetcore / saas-springboot 同模式: host 端口探针才可靠
# (Docker HEALTHCHECK 跨 daemon 行为不一致 —— saas-springboot v0.1.8/09/10 教训)。
i=0
while [ $i -lt 60 ]; do
  if wget --tries=1 --timeout=3 -q "http://127.0.0.1:${HOST_PORT}/health" -O /dev/null 2>/dev/null; then
    echo "→ /health 200 (host 127.0.0.1:${HOST_PORT}) after ${i}s"
    break
  fi
  # 容器实际死亡 (OOM / start-cmd failure / 立刻 crash) 提前终止循环, 立刻报失败。
  if ! docker inspect --format='{{.State.Running}}' "$CONTAINER_NAME" 2>/dev/null | grep -q true; then
    echo "→ container not running, logs:"
    docker logs --tail 30 "$CONTAINER_NAME"
    exit 1
  fi
  i=$((i+1))
  sleep 1
done

if [ $i -ge 60 ]; then
  echo "→ /health 仍未 200（60s 上限）, logs:"
  docker logs --tail 30 "$CONTAINER_NAME"
  exit 1
fi

echo "→ deploy done at $(date -u)"