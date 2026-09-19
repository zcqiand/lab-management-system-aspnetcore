using System.Security.Authentication;
using Lab.AspNetCore.Auth.Config;
using Lab.AspNetCore.Auth.Jwt;
using Lab.AspNetCore.Auth.Sso;
using Lab.AspNetCore.Auth.State;
using Lab.AspNetCore.Data;
using Lab.AspNetCore.Directory;
using Lab.AspNetCore.Security;
using Lab.AspNetCore.Persistence;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// conventions §6: 家族统一监听 key SERVER_PORT（aspnetcore=5204，2026-09-02 端口分段）。ASPNETCORE_URLS
// 优先级更高（容器内 Dockerfile ENV 已设）, 本 shim 只服务裸机 dotnet run。
var shimUrls = Lab.AspNetCore.Hosting.ServerPortShim.ResolveUrls(builder.Configuration);
if (shimUrls is not null)
{
    builder.WebHost.UseUrls(shimUrls);
}

// B1 认证域底座（镜像 lab-springboot SecurityConfig）：
//   permitAll = login / refresh / sso/**，其余 authenticated。
// NRT 隐式 Required 推断关闭（REQ-2026-001 live 实证）：<Nullable>enable</Nullable> 下
// 生成 DTO 无 ? 注解的可空引用属性（Config/Description/ReportNameCode…）被 MVC 隐式标
// Required → 契约可选字段缺省即 400。契约必填字段的校验走 NSwag 产出的显式
// [Required] 属性，不受此开关影响（与 springboot @Valid + msw/nextjs 宽松绑定对齐）。
builder.Services.AddControllers(o =>
    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
// LabOptions:Lab json 段绑定(appsettings dev 值)+ flat env 直读入口
// (2026-08-28 key 统一:SSO 属性 getter 优先读 flat LAB_SAAS_*/LAB_SSO_*,
// 与 lab-springboot yml 占位符同名;json 段作 dev fallback)
builder.Services.Configure<LabOptions>(builder.Configuration.GetSection("Lab"));
builder.Services.PostConfigure<LabOptions>(o =>
{
    o.Config = builder.Configuration;
    o.Sso.Config = builder.Configuration;
});

// Swagger / OpenAPI UI：与 saas-identity-platform-aspnetcore v0.1.5 同位,
// 服务于前端 orval 复核契约 + QA curl 试验端点（v0.1.8 接入）。
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "lab-management-system-aspnetcore",
        Version = "v1",
        Description = "ASP.NET Core 8 后端。NSwag 读 ../lab-management-system-shared/generated/openapi/openapi.yaml 产 AllGenerated.cs → 按类拆分为 src/Controllers/Generated/<Tag>Controller.cs + src/Models/Generated/<Dto>.cs（spec §2.2，14 controllers + 151 models）；concrete 实现见 src/Controllers/Implementation/。",
    });
});
// signer 必须在 AddJwtBearer lambda 外创建+注册：该 lambda 惰性执行
//（首个认证请求才跑 OptionsFactory），那时容器已 build、ServiceCollection 只读，
// 在 lambda 里 AddSingleton 会抛 "collection cannot be modified because it is read-only"
// 且 AuthService 也从容器解析 LabJwtSigner（不能只做局部变量）。
//
// ADR-0019：所有 JWT 配置从 env 必填注入（ConfigBuilder.RequireXxx 集中校验）。
// TTL/RefreshTTL 是数值(无 demo 字面陷阱),保留 int.TryParse 但缺省值改为 0 → 必填。
var jwtSigningKey = ConfigBuilder.RequireJwtSigningKey(builder.Configuration);
var jwtIssuer = ConfigBuilder.RequireJwtIssuer(builder.Configuration);
var jwtTtlSeconds = int.TryParse(builder.Configuration["JWT_TTL_SECONDS"], out var t) ? t
    : throw new InvalidOperationException("JWT_TTL_SECONDS env is required (ADR-0019 禁字面默认值)");
var jwtRefreshTtlSeconds = int.TryParse(builder.Configuration["JWT_REFRESH_TTL_SECONDS"], out var rt) ? rt
    : throw new InvalidOperationException("JWT_REFRESH_TTL_SECONDS env is required (ADR-0019 禁字面默认值)");
var jwtSigner = new LabJwtSigner(jwtSigningKey, jwtIssuer, jwtTtlSeconds, jwtRefreshTtlSeconds);
builder.Services.AddSingleton(jwtSigner);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // claim 名保持 "sub"/"tenant_id" 原样（对齐 spring 侧）
        options.TokenValidationParameters = LabTokenValidationFactory.Build(jwtSigner);
    });
builder.Services.AddAuthorization(o =>
{
    // permitAll 三端点：login / refresh / sso/**（镜像 spring SecurityConfig permitAll 列表）
    o.AddPolicy("permitAll", p => p.RequireAssertion(_ => true));
});

// CORS：lab 前端三仓（5202 react / 5203 vue / 5201 nextjs），env LAB_CORS_ALLOWED_ORIGINS 覆盖
// ADR-0019：缺失 throw,不允许 fallback 到 localhost dev 列表。
var allowedOrigins = ConfigBuilder.RequireCorsOrigins(builder.Configuration);
builder.Services.AddCors(o => o.AddPolicy("labFrontend", p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyMethod()
    .AllowAnyHeader()
    // SSO state cookie 跨源往返必需：前端 axios withCredentials=true 时，
    // CORS 响应必须带 Access-Control-Allow-Credentials 才生效
    .AllowCredentials()));

// State cookie manager（HS256 签 state,签名密钥复用 JWT_SIGNING_KEY）
builder.Services.AddSingleton(sp => new StateCookieManager(jwtSigningKey));

// SSO 客户端：恒 real（2026-09-20 人裁全家族删 no-sso profile，ADR-0008 §6 废止）。
// 离线模拟由测试项目的 Noop 替身承担，src 不再携带 noop 实现。
builder.Services.AddTransient<SaasErrorMappingHandler>();
builder.Services.AddHttpClient<ISaasAuthClient, HttpSaasAuthClient>()
    .AddHttpMessageHandler<SaasErrorMappingHandler>();
builder.Services.AddHttpClient<ISaasMeClient, HttpSaasMeClient>()
    .AddHttpMessageHandler<SaasErrorMappingHandler>();

// 用户目录（B1 配置式 demo，1:1 镜像 lab-msw / lab-springboot ConfigUserDirectory）
// ADR-0019：DevPassword 缺失 throw,不允许 "dev123456" 兜底。dev 期 appsettings.Development.json
// 必须显式声明 (即便 dev 也要求显式 dev password,与 prod 同 key 路径对齐)。
builder.Services.AddSingleton<IUserDirectory>(sp =>
    new ConfigUserDirectory(
        ConfigBuilder.RequireDevPassword(sp.GetRequiredService<IConfiguration>())));
builder.Services.AddSingleton<AuthService>(sp =>
    new AuthService(
        sp.GetRequiredService<IUserDirectory>(),
        sp.GetRequiredService<LabJwtSigner>(),
        sp.GetRequiredService<ISaasAuthClient>(),
        sp.GetRequiredService<ISaasMeClient>(),
        sp.GetRequiredService<StateCookieManager>(),
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LabOptions>>()));

// B2：码表/计算方法/技术要求。
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
// 恒 ef（2026-09-20 人裁删 memory provider 分支；LAB_DATA_PROVIDER key 全家族同批删除）。
// ADR-0019：DATABASE_URL 缺失 fail-fast，不允许任何字面/路径兜底。
var connectionString = builder.Configuration["DATABASE_URL"]
    ?? throw new InvalidOperationException("DATABASE_URL env is required (恒 ef 路径, ADR-0019 禁字面默认值)");
var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).EnableDynamicJson().Build();
// EF Core 8 AddDbContext<TContext> 给的是 non-generic DbContextOptionsBuilder。
// UseSnakeCaseNamingConvention() 必须挂上:PascalCase 列 vs snake_case DB = 42703
// (2026-09-04 prod incident)。TestDb 用 LabDbContextConfig.UseLabNpgsql(string),
// prod 走 NpgsqlDataSource(EnableDynamicJson for jsonb) 不能复用 string helper,
// 这里手挂 convention。L4 测试 LabDbContextConfigTest 锁住列名映射。
builder.Services.AddDbContext<LabDbContext>(
    options => options.UseNpgsql(dataSource).UseSnakeCaseNamingConvention());
builder.Services.AddScoped<EfCatalogStore>();
builder.Services.AddScoped<EfMethodStore>();
builder.Services.AddScoped<EfRequirementStore>();
builder.Services.AddScoped<EfFlowStore>();
builder.Services.AddScoped<EfDictionaryStore>();
builder.Services.AddScoped<EfJunctionStore>();
builder.Services.AddScoped<ICatalogStore>(sp => sp.GetRequiredService<EfCatalogStore>());
builder.Services.AddScoped<IMethodStore>(sp => sp.GetRequiredService<EfMethodStore>());
builder.Services.AddScoped<IRequirementStore>(sp => sp.GetRequiredService<EfRequirementStore>());
builder.Services.AddScoped<IFlowStore>(sp => sp.GetRequiredService<EfFlowStore>());
builder.Services.AddScoped<IDictionaryStore>(sp => sp.GetRequiredService<EfDictionaryStore>());
builder.Services.AddScoped<IJunctionStore>(sp => sp.GetRequiredService<EfJunctionStore>());
builder.Services.AddScoped<SummaryService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<SampleReceiptService>();
builder.Services.AddScoped<SampleService>();
builder.Services.AddScoped<TestRecordService>();
builder.Services.AddScoped<ReportFlowService>();
builder.Services.AddScoped<DictionaryService>();
builder.Services.AddScoped<JunctionService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<CalculationMethodService>();
builder.Services.AddScoped<TechnicalRequirementService>();

var app = builder.Build();

app.UseCors("labFrontend");

// Swagger UI（与 saas-aspnetcore v0.1.5 同位：dev/staging 在线暴露, prod 暂全开 —
// 与 springboot springdoc-openapi 一致;生产 gating 走 ASPNETCORE_ENVIRONMENT
// 之外的策略独立 PR）。
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "lab-management-system-aspnetcore v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

// 异常 → HTTP 映射（镜像 springboot GlobalExceptionHandler + SaasAuthException 子类）
app.UseExceptionHandler(errorApp =>
    errorApp.Run(async context =>
    {
        var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.Response.StatusCode = ex switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            Lab.AspNetCore.Services.MenusUnavailableException => StatusCodes.Status503ServiceUnavailable,
            AuthenticationException or UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            ArgumentException => StatusCodes.Status400BadRequest,
            SaasAuthException s => s.Status,
            _ => StatusCodes.Status500InternalServerError,
        };
        await context.Response.WriteAsJsonAsync(new { error = ex?.Message ?? "internal error" });
    }));

// 匿名 /health 探针（deploy 脚本 wget 探 200；与 saas-aspnetcore MapGet("/health") 同模式）
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapControllers();

app.Run();

public partial class Program { }
