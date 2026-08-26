# lab-management-system-aspnetcore Architecture

> 本仓架构视角：lab 家族后端 2/2（ASP.NET Core 8 + NSwag + xUnit）。
> 回答三个问题：
> 1. 本仓如何在 lab-shared 契约下，用 NSwag codegen + 手写 partial Controller 落地 API；
> 2. 业务数据（InMemoryStore / EF Core）与租户上下文（TenantGuard / ITenantContext）怎么协作；
> 3. 改一次契约 → 三端同步的核心流程在本仓怎么走。

> **范围**：本文档只描述 *架构*（结构 / 边界 / 数据流 / 决策）。
> 编码细则见 [docs/conventions/](conventions/)，单个决策的 ADR 见 [docs/adr/](adr/)，需求/功能见 [docs/requirements/](../requirements/) 与 [docs/functions/function-tree.md](functions/function-tree.md)。

---

## 0. 阅读路径

| 你是… | 直接看 |
|---|---|
| 新人，要 30 分钟搞懂本仓 | §1 → §2 → §3.1（NSwag 链）→ §4（启动流程） |
| 改 API 契约怎么同步 | §3.1 → §5 → [父仓 docs/ARCHITECTURE.md §5.1](../docs/ARCHITECTURE.md#51-改一次契约--三端同步codegen-链) |
| 想加新 endpoint | §3.2（手写 partial Controller）→ §3.4（TenantGuard）→ [function-tree.md](functions/function-tree.md) |
| 切换 InMemory ↔ EF | §3.3 → §3.6（Program.cs DI 分支）→ ADR-0010（§6） |
| 想问「为什么这样设计」 | §7（决策索引）→ 对应 ADR |

---

## 1. 角色与定位

**lab-management-system-aspnetcore 是 lab 家族的后端 2/2**：

| 维度 | 本仓 |
|---|---|
| 角色 | 后端 HTTP API server |
| 技术栈 | ASP.NET Core 8 + NSwag（C# controller codegen）+ xUnit + dotnet format |
| 端口 | 5000（dev default） |
| 契约源 | `../lab-management-system-shared/generated/openapi/openapi.yaml`（lab-shared `npm run emit:openapi` 产物） |
| 同构对侧 | `saas-identity-platform-aspnetcore`（结构 1:1，业务表不同） |
| 并行实现 | `lab-management-system-springboot`（同 lab-shared 契约；Spring Boot 实现） |
| 持久化 | dev=`Lab:Data:Provider=memory`（InMemory fixture）；prod=`ef`（Npgsql + EF Core，shared SQL 是 SSOT） |

**关键哲学**：

1. **路由不手写**：所有 `[Route]`/`[HttpGet]` 等由 NSwag 从 OpenAPI.yaml 生成；本仓只覆盖抽象方法提供业务。
2. **DB schema 不本地维护**：lab-shared 的 `sql/migrations/V*.sql` 是真源；本仓 EF Core 只做 *运行时校验*，不 `Migrate()`。与 lab-springboot 的 Flyway baseline-v13 冻结策略同哲学——不重复建表。
3. **partial class 分层**：`Generated/Controllers.cs` 持有 abstract 基类，`Implementation/<Tag>Controller.cs` 持有 partial 实现——镜像 springboot 的 `api/controller/` 双层结构。
5. **JWT HS256 真签**: ADR-0008 + Phase 2B 镜像 saas 删 `RequireSignedTokens=false` (v0.1.17+ 起统一 HS256，dev/prod 同 `TokenValidationParameters`)。

---

## 2. 目录骨架

```
lab-management-system-aspnetcore/
├── CLAUDE.md                          ← 入口：技术栈 + 禁止事项 + 指向别处
├── .harness/stack.json                ← suite 门禁读取的项目自描述
├── docs/
│   ├── functions/function-tree.md     ← F/I 级功能清单（M00..M06）
│   ├── adr/0008-real-backend-oauth-jwt.md  ← 本仓 ADR
│   ├── design/                        ← 流程/设计（人评审）
│   └── conventions/                   ← 本仓编码细则
├── scripts/
│   ├── gen-shared.sh                  ← 调 shared emit + nswag run + patch-generated.py
│   └── patch-generated.py             ← NSwag 已知缺陷确定性修补（State / RequirementComparison）
├── src/
│   ├── Program.cs                     ← DI 注册 + JwtBearer + CORS + dev/ef 分支
│   ├── appsettings.json               ← 默认配置
│   ├── appsettings.Development.json   ← dev 默认值（Lab:Sso:Profile=no-sso 等）
│   ├── Controllers/
│   │   ├── Generated/Controllers.cs   ← NSwag 产物（gitignored，gen-shared 重写）
│   │   └── Implementation/            ← 手写 partial class 覆盖 abstract 方法
│   │       ├── AuthController.cs
│   │       ├── ContractController.cs
│   │       ├── CatalogController.cs
│   │       ├── DictionaryController.cs
│   │       ├── MethodAndRequirementController.cs
│   │       └── SummaryController.cs
│   ├── Models/Generated/              ← NSwag 产物：DTO record（gitignored）
│   ├── Auth/                          ← ADR-0008 真 JWT 链路
│   │   ├── Jwt/LabJwtSigner.cs        ← HMAC HS256 签发
│   │   ├── Jwt/LabTokenValidationFactory.cs  ← TokenValidationParameters（拒 alg=none）
│   │   ├── Jwt/LabOptions.cs
│   │   ├── Sso/ISaasAuthClient.cs     ← saas /oauth/authorize + /oauth/token
│   │   ├── Sso/ISaasMeClient.cs       ← saas /me + /me/tenants
│   │   ├── Sso/SaasAuthException.cs   ← SaasErrorMappingHandler 4xx/5xx → exception
│   │   ├── Sso/SaasErrorMappingHandler.cs
│   │   └── State/StateCookieManager.cs ← HS256 签 state cookie
│   ├── Security/TenantContext.cs      ← ITenantContext + HttpTenantContext（JWT tenant_id claim）
│   ├── Data/                          ← store 抽象 + InMemory 实现
│   │   ├── IStores.cs                 ← ICatalogStore/IMethodStore/... 6 接口
│   │   ├── InMemoryCatalogStore.cs    ← InMemory fixture：InspectionModel/Spec/Grade/Brand
│   │   ├── InMemoryMethodStore.cs     ← CalculationRule + TechnicalRequirement
│   │   ├── InMemoryFlowStore.cs       ← contracts/receipts/samples/test-records + flow queue
│   │   ├── InMemoryDictionaryStore.cs ← InspectionSpecialty/Object/Parameter/Standard
│   │   ├── InMemoryJunctionStore.cs   ← 5 张 M:N 关联表
│   │   └── CatalogBrandFkHook.cs      ← IHostedService：DELETE brand → SET NULL（V011 FK 语义）
│   ├── Persistence/                   ← EF Core 实现（Lab:Data:Provider=ef 时启用）
│   │   ├── LabDbContext.cs
│   │   ├── EfCatalogStore.cs / EfMethodStore.cs / EfRequirementStore.cs
│   │   ├── EfFlowStore.cs / EfDictionaryStore.cs / EfJunctionStore.cs
│   │   └── EfMethodRequirementStore.cs
│   ├── Services/                      ← 业务层（构造器注入 store + ITenantContext）
│   │   ├── AuthService.cs             ← login/sso/refresh/me/switchTenant/logout
│   │   ├── ContractService.cs
│   │   ├── SampleReceiptService.cs    ← 接样 CRUD + flow 历史
│   │   ├── SampleAndRecordService.cs  ← samples + test-records（含 verdict PATCH）
│   │   ├── ReportFlowService.cs       ← flow/queue + flow 推进（SUBMIT/RETURN/WITHDRAW）
│   │   ├── DictionaryService.cs       ← 检测专项/项目/参数/标准
│   │   ├── CatalogService.cs          ← 型号/规格/等级/牌号
│   │   ├── MethodAndRequirementService.cs  ← 计算方法 + 技术要求
│   │   ├── SummaryService.cs          ← M05 报告汇总 + 仪表盘
│   │   └── JunctionService.cs         ← 5 张 link 表
│   ├── Directory/                     ← 用户目录（B1 配置式 demo）
│   │   ├── IUserDirectory.cs          ← FindByEmail/FindById/FindByUsername/Upsert
│   │   └── ConfigUserDirectory.cs     ← appsettings 配置 + InMemory
│   ├── Serialization/EnumMemberEnumConverter.cs  ← System.Text.Json enum 名策略
│   ├── obj/ bin/                      ← 编译产物（gitignored）
│   ├── global.json                    ← .NET 8 SDK pin
│   └── Lab.AspNetCore.csproj          ← 项目文件 + packages
├── tests/                             ← xUnit + [assembly: CollectionBehavior(DisableTestParallelization)] + fnTest
├── aspnetcore.nswag                   ← NSwag 配置：读 ../shared/openapi.yaml
├── Dockerfile                         ← multi-stage .NET 8 build
├── deploy/                            ← VPS deploy 脚本
└── README.md
```

**5 段差异 vs saas-aspnetcore**：

| 维度 | saas-aspnetcore | lab-aspnetcore |
|---|---|---|
| 持久化 | InMemoryStore（Tenants/Users/Roles/Menus/Apps） | InMemory + EF；5 张 store（contracts/receipts/samples/methods/dictionary/junction） |
| 业务领域 | OAuth IdP（authorize/callback/refresh/menus） | 实验室检测业务（合同 → 接样 → 样品 → 检测 → 报告 → 归档） |
| 业务表 | 平台级（无 tenant 收口） | tenant 收口为主（calculation-rules 平台级除外） |
| SSO 真对接 | 否（自身就是 saas） | 是（[ADR-0008](adr/0008-real-backend-oauth-jwt.md) 调 saas /oauth/token） |
| 数据规模 | 小（IdP 元数据） | 中（业务表 12 张 + link 表 5 张 + flow 历史） |

---

## 3. 核心模块

### 3.1 NSwag codegen 链

**入口**：`scripts/gen-shared.sh`

```bash
# step 1/2 — lab-shared: emit OpenAPI.yaml
(cd "$SHARED_DIR" && npm run emit:openapi)

# step 2/2 — NSwag → src/Controllers/Generated/ + src/Models/Generated/
mkdir -p src/Controllers/Generated src/Models/Generated
(cd "$ROOT" && nswag run "$NSWAG_CONFIG")

# patch — NSwag 已知缺陷确定性修补
python scripts/patch-generated.py
```

**配置**：`aspnetcore.nswag`

| 关键项 | 值 | 说明 |
|---|---|---|
| `fromDocument.url` | `../lab-management-system-shared/generated/openapi/openapi.yaml` | 直接读 shared 仓产物，不复制 |
| `operationGenerationMode` | `MultipleClientsFromOperationId` | 一个 openapi.yaml → 多个 `<Tag>Controller` 类 |
| `controllerStyle` | `Abstract` | 生成 abstract 基类 + 手写 partial 实现 |
| `controllerTarget` | `AspNetCore` | ASP.NET Core MVC（非 minimal API） |
| `generateDtoTypes` | `true` | DTO record 同文件落 `src/Models/Generated/` |
| `typeStyle` | `Record` | C# 9+ record 不可变类型 |
| `jsonLibrary` | `SystemTextJson` | 与 .NET 8 默认一致 |
| `addNullableAnnotations` | `true` | nullable reference types 标注 |

**产物**：

- `src/Controllers/Generated/Controllers.cs` — abstract 类，方法 stub 抛 `NotImplementedException`
- `src/Models/Generated/*.cs` — DTO record（与 shared TypeSpec models 1:1）

**patch-generated.py 修补**：

NSwag 对 OpenAPI 3.x 联合类型 / `oneOf` 解析有已知缺陷，会生成不能编译的 stub。本仓 `patch-generated.py` 做确定性修补：
- `State` 类型（OAuth state 字符串包装）
- `RequirementComparison`（技术要求比较枚举）

修补是确定性 AST 替换，不是手改——下次 `gen-shared.sh` 跑完，patch 重放，结果幂等。

**禁止事项**：

- ❌ 直接编辑 `src/Controllers/Generated/Controllers.cs`（下次 `gen-shared` 重写）
- ❌ 直接编辑 `src/Models/Generated/*.cs`（同上）
- ❌ 用包依赖形式 import shared 产物（必须文件路径读 `../shared/generated/openapi/openapi.yaml`，避免循环依赖）

---

### 3.2 手写 partial Controllers

**目录**：`src/Controllers/Implementation/<Tag>Controller.cs`

**形态**：

```csharp
// AuthController.cs（简化示例）
public partial class AuthController : Lab.AspNetCore.Controllers.Generated.AuthController
{
    public AuthController(AuthService auth) : base() { _auth = auth; }

    public override async Task<LoginResponse> LoginAsync(LoginRequest body)
    {
        TenantGuard.VerifyPathTenant(body.TenantId); // 路径 tenantId vs JWT claim
        return await _auth.LoginAsync(body);
    }
    // ... override 其他 abstract 方法
}
```

**与 generated 基类协作**：

| 层次 | 位置 | 职责 |
|---|---|---|
| Generated 基类 | `src/Controllers/Generated/Controllers.cs` | 路由 (`[HttpPost("/api/auth/login")]`) + 参数绑定 + DTO 序列化 |
| Implementation partial | `src/Controllers/Implementation/<Tag>Controller.cs` | 覆盖 abstract 方法 → 调 Service → 不写业务逻辑 |

**关键约束**：

- ❌ Implementation 不写 `[HttpGet]`/`[Route]`——路由必须 NSwag 生成；
- ❌ Implementation 不写 `[FromBody]`/`[FromRoute]`——基类已绑；
- ❌ Implementation 第一行业务前必调 `TenantGuard.VerifyPathTenant(tenantId)`（若该接口接受路径 tenantId）；
- ✅ Implementation 通过构造器注入 Service（不字段注入、不静态访问）；
- ✅ Service 通过构造器注入 Store（`ICatalogStore` 等抽象接口）；
- ✅ Store 接口实现可换：dev 用 `InMemoryXxxStore`、prod 用 `EfXxxStore`（DI 在 `Program.cs` 切）。

---

### 3.3 InMemoryStore 与 EF 持久化（双轨）

**架构**：

```
Implementation Controller
        │ 构造器注入
        ▼
   XxxService
        │ 构造器注入
        ▼
   IXxxStore (interface in src/Data/IStores.cs)
        │
   ┌────┴───────────────┐
   ▼                    ▼
InMemoryXxxStore    EfXxxStore
（进程内 ConcurrentDictionary）  （Npgsql + EF Core + LabDbContext）
```

**InMemory（dev default）**：

| 文件 | 表/范围 | 形态 |
|---|---|---|
| `InMemoryFlowStore.cs` | contracts / receipts / samples / test-records + flow queue + flow history | `ConcurrentDictionary<string, T>` |
| `InMemoryCatalogStore.cs` | inspection_models / specs / grades / brands | 同上 |
| `InMemoryMethodStore.cs` | calculation_rules | 复合键 `(objectCode, parameterCode)` |
| `InMemoryRequirementStore.cs` | technical_requirements | 三键 `(object, parameter, judgmentStandard)` |
| `InMemoryDictionaryStore.cs` | inspection_specialties / objects / parameters / standards | 按模块分字典 |
| `InMemoryJunctionStore.cs` | 5 张 link 表（specialty↔object / object↔parameter / standard↔parameter / report-name↔*） | `(srcKey, tgtKey)` |

**EF Core（prod，`Lab:Data:Provider=ef`）**：

- `LabDbContext` 镜像 shared SQL 表结构（`contracts`, `receipts`, `samples`, `test_records`, `inspection_models`, `inspection_specialties`, ...）；
- 与 shared SQL 同步靠 `sync-db.mjs`（[lab-nextjs 借链](../lab-management-system-nextjs/scripts/sync-db.mjs)）灌库，不靠 EF Migrations；
- EF Migrations 本仓**不维护**（[ADR-0010 §6](#6-adr-0010-待办) open question）；
- DI 在 `Program.cs` 用 `if (dataProvider == "ef")` 二选一。

**fixture 一致性**：

InMemoryStore 是进程内 fixture，与 lab-msw handlers + `lab-springboot` InMemory fixture **业务形状必须一致**（Contract/Receipt/Sample 等 DTO 同名同字段同语义）。任何字段重命名 = 三端 fixture 断链（lab-shared TypeSpec 是 SSOT）。

---

### 3.4 TenantGuard / TenantContext 安全层

**TenantContext**（`src/Security/TenantContext.cs`）：

```csharp
public interface ITenantContext { string TenantId { get; }
}
public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public string TenantId
    {
        get
        {
            var claim = accessor.HttpContext?.User.FindFirst("tenant_id")?.Value;
            return string.IsNullOrEmpty(claim) ? "TENANT-001" : claim;
        }
    }
}
```

- 从当前请求的 JWT `tenant_id` claim 读；
- 缺失时 dev fallback `TENANT-001`（镜像 `lab-springboot InspectionCatalogController.currentTenantIdOrDefault`）；
- `scoped` 生命周期（每请求一次）。

**TenantGuard**：

`TenantGuard.VerifyPathTenant(pathTenantId)` 由 `TenantContext` + Service 协作完成：当 Implementation Controller 收到带路径 `tenantId` 的请求（如 `GET /api/contracts?tenantId=...`），校验：

1. 路径 tenantId 非空；
2. == JWT claim tenant_id（或 == "TENANT-001" 且 dev 缺省）；
3. 不等 → 抛 `UnauthorizedAccessException` → `Program.cs` 异常映射返回 401。

**所有 tenant-scoped 接口**：

M02.F01（合同）、M03.F01-F09（接样/样品/检测/flow）、M04.F06-F09（码表）、M06.F06（技术要求）都属 tenant 收口。`M06.F05` 计算方法平台级（无 tenant）。

**其他安全层**（[ADR-0008](adr/0008-real-backend-oauth-jwt.md)）：

| 层 | 位置 | 职责 |
|---|---|---|
| JwtBearer | `Program.cs` AddAuthentication | 验签 HS256、验 issuer、验 lifetime（**禁 alg=none**） |
| LabTokenValidationFactory | `src/Auth/Jwt/LabTokenValidationFactory.cs` | 构造 `TokenValidationParameters`，`ValidAlgorithms=[HS256]`，`RequireSignedTokens=true` |
| LabJwtSigner | `src/Auth/Jwt/LabJwtSigner.cs` | HMAC HS256 签发 access + refresh JWT（refresh 内嵌 saas_refresh_token claim） |
| StateCookieManager | `src/Auth/State/StateCookieManager.cs` | HS256 签 OAuth state cookie，HttpOnly + SameSite=Lax |
| SaasErrorMappingHandler | `src/Auth/Sso/SaasErrorMappingHandler.cs` | HttpClient 4xx/5xx → `SaasAuthException`（带 Status） |

---

### 3.5 Program.cs 配置矩阵

**关键配置点**：

| 配置 | 默认 | 说明 |
|---|---|---|
| `Lab:Jwt:Secret` | dev fallback（≥32 字节） | HMAC 密钥；启动校验长度；缺失即抛 `InvalidOperationException` |
| `Lab:Jwt:Issuer` | `lab-management-system` | JWT iss claim |
| `Lab:Jwt:TtlSeconds` | `3600` | access token TTL |
| `Lab:Jwt:RefreshTtlSeconds` | `604800` | refresh token TTL（7d） |
| `Lab:Sso:Profile` | dev=`no-sso` / prod=`dev` 或 `prod` | `no-sso` 走 NoopSaasAuthClient；其他走 HttpSaasAuthClient |
| `Lab:Cors:AllowedOrigins` | `http://localhost:5173,http://localhost:5174,http://localhost:3000` | lab 三前端 dev port |
| `Lab:Data:Provider` | `memory` | `memory` / `ef` 二选一 |
| `Lab:Data:ConnectionString` | ef 时必填 | Npgsql 连接串（env 注入，不硬编码） |
| `Lab:Auth:DevPassword` | `dev123456` | dev demo 密码 |

**DI 注入生命周期**：

| 服务 | 生命周期 | 理由 |
|---|---|---|
| `LabJwtSigner` | Singleton | 无状态 + 复用密钥 |
| `StateCookieManager` | Singleton | 同上 |
| `ISaasAuthClient` / `ISaasMeClient` | Singleton（noop） / Transient（http） | HttpClient factory 管生命周期 |
| `ConfigUserDirectory` (IUserDirectory) | Singleton | InMemory 用户目录 |
| `ITenantContext` (HttpTenantContext) | Scoped | 依赖 IHttpContextAccessor |
| `LabDbContext` | Scoped | EF Core 默认 |
| `EfXxxStore` | Scoped | 持有 DbContext |
| `InMemoryXxxStore` | Singleton | 进程内 ConcurrentDictionary |
| `XxxService` | Scoped（ef） / Singleton（memory） | 视底层 store 生命周期 |

**请求管道顺序**（`Program.cs`）：

```
UseCors("labFrontend")           ← 跨源白名单（3000 + 5173 + 5174）
UseSwagger / UseSwaggerUI        ← dev/staging 在线，prod 待策略 gating
UseAuthentication                ← JwtBearer 解析 JWT
UseAuthorization                 ← [Authorize] + permitAll policy
UseExceptionHandler              ← 异常 → HTTP 状态码映射
MapGet("/health")                ← 匿名探针（deploy 脚本 wget）
MapControllers                   ← 所有 Controller 路由
```

---

### 3.6 双轨持久化切换（`Program.cs` 分支）

**dev 默认**（`Lab:Data:Provider=memory`）：

- 注册 6 个 `InMemoryXxxStore` 为 Singleton；
- 注册 11 个 Service 为 Singleton；
- 注册 `CatalogBrandFkHook` 为 IHostedService（启动时跑：DELETE brand → SET NULL 模拟 V011 FK 语义）。

**prod**（`Lab:Data:Provider=ef`）：

- `NpgsqlDataSourceBuilder(connectionString).EnableDynamicJson().Build()`；
- `AddDbContext<LabDbContext>(options => options.UseNpgsql(dataSource))`；
- 注册 6 个 `EfXxxStore` 为 Scoped（持有 DbContext）；
- 注册 11 个 Service 为 Scoped；
- **不注册** `CatalogBrandFkHook`（FK 由 DB 自己管）。

**切换信号**：`Lab:Data:ConnectionString` 缺失且 `ef` → 启动抛 `InvalidOperationException`（fail-fast）。

---

## 4. 核心流程

### 4.1 启动流程

```
dotnet run
  ↓
WebApplication.CreateBuilder(args)
  ↓
builder.Services.AddControllers + SwaggerGen
  ↓
LabJwtSigner 单例创建（必须在 AddJwtBearer lambda 外）
  ↓
AddAuthentication + AddJwtBearer
  ├─ MapInboundClaims = false（claim 名 "sub"/"tenant_id" 原样）
  └─ TokenValidationParameters = LabTokenValidationFactory.Build(signer)
       ├─ ValidateIssuerSigningKey = true
       ├─ RequireSignedTokens = true（拒 alg=none）
       ├─ ValidAlgorithms = [HS256]
       ├─ ValidateIssuer = true
       └─ ValidateLifetime = true
  ↓
AddAuthorization + permitAll policy
  ↓
AddCors("labFrontend") + allowedOrigins from env
  ↓
StateCookieManager singleton
  ↓
Sso Profile 分支:
  ├─ no-sso → NoopSaasAuthClient / NoopSaasMeClient
  └─ dev/prod → HttpSaasAuthClient + SaasErrorMappingHandler
  ↓
ConfigUserDirectory singleton
  ↓
Data Provider 分支:
  ├─ memory → InMemoryXxxStore + InMemory services + CatalogBrandFkHook
  └─ ef → LabDbContext + EfXxxStore + EF services
  ↓
app.UseCors / UseSwagger / UseAuthentication / UseAuthorization / UseExceptionHandler
  ↓
app.MapGet("/health").AllowAnonymous()
  ↓
app.MapControllers()
  ↓
app.Run()
```

**关键不变量**：

- `LabJwtSigner` **必须在 AddJwtBearer lambda 外**创建+注册（lambda 惰性执行，容器已 build 时 ServiceCollection 只读；详细注释见 `Program.cs` 第 32-35 行）；
- `services.AddSingleton(jwtSigner)` 后 `AuthService` 才从容器解析得到 signer；
- 异常 → HTTP 映射覆盖 `KeyNotFoundException` → 404、`AuthenticationException` → 401、`ArgumentException` → 400、`SaasAuthException` → Status。

---

### 4.2 改契约 → 三端同步（NSwag 重生链）

```
1. [lab-shared] 改 tsp/main.tsp 或 sql/migrations/V00N+1__*.sql
   ↓ git commit + push

2. [lab-shared] npm run build           ← emit:openapi + tsc --noEmit
   gate: python scripts/gate.py -p lab-management-system-shared
   ↓ exit 0

3. [lab-aspnetcore] bash scripts/gen-shared.sh
   固定三步：
   a) (cd ../shared && npm run emit:openapi)
   b) nswag run aspnetcore.nswag → src/Controllers/Generated/Controllers.cs
   c) patch-generated.py → 修补 State / RequirementComparison
   ↓

4. [Implementation] 手写/覆盖 abstract 方法
   ├─ 新 Tag？→ 新建 src/Controllers/Implementation/<Tag>Controller.cs
   ├─ 改 method 签名？→ 在 Implementation 同步改 override
   └─ 改 TenantGuard 行为？→ 在 Service 层调 TenantGuard.VerifyPathTenant

5. [function-tree.md] 改 I 子项（如新增端点）
   ├─ 必须先改树再改代码（ADR-0003）
   └─ 改功能与改代码必须同一 commit

6. [tests] xUnit fnTest 嵌入 fn-ID → trace_cmd 产 .state/trace.json
   ↓

7. gate: python scripts/gate.py -p lab-management-system-aspnetcore
   ├─ L1 dotnet format
   ├─ L3 dotnet build
   └─ L4 dotnet test + trace_cmd
   ↓ exit 0

8. [父仓] git update-index --add --cacheinfo 160000,<NEW_HASH>,output/lab-management-system-aspnetcore
```

**关键检查点**：

- 改契约时必须**先**改 shared BASE tree 的 F 级（[ADR-0003](../docs/adr/0003-function-tree-requires-human-approval.md)），再改本仓 I 级子项；否则 L5 红；
- `gen-shared.sh` 不会 `cp` SQL 文件——本仓 EF 不 Migrate，schema 真源永远是 shared SQL；
- `patch-generated.py` 是确定性 AST 修补，幂等可重放；
- `dotnet test` 测试**禁止并行**：`[assembly: CollectionBehavior(DisableTestParallelization)]`——InMemoryStore 是 Singleton fixture，并发修改抛 `Collection was modified`；
- skip/xfail 的测试**禁止挂 fn-ID**（CLAUDE.md 硬约束 + 父仓 ADR-0002）。

---

### 4.3 一个请求的生命周期（tenant-scoped endpoint）

```
GET /api/contracts/abc-123
Authorization: Bearer <JWT (HS256, alg=HS256)>
  ↓
UseCors 验 origin ∈ {5173, 5174, 3000}
  ↓
UseAuthentication (JwtBearer)
  ├─ LabTokenValidationFactory 验签 HS256（拒 alg=none）
  ├─ 验 issuer == "lab-management-system"
  ├─ 验 exp > now
  └─ User.Claims = { sub, tenant_id, exp, saas_refresh_token?, ... }
  ↓
UseAuthorization 验 [Authorize]（非 permitAll）
  ↓
MapGet → Generated 路由绑定 {id="abc-123"} 到 ContractController.GetAsync
  ↓
Implementation ContractController.GetAsync override
  ├─ var tenantId = new HttpTenantContext(accessor).TenantId
  ├─ TenantGuard.VerifyPathTenant(...)（若该接口接收路径 tenantId）
  └─ return _contractService.GetAsync(tenantId, id)
  ↓
ContractService.GetAsync
  ├─ store = InMemoryFlowStore or EfFlowStore（按 provider）
  ├─ var contract = store.GetContract(tenantId, id)
  ├─ null → throw KeyNotFoundException
  └─ return contract.ToDto()
  ↓
ExceptionHandler: null → 200, KeyNotFoundException → 404
  ↓
JSON 响应 { id, contractCode, projectName, status, ... }
```

---

## 5. 与契约仓同步

**入口**：`scripts/gen-shared.sh`

```bash
SHARED_DIR="$(cd "$(dirname "$0")/../../lab-management-system-shared" && pwd)"
OPENAPI="$SHARED_DIR/generated/openapi/openapi.yaml"
NSWAG_CONFIG="$ROOT/aspnetcore.nswag"

# step 1 — shared emit
(cd "$SHARED_DIR" && npm run emit:openapi)

# step 2 — NSwag run
mkdir -p src/Controllers/Generated src/Models/Generated
(cd "$ROOT" && nswag run "$NSWAG_CONFIG")

# patch NSwag 已知缺陷
python "$ROOT/scripts/patch-generated.py"
```

**与 springboot 仓的同步差异**：

| 维度 | lab-springboot | lab-aspnetcore |
|---|---|---|
| Codegen 工具 | openapi-generator-maven-plugin（Java） | NSwag CLI（C#） |
| 产物位置 | `src/main/java/.../controller/` | `src/Controllers/Generated/Controllers.cs` |
| DB 同步 | Flyway（`db/migration/V*.sql` cp 自 shared） | EF Core 仅 ORM 镜像，不 Migrate；库由 sync-db 灌 |
| SQL 拷贝 | `cp shared/sql/migrations/V*.sql db/migration/` 含 cmp abort 防护 | ❌ 不拷贝（本仓无 db/migration/） |
| DIVERGED_VERSIONS | V014/V017 永久分叉白名单 | 不适用（无 SQL 拷贝） |

**契约同步失败时**：

- `OPENAPI` 不存在 → `gen-shared.sh` exit 1（明确 ERROR 信息）；
- NSwag 跑挂 → 抛异常（非零退出）；
- patch-generated.py 修补失败 → 抛异常（NSwag 升级可能改变 AST，需人工修脚本）。

---

## 6. ADR-0010 待办

**ADR-0010**（父仓 [00010-aspnetcore-ef-mirrors-sql.md](../docs/adr/0010-aspnetcore-ef-mirrors-sql.md)）：EF Core Migrations 应镜像 shared SQL DDL。

**当前状态**：

- 本仓 `Migrations/` 目录**不存在**——EF 只在 ORM 层（`LabDbContext`）声明 entity，schema 真源仍是 `../lab-management-system-shared/sql/migrations/V*.sql`；
- prod DB schema 由 `lab-nextjs/scripts/sync-db.mjs` 从 shared SQL 灌入；
- EF 启动时 **不** Migrate，仅 DbContext model 校验。

**open question**（详见父仓 docs/adr/0010）：

- 是否补 `Migrations/InitialSchema.cs`（EF 镜像 shared SQL DDL）？
- 还是修订 ADR-0010 改口"EF 不维护 migration，schema 唯一真源是 shared SQL"？
- 与 springboot 仓 Flyway baseline-v13 冻结策略的关系？

**当前决定**：维持现状（不补 EF Migrations），与 springboot baseline-v13 冻结同哲学——schema 由 shared SQL 单一管理。

---

## 7. 决策索引

本仓与父仓 ADR 的引用：

### 7.1 本仓特有 ADR

| ADR | 主题 | 一句话 |
|---|---|---|
| [0008](adr/0008-real-backend-oauth-jwt.md) | 真后端 OAuth 2.0 + JWT 签发 | HMAC HS256 取代 alg=none；state cookie + HS256；refresh 嵌 saas_refresh_token |

### 7.2 父仓 ADR（影响本仓）

| ADR | 主题 | 在本仓的落地点 |
|---|---|---|
| [0007](../docs/adr/0007-shared-sql-ssot.md) | shared 双 SSOT | schema 真源 = shared SQL；本仓不维护 SQL |
| [0010](../docs/adr/0010-aspnetcore-ef-mirrors-sql.md) | aspnetcore EF 应镜像 SQL | 待办：本仓暂不补 EF Migrations（§6） |
| [0014](../docs/conventions/multi-repo-family.md#4-后端配置env-driven-单-urladr-0014) | env-driven 单 URL | 本仓配置全部走 env（Lab:Jwt:Secret 等） |
| [0003](../docs/adr/0003-function-tree-requires-human-approval.md) | 功能清单变更需人批 | 改 F/I 走 `/tree-change` |

### 7.3 隐含约束（来自 CLAUDE.md）

| 编号 | 主题 | 一句话 |
|---|---|---|
| CLAUDE.md §2.1 | 禁 Controller/Program.cs 写业务 | Implementation 只覆盖 abstract 方法，不写业务 |
| CLAUDE.md §2.2 | 禁 catch 吞异常 | catch 后必须 log + rethrow |
| CLAUDE.md §2.3 | 禁硬编码连接串/密钥 | 走 appsettings.Development.json 或 env |
| CLAUDE.md §2.4 | 禁直接改 function-tree.md | 改功能走 `/tree-change` |

---

## 8. 术语表

| 术语 | 含义 | 详细 |
|---|---|---|
| **partial Controller** | C# `partial class` 跨文件合并 | Generated 基类 + Implementation 子类合成完整类 |
| **NSwag** | OpenAPI → C# controller codegen | `aspnetcore.nswag` 配置驱动 |
| **abstract Controller** | NSwag `controllerStyle=Abstract` | 路由在基类，方法 stub 抛 `NotImplementedException`，partial 实现覆盖 |
| **InMemory fixture** | 进程内 fixture | 与 lab-msw / lab-springboot InMemory 业务形状一致 |
| **EF Core** | ORM 框架 | 本仓仅 ORM 镜像，**不 Migrate**（schema 仍 shared SQL） |
| **TenantGuard** | 路径 tenantId vs JWT claim 校验 | tenant-scoped 接口第一行调 |
| **TenantContext (ITenantContext)** | 从 JWT claim 解 tenant_id | scoped，dev fallback `TENANT-001` |
| **LabJwtSigner** | HMAC HS256 JWT 签发器 | 拒 alg=none（v0.1.17 起） |
| **LabTokenValidationFactory** | 构造 TokenValidationParameters | `ValidAlgorithms=[HS256]`, `RequireSignedTokens=true` |
| **StateCookieManager** | HS256 签 OAuth state cookie | HttpOnly + SameSite=Lax + 5min Max-Age |
| **SaasAuthException** | 4xx/5xx → 强类型异常 | 带 Status 字段，Program.cs 异常映射用 |
| **SaasErrorMappingHandler** | HttpClient DelegatingHandler | 4xx/5xx → SaasAuthException |
| **no-sso profile** | dev demo 模式 | NoopSaasAuthClient 固定返回 admin session |
| **real backend OAuth** | 真对接 saas /oauth/{authorize,token} | ADR-0008 取代 B1 alg=none + mock SSO |
| **SSOT** | Single Source of Truth | lab-shared 担 API + DB 双 SSOT |
| **BASE tree** | 契约仓的功能清单 | 只到 F 级；本仓镜像后加 I |
| **gitlink** | 父仓对子仓 commit hash 引用 | mode 160000；详见 [docs/conventions/submodule.md](../docs/conventions/submodule.md) |
| **trace.json** | 测试命中 fn-ID 清单 | `trace_cmd` 产，禁止手写 |
| **fnTest** | 测试 ID 嵌入 it 名 | `fnTest(["M01.F05.I01"], "...", () => {...})` |
| **stack.json** | 项目自描述 | suite 门禁读它；项目只能声明 L1-L4 |
| **multi-repo-family** | 多仓家族拓扑 | shared + msw + N 前端 + M 后端 + 父仓 |

---

## 附录 A：与父仓 docs/ARCHITECTURE.md 的关系

本文档是**子仓视角**——只描述 lab-management-system-aspnetcore 一个仓的内部结构。

| 你想知道… | 看哪里 |
|---|---|
| lab 家族 + 14 仓拓扑 | [父仓 docs/ARCHITECTURE.md §1-§2](../docs/ARCHITECTURE.md) |
| 双 SSOT + 一份契约三套 codegen | [父仓 docs/ARCHITECTURE.md §3](../docs/ARCHITECTURE.md) |
| 改契约三端同步全流程 | [父仓 docs/ARCHITECTURE.md §5.1](../docs/ARCHITECTURE.md#51-改一次契约--三端同步codegen-链) |
| 端口 + CORS + env 全景 | [父仓 docs/ARCHITECTURE.md §6](../docs/ARCHITECTURE.md) |
| 12 份 ADR 总索引 | [父仓 docs/ARCHITECTURE.md §7](../docs/ARCHITECTURE.md) |
| 14 仓 CLAUDE.md 一览 | [父仓 docs/ARCHITECTURE.md §9](../docs/ARCHITECTURE.md) |
| 典型陷阱 | [父仓 docs/ARCHITECTURE.md 附录 B](../docs/ARCHITECTURE.md) + `~/.claude/.../memory/MEMORY.md` |

**本仓独有**，父仓文档只提及：

- NSwag `controllerStyle=Abstract` + partial Controller 双层结构（§3.2）
- patch-generated.py 确定性修补（§3.1）
- InMemory + EF 双轨持久化切换（§3.3 / §3.6）
- LabJwtSigner / LabTokenValidationFactory 真签名链（§3.4）
- no-sso profile dev 降级（§3.5）
- ADR-0008 真 OAuth + state cookie（§3.4 / 附录 A）

---

## 附录 B：与 saas-identity-platform-aspnetcore 后端仓的对照

| 维度 | saas-aspnetcore | lab-aspnetcore |
|---|---|---|
| 角色 | OAuth IdP 自身 | 业务后端（合同→接样→报告） |
| 业务表 | tenants/users/apps/menus/roles | contracts/receipts/samples/methods/dictionary/junction |
| tenant 收口 | 平台级无 tenant | tenant 收口为主（calculation-rules 除外） |
| 数据规模 | 小（IdP 元数据） | 中（12 业务表 + 5 link 表） |
| Auth | IdP 自身（签发 token） | 调 saas /oauth/token（[ADR-0008](adr/0008-real-backend-oauth-jwt.md)） |
| SSO | 自身就是 SSO | redirect to saas authorize + state cookie |
| 同构点 | ✅ NSwag + partial Controller + InMemory fixture + xUnit + CollectionBehavior(DisableTestParallelization) | 同上 |
| 共同约束 | ❌ 不手写 Controller 路由 / 不跳过 TenantGuard / 测试不并行 | 同上 |
| 不同点 | IdP 元数据直接 InMemory | 业务可切 EF Core（prod） |

**迁移 / 借鉴**：

- saas-aspnetcore 已先落地 NSwag + partial Controller 模式 → lab-aspnetcore 镜像；
- lab-aspnetcore 的真 OAuth（ADR-0008）反过来给 saas-aspnetcore 提供 token format 参考；
- 两仓 `gen-shared.sh` 脚本结构同构（emit + codegen + patch），只是 codegen 工具不同（NSwag vs NSwag，两仓都是 NSwag）。

---

## 附录 C：典型陷阱（详见 [父仓 memory](../docs/conventions/) + `~/.claude/.../memory/MEMORY.md`）

| 陷阱 | 后果 | 解法 |
|---|---|---|
| `dotnet run` 停不干净 | 残留子进程占端口 5000 | 按端口反查杀子进程 exe（别按 dotnet 进程名） |
| 改 `Generated/Controllers.cs` 后被覆盖 | 下次 gen-shared 重写丢失手改 | 实现放 Implementation partial，Generated gitignored |
| 测试并行跑 | `Collection was modified`（InMemory Singleton fixture 并发写） | `[assembly: CollectionBehavior(DisableTestParallelization)]` |
| 跳过 TenantGuard | 跨租户访问 | tenant-scoped endpoint 第一行必调 `TenantGuard.VerifyPathTenant` |
| Lab:Jwt:Secret 缺失 | dev fallback 密钥泄漏 | 启动校验长度 ≥32 字节；prod env 注入；env-file 别写（deploy 烘焙） |
| EF 不 Migrate 启动失败 | DbContext model 与 DB schema 不一致 | 用 sync-db 灌 shared SQL；不补 EF Migration（§6 open question） |
| patch-generated.py AST 改不动 | NSwag 升级改变输出格式 | patch 是确定性文本替换；若失败人工修脚本后重放 |
| `appsettings.json` 写连接串 | 密钥泄漏入 git | 走 appsettings.Development.json 或 env；CI lint 拦硬编码 |