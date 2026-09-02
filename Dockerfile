# ===== lab-management-system-aspnetcore — ASP.NET Core 8 production image =====
# Multi-stage:
#   build    : dotnet:8.0-sdk → restore + publish self-contained=false
#   runtime  : aspnet:8.0 (debian-slim) → copy published app + ContentRoot
# 容器内监听 :5204 (ASPNETCORE_URLS=http://+:5204; conventions §6 端口分段);VPS nginx 反代到 host 8014
# (lab-aspnetcore.xiangru.uk — lab 家族 801x: vue=8010 react=8011 nextjs=8012 springboot=8013 aspnetcore=8014,
#  与 springboot 同 PostgreSQL 远程库, swap 时只换前端 env 里的 base URL)。

# ---------- Stage 1: builder ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /src

# 装 ca-certificates — slim 默认缺
RUN apt-get update -qq && apt-get install -y --no-install-recommends \
    ca-certificates \
 && rm -rf /var/lib/apt/lists/*

# 拷 csproj 先, restore 走 layer cache (csproj 不变就不重跑 restore)
COPY src/Lab.AspNetCore.csproj src/
RUN dotnet restore src/Lab.AspNetCore.csproj

# 拷全 + publish。Release + linux-x64 (容器是 debian-slim 但微软 8.0 镜像也是 debian,
# 所以 RID 默认即可)。Output 写到 /app/publish, ContentRootPath 用 AppContext.BaseDirectory
# 指 /app 让 appsettings*.json 也自动落到 /app。
COPY . .
RUN dotnet publish src/Lab.AspNetCore.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# appsettings*.json 在 src/, csproj Include 路径已含, publish 时一并落到 bin/,
# 已含在 publish 输出里。

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# 装 ca-certificates + wget —— mcr.microsoft.com/dotnet/aspnet:8.0 基于 debian-slim,
# 不带 wget;Docker HEALTHCHECK 用它探 /health。无 netcat/curl, wget 是最小依赖。
RUN apt-get update -qq && apt-get install -y --no-install-recommends \
    ca-certificates wget \
 && rm -rf /var/lib/apt/lists/*

# 非 root 跑（dotnet 镜像默认内置，非 0 即可；显式声明便于 security scan）
RUN groupadd --system --gid 1001 labasp \
 && useradd  --system --uid 1001 --gid labasp labasp

# 从 builder 拷 publish 产物
COPY --from=builder --chown=labasp:labasp /app/publish /app

# 容器内监听 :5204（conventions §6 端口分段：本地 dev 与容器内统一）。
# ContentRootPath=AppContext.BaseDirectory → /app, 自动找到 appsettings*.json。
ENV ASPNETCORE_URLS=http://+:5204 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true

EXPOSE 5204

USER labasp

# ASP.NET Core 8 冷启动在 1C2G VPS 上 5-15s;start-period=10s 太紧,第一次
# HEALTHCHECK 经常撞上 Application started 之前 → exit 1 → retries=3 在前 100s
# 内连续失败 → Docker 永久标 (unhealthy),即使容器实际在跑 /health 200。
# start-period=30s 给启动留余量;deploy.sh wget 探针仍是 ground truth
# (Docker HEALTHCHECK 跨 daemon 行为不一致 —— saas-springboot v0.1.8/09/10 教训)。
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD wget --tries=1 --timeout=3 -qO- http://127.0.0.1:5204/health >/dev/null || exit 1

ENTRYPOINT ["dotnet", "lab-management-system-aspnetcore.dll"]