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
#   - 数据库：PostgreSQL 远程, DATABASE_URL 从 env 注入
#     （EF Core / Npgsql 无 ./data 卷, 程序重启数据不丢；与 lab-springboot 共用 lab_dev/lib 库）
#   - 容器内 ASP.NET Core 8 监听 :5204（conventions §6）；host=container=5204
#     （ADR-0018 单层 port 方案，docker run -p 127.0.0.1:5204:5204；lab 家族 X04 段）
#   - 密钥走 ./aspnetcore.env (DATABASE_URL + JWT_SIGNING_KEY + LAB_CORS_ALLOWED_ORIGINS),
#     setup-vps.sh 不预生成（fail-fast 不便）,本脚本首启自举。
#   - JWT_SIGNING_KEY（HS256 ≥32B, 签 lab 自家 JWT）**不**写入默认 dev 值: 生产路径 = 必填;
#     缺则 fail-fast（lab-springboot 同模式）。
#
# 前置: deploy 用户需在 docker 组中(sudo usermod -aG docker deploy)。
#        aspnetcore.env 必须由 setup-vps.sh 或本脚本首启生成
#        (DATABASE_URL + JWT_SIGNING_KEY 必填)。

set -eu

USERNAME="${1:-}"
PASSWORD="${2:-}"
VERSION="${3:-latest}"
IMAGE="${USERNAME}/lab-management-system-aspnetcore:${VERSION}"
BASE="/home/deploy/lab-management-system-aspnetcore"
CONTAINER_NAME="lab-management-system-aspnetcore"

# nginx domain（deploy 脚本渲染 nginx vhost 时用）
NGINX_DOMAIN="${NGINX_DOMAIN:-lab-aspnetcore.xiangru.uk}"
NGINX_CERT_BASENAME="${NGINX_CERT_BASENAME:-xiangru-uk}"

if [ -z "$USERNAME" ] || [ -z "$PASSWORD" ]; then
  echo "Usage: $0 <DOCKER_USERNAME> <DOCKER_PASSWORD> [VERSION]" >&2
  exit 2
fi

# aspnetcore.env 自举保护: 缺失时, 如 $DATABASE_URL + $JWT_SIGNING_KEY
# + $LAB_SAAS_CLIENT_SECRET 在环境里, 自动生成（含 CORS 默认白名单 + SSO 配置）; 否则 fail fast。
# （DATABASE_USER/PASSWORD 不需要: 连接串内嵌凭据, 2026-08-28 删冗余 secrets。）
# setup-vps.sh 仍是首推（VPS 一次性, 生成 nginx vhost + 目录 + sudoers）, 本分支仅给
# "先有 DATABASE_URL 临时上线"的场景。
if [ ! -f "$BASE/aspnetcore.env" ]; then
  # 禁默认值兜底:secret 类(DATABASE_URL/JWT_SIGNING_KEY/LAB_SAAS_CLIENT_SECRET/
  # 服务账号)必须显式传入,缺哪个报哪个 —— LabOptions.SsoSection 的 dev 默认值
  # (alice/dev123456)只属于 dev,prod 静默吃它 = 菜单快照打错账号还无声。
  for _req in DATABASE_URL JWT_SIGNING_KEY LAB_SAAS_CLIENT_SECRET LAB_SAAS_SERVICE_USER LAB_SAAS_SERVICE_PASSWORD; do
    eval "_val=\"\${${_req}:-}\""
    if [ -z "$_val" ]; then
      echo "ERROR: $BASE/aspnetcore.env missing and env ${_req} not set (add GitHub Secret ${_req} → ci.yml envs → ssh-action envs)" >&2
      exit 1
    fi
  done
  echo "→ bootstrapping $BASE/aspnetcore.env (key 集合 = .env.production, suite L0.5 锁死)"
  umask 077
  {
    printf 'DATABASE_URL=%s\n' "$DATABASE_URL"
    printf 'DATABASE_NAME=lab_prod\n'
    printf 'SERVER_PORT=5204\n'
    # lab 仓 PostgreSQL 路径（与 lab-springboot 同库）：Provider=ef；
    # 2026-08-28 key 统一:flat LAB_DATA_PROVIDER(Lab__Data__Provider 段映射废弃)
    printf 'LAB_DATA_PROVIDER=ef\n'
    # JWT 签名密钥（HS256 ≥32B）。prod 必填 —— 不落 dev 默认值。
    # StateCookieManager 也复用同一密钥（HS256 签 SSO state），所以只填这一个。
    # key 名与 Program.cs 读者一致（JWT_SIGNING_KEY；曾写 Lab__Jwt__Secret 无 flat 读者，
    # 2026-08-28 断链修复）。Issuer/Ttl/RefreshTtl 显式写(值=契约值,不吃代码默认)。
    printf 'JWT_SIGNING_KEY=%s\n' "$JWT_SIGNING_KEY"
    printf 'JWT_ISSUER=lab-management-system\n'
    printf 'JWT_AUDIENCE=lab-management-system-clients\n'
    printf 'JWT_TTL_SECONDS=3600\n'
    printf 'JWT_REFRESH_TTL_SECONDS=604800\n'
    # CORS 白名单：lab 前端三仓 + 同域（与 lab-springboot.springboot.env 同源集合）。
    # Program.cs 只读 flat key LAB_CORS_ALLOWED_ORIGINS（Phase 4 起老 key Lab__Cors__* 废弃）。
    printf 'LAB_CORS_ALLOWED_ORIGINS=https://%s,https://lab-vue.xiangru.uk,https://lab-react.xiangru.uk,https://lab-nextjs.xiangru.uk,http://localhost:5201,http://localhost:5202,http://localhost:5203\n' "$NGINX_DOMAIN"
    # SSO 跳板：v0.1.9 接 saas-aspnetcore v0.2.0 真 OAuth IdP（同栈匹配 —— ADR xxc-cuddling 决策 §1）
    # client_id 是固定 UUID (11111111-...) 不是字符串 'lab-mgmt', 因为 shared/openapi.yaml
    # TypeSpec @format("uuid") 给 saas-aspnetcore/saas-springboot NSwag codegen 生成 Guid/UUID,
    # saas-nextjs 走 string. 固定 UUID 是跨 3 saas 后端的最小公约数. (后续 PR 改 TypeSpec
    # 移除 @format 后可改回 'lab-mgmt')
    # 2026-08-28 key 统一:Lab__Sso__* 段映射全部废弃,flat key 与 lab-springboot 同名
    printf 'LAB_SSO_PROFILE=real\n'
    printf 'LAB_SAAS_BASE_URL=https://saas-aspnetcore.xiangru.uk\n'
    # 登录 UI 同栈匹配：lab-vue 后端是 lab-aspnetcore → 登录页指 saas-vue
    #（2026-08-29 前指 saas-react；saas-vue LoginPage 已补 OAuth code 回跳）
    printf 'LAB_SSO_LOGIN_URL=https://saas-vue.xiangru.uk\n'
    printf 'LAB_SAAS_CLIENT_ID=11111111-1111-1111-1111-111111111111\n'
    printf 'LAB_SAAS_CLIENT_SECRET=%s\n' "$LAB_SAAS_CLIENT_SECRET"
    printf 'LAB_SAAS_DEFAULT_TENANT_ID=%s\n' "${LAB_SAAS_DEFAULT_TENANT_ID:-00000000-0000-0000-0000-000000000001}"
    printf 'LAB_SSO_CALLBACK_REDIRECT=https://lab-react.xiangru.uk/login\n'
    printf 'LAB_SAAS_SERVICE_USER=%s\n' "$LAB_SAAS_SERVICE_USER"
    printf 'LAB_SAAS_SERVICE_PASSWORD=%s\n' "$LAB_SAAS_SERVICE_PASSWORD"
  } > "$BASE/aspnetcore.env"
  chown deploy:deploy "$BASE/aspnetcore.env" 2>/dev/null || true
  chmod 600 "$BASE/aspnetcore.env"
fi
# 校验 aspnetcore.env 里有 DATABASE_URL + JWT_SIGNING_KEY
# （即使 env-file 已存在, 内容可能是上一次失败留下的）
# 老契约迁移提示: 旧 env-file 里是 Lab__Jwt__Secret/Lab__Sso__Profile/双写
# Lab__Data__ConnectionString 等 —— 二选一: 手工改 key 名, 或备份后删掉 env-file
# 带 secrets 重跑本脚本重建。改后必须走本脚本重建容器（--env-file 只在 create 时读）。
if ! grep -q '^DATABASE_URL=' "$BASE/aspnetcore.env"; then
  echo "ERROR: $BASE/aspnetcore.env has no DATABASE_URL line" >&2
  exit 1
fi
if ! grep -q '^JWT_SIGNING_KEY=' "$BASE/aspnetcore.env"; then
  echo "ERROR: $BASE/aspnetcore.env has no JWT_SIGNING_KEY line (old key Lab__Jwt__Secret? see migration note above)" >&2
  exit 1
fi
# v0.1.9: SSO 配置校验 + 2026-08-28 key 对齐迁移(append-if-missing 到
# .env.production 全集;key 集合契约由 suite L0.5 check_deploy_parity 锁死)。
# secret 类缺了 fail-fast 不再 WARNING 降级 —— 静默 no-sso 在 prod 是事故不是兜底。
if [ -f "$BASE/aspnetcore.env" ]; then
  append_if_missing() {
    key="$1"; val="$2"
    if ! grep -q "^${key}=" "$BASE/aspnetcore.env"; then
      echo "→ append ${key} to existing $BASE/aspnetcore.env"
      umask 077
      printf '%s=%s\n' "$key" "$val" >> "$BASE/aspnetcore.env"
    fi
  }
  append_if_missing LAB_SSO_PROFILE 'real'
  append_if_missing LAB_DATA_PROVIDER 'ef'
  append_if_missing LAB_SAAS_BASE_URL 'https://saas-aspnetcore.xiangru.uk'
  # v0.2.7: 登录 UI 同栈匹配 saas-vue（lab-vue 后端 = 本仓；ADR-0014 T-10 曾指
  # saas-react，现改 saas-vue 对齐 vue 栈）。存量 env 里脚本旧默认 saas-react
  # 原地迁移；自定义值不动。
  if ! grep -q '^LAB_SSO_LOGIN_URL=' "$BASE/aspnetcore.env"; then
    append_if_missing LAB_SSO_LOGIN_URL 'https://saas-vue.xiangru.uk'
  elif grep -q '^LAB_SSO_LOGIN_URL=https://saas-react\.xiangru\.uk$' "$BASE/aspnetcore.env"; then
    echo "→ migrate stale LAB_SSO_LOGIN_URL saas-react -> saas-vue (同栈匹配) in $BASE/aspnetcore.env"
    sed -i 's#^LAB_SSO_LOGIN_URL=https://saas-react\.xiangru\.uk$#LAB_SSO_LOGIN_URL=https://saas-vue.xiangru.uk#' "$BASE/aspnetcore.env"
  fi
  append_if_missing LAB_SAAS_CLIENT_ID '11111111-1111-1111-1111-111111111111'
  if ! grep -q '^LAB_SAAS_CLIENT_SECRET=' "$BASE/aspnetcore.env"; then
    if [ -z "${LAB_SAAS_CLIENT_SECRET:-}" ]; then
      echo "ERROR: LAB_SAAS_CLIENT_SECRET missing in $BASE/aspnetcore.env and not forwarded via ci.yml envs (静默降级 no-sso 在 prod 是事故)" >&2
      exit 1
    fi
    append_if_missing LAB_SAAS_CLIENT_SECRET "$LAB_SAAS_CLIENT_SECRET"
  fi
  append_if_missing LAB_SAAS_DEFAULT_TENANT_ID '00000000-0000-0000-0000-000000000001'
  append_if_missing LAB_SSO_CALLBACK_REDIRECT 'https://lab-react.xiangru.uk/login'
  if ! grep -q '^LAB_SAAS_SERVICE_USER=' "$BASE/aspnetcore.env"; then
    if [ -z "${LAB_SAAS_SERVICE_USER:-}" ] || [ -z "${LAB_SAAS_SERVICE_PASSWORD:-}" ]; then
      echo "ERROR: LAB_SAAS_SERVICE_USER/PASSWORD missing in $BASE/aspnetcore.env and not forwarded via ci.yml envs (LabOptions dev 默认 alice 只属于 dev)" >&2
      exit 1
    fi
    append_if_missing LAB_SAAS_SERVICE_USER "$LAB_SAAS_SERVICE_USER"
    append_if_missing LAB_SAAS_SERVICE_PASSWORD "$LAB_SAAS_SERVICE_PASSWORD"
  fi
  append_if_missing DATABASE_NAME 'lab_prod'
  append_if_missing SERVER_PORT '5204'
  append_if_missing JWT_ISSUER 'lab-management-system'
  append_if_missing JWT_AUDIENCE 'lab-management-system-clients'
  append_if_missing JWT_TTL_SECONDS '3600'
  append_if_missing JWT_REFRESH_TTL_SECONDS '604800'
  # 死键清理:Lab__Sso__*/Lab__Data__* 段映射已废弃(key 统一改 flat,老 env-file
  # 迁移时删除);Lab__Cors__AllowedOrigins 无读者同理
  for dead in Lab__Sso__SaasBase Lab__Sso__LoginUrl Lab__Sso__ClientId Lab__Sso__ClientSecret Lab__Sso__DefaultTenantId Lab__Sso__ServiceUser Lab__Sso__ServicePassword Lab__Sso__CallbackRedirectBase Lab__Data__Provider Lab__Cors__AllowedOrigins; do
    if grep -q "^${dead}=" "$BASE/aspnetcore.env"; then
      echo "→ drop legacy key ${dead} from $BASE/aspnetcore.env (key 统一为 flat)"
      umask 077
      sed -i "/^${dead}=/d" "$BASE/aspnetcore.env"
    fi
  done
fi

# nginx vhost 重渲染（每次 deploy 都跑,ADR-0018:容器端口变了 vhost 必须跟）:
# 模板从 master 拉,渲染后写入 sites-available,symlink sites-enabled,再 sudo nginx -t + reload。
# diff 检测:内容未变跳过 reload (nginx -t 也省)。
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

# 渲染到临时文件 —— sed 同时覆盖 3 种 placeholder:
#   Style A (lab-vue/react):      <domain>
#   Style B/C (nextjs/sp/aspc):   lab.YOUR_DOMAIN / saas.YOUR_DOMAIN
#   cert 路径: your-cert.{crt,cert} / <domain>.crt → 统一到 ${NGINX_CERT_BASENAME}.cert
TMP_VHOST="$(mktemp -t vpstpl.XXXXXX)"
sed \
  -e "s|<domain>|${NGINX_DOMAIN}|g" \
  -e "s|lab\.YOUR_DOMAIN|${NGINX_DOMAIN}|g" \
  -e "s|saas\.YOUR_DOMAIN|${NGINX_DOMAIN}|g" \
  -e "s|/etc/nginx/ssl/<domain>\.crt|/etc/nginx/ssl/${NGINX_CERT_BASENAME}.cert|g" \
  -e "s|/etc/nginx/ssl/<domain>\.key|/etc/nginx/ssl/${NGINX_CERT_BASENAME}.key|g" \
  -e "s|/etc/nginx/ssl/your-cert\.crt|/etc/nginx/ssl/${NGINX_CERT_BASENAME}.cert|g" \
  -e "s|/etc/nginx/ssl/your-cert\.cert|/etc/nginx/ssl/${NGINX_CERT_BASENAME}.cert|g" \
  -e "s|/etc/nginx/ssl/your-cert\.key|/etc/nginx/ssl/${NGINX_CERT_BASENAME}.key|g" \
  "${NGINX_TEMPLATE}" > "${TMP_VHOST}"

# diff 检测:已有 vhost 且内容相同就 skip,不同才重写 + reload
if [ -e "${NGINX_VHOST_FILE}" ] && diff -q "${TMP_VHOST}" "${NGINX_VHOST_FILE}" >/dev/null 2>&1; then
  echo "→ nginx vhost ${NGINX_VHOST_FILE} unchanged, skip"
  rm -f "${TMP_VHOST}"
else
  echo "→ rendering nginx vhost ${NGINX_VHOST_FILE} (domain=${NGINX_DOMAIN} cert=${NGINX_CERT_BASENAME})"
  # 写入 sites-available (deploy 用户可能没写权限,需要 sudoers 配 nginx 白名单)
  if [ -w "${NGINX_SITES_AVAILABLE}" ]; then
    cp "${TMP_VHOST}" "${NGINX_VHOST_FILE}"
  else
    sudo cp "${TMP_VHOST}" "${NGINX_VHOST_FILE}" \
      || { echo "ERROR: sudo cp ${NGINX_VHOST_FILE} failed"; rm -f "${TMP_VHOST}"; exit 1; }
  fi
  # symlink sites-enabled
  if [ -w "${NGINX_SITES_ENABLED}" ]; then
    ln -sf "${NGINX_VHOST_FILE}" "${NGINX_VHOST_LINK}"
  else
    sudo ln -sf "${NGINX_VHOST_FILE}" "${NGINX_VHOST_LINK}" \
      || { echo "ERROR: sudo ln ${NGINX_VHOST_LINK} failed"; rm -f "${TMP_VHOST}"; exit 1; }
  fi
  rm -f "${TMP_VHOST}"
  # nginx config test + reload (CI 自动完成,不再依赖手工)
  echo "→ nginx -t"
  sudo nginx -t
  echo "→ systemctl reload nginx"
  sudo systemctl reload nginx
  echo "✓ nginx reloaded"
fi

# 必要时补 CORS 白名单（已有则不覆盖, 运维手工补的 prod origin 不会丢）。
# Program.cs 只读 flat key LAB_CORS_ALLOWED_ORIGINS(Phase 4 对称化;老分段
# Lab__Cors__AllowedOrigins 无读者,上面迁移段已删,不再双写)。
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
  -p "127.0.0.1:5204:5204" \
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
  if wget --tries=1 --timeout=3 -q "http://127.0.0.1:5204/health" -O /dev/null 2>/dev/null; then
    echo "→ /health 200 (host 127.0.0.1:5204) after ${i}s"
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