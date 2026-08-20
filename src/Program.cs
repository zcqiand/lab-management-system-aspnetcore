using System.Security.Authentication;
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

// B1 认证域底座（镜像 lab-springboot SecurityConfig）：
//   permitAll = login / refresh / sso/**，其余 authenticated。
builder.Services.AddControllers();
builder.Services.Configure<LabOptions>(builder.Configuration.GetSection("Lab"));
// signer 必须在 AddJwtBearer lambda 外创建+注册：该 lambda 惰性执行
//（首个认证请求才跑 OptionsFactory），那时容器已 build、ServiceCollection 只读，
// 在 lambda 里 AddSingleton 会抛 "collection cannot be modified because it is read-only"
// 且 AuthService 也从容器解析 LabJwtSigner（不能只做局部变量）。
var jwtSigner = new LabJwtSigner(
    builder.Configuration["Lab:Jwt:Secret"] ?? "dev-lab-jwt-secret-dev-lab-jwt-secret-dev-lab-jwt-secret",
    builder.Configuration["Lab:Jwt:Issuer"] ?? "lab-management-system",
    int.TryParse(builder.Configuration["Lab:Jwt:TtlSeconds"], out var t) ? t : 3600,
    int.TryParse(builder.Configuration["Lab:Jwt:RefreshTtlSeconds"], out var rt) ? rt : 604800);
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

// CORS：lab 前端三仓（5173 react / 5174 vue / 3000 nextjs），env LAB_CORS_ALLOWED_ORIGINS 覆盖
var allowedOrigins = (builder.Configuration["Lab:Cors:AllowedOrigins"]
    ?? "http://localhost:5173,http://localhost:5174,http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddPolicy("labFrontend", p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyMethod()
    .AllowAnyHeader()
    // SSO state cookie 跨源往返必需：前端 axios withCredentials=true 时，
    // CORS 响应必须带 Access-Control-Allow-Credentials 才生效
    .AllowCredentials()));

// State cookie manager（HS256 签 state,签名密钥复用 Lab:Jwt:Secret）
builder.Services.AddSingleton(sp => new StateCookieManager(
    sp.GetRequiredService<IConfiguration>()["Lab:Jwt:Secret"] ?? "dev-lab-jwt-secret-dev-lab-jwt-secret-dev-lab-jwt-secret"));

// SSO 客户端（ADR-0008：profile 切换 noop vs real）
var ssoProfile = builder.Configuration["Lab:Sso:Profile"] ?? "no-sso";
if (ssoProfile == "no-sso")
{
    builder.Services.AddSingleton<ISaasAuthClient, NoopSaasAuthClient>();
    builder.Services.AddSingleton<ISaasMeClient, NoopSaasMeClient>();
}
else
{
    builder.Services.AddTransient<SaasErrorMappingHandler>();
    builder.Services.AddHttpClient<ISaasAuthClient, HttpSaasAuthClient>()
        .AddHttpMessageHandler<SaasErrorMappingHandler>();
    builder.Services.AddHttpClient<ISaasMeClient, HttpSaasMeClient>()
        .AddHttpMessageHandler<SaasErrorMappingHandler>();
}

// 用户目录（B1 配置式 demo，1:1 镜像 lab-msw / lab-springboot ConfigUserDirectory）
builder.Services.AddSingleton<IUserDirectory>(sp =>
    new ConfigUserDirectory(
        sp.GetRequiredService<IConfiguration>()["Lab:Auth:DevPassword"] ?? "dev123456"));
builder.Services.AddSingleton<AuthService>(sp =>
    new AuthService(
        sp.GetRequiredService<IUserDirectory>(),
        sp.GetRequiredService<LabJwtSigner>(),
        sp.GetRequiredService<ISaasAuthClient>(),
        sp.GetRequiredService<ISaasMeClient>(),
        sp.GetRequiredService<StateCookieManager>(),
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LabOptions>>()));

// B2：码表/计算规则/技术要求。
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
var dataProvider = builder.Configuration["Lab:Data:Provider"] ?? "memory";
if (dataProvider == "ef")
{
    var connectionString = builder.Configuration["Lab:Data:ConnectionString"]
        ?? throw new InvalidOperationException("Lab:Data:Provider=ef 需要 Lab:Data:ConnectionString");
    var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).EnableDynamicJson().Build();
    builder.Services.AddDbContext<LabDbContext>(options => options.UseNpgsql(dataSource));
    builder.Services.AddScoped<EfCatalogStore>();
    builder.Services.AddScoped<EfRuleStore>();
    builder.Services.AddScoped<EfRequirementStore>();
    builder.Services.AddScoped<EfFlowStore>();
    builder.Services.AddScoped<EfDictionaryStore>();
    builder.Services.AddScoped<EfJunctionStore>();
    builder.Services.AddScoped<ICatalogStore>(sp => sp.GetRequiredService<EfCatalogStore>());
    builder.Services.AddScoped<IRuleStore>(sp => sp.GetRequiredService<EfRuleStore>());
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
    builder.Services.AddScoped<CalculationRuleService>();
    builder.Services.AddScoped<TechnicalRequirementService>();
}
else
{
    builder.Services.AddSingleton<InMemoryCatalogStore>();
    builder.Services.AddSingleton<InMemoryRuleStore>();
    builder.Services.AddSingleton<InMemoryRequirementStore>();
    builder.Services.AddSingleton<InMemoryFlowStore>();
    builder.Services.AddSingleton<InMemoryDictionaryStore>();
    builder.Services.AddSingleton<InMemoryJunctionStore>();
    builder.Services.AddSingleton<ICatalogStore>(sp => sp.GetRequiredService<InMemoryCatalogStore>());
    builder.Services.AddSingleton<IRuleStore>(sp => sp.GetRequiredService<InMemoryRuleStore>());
    builder.Services.AddSingleton<IRequirementStore>(sp => sp.GetRequiredService<InMemoryRequirementStore>());
    builder.Services.AddSingleton<IFlowStore>(sp => sp.GetRequiredService<InMemoryFlowStore>());
    builder.Services.AddSingleton<IDictionaryStore>(sp => sp.GetRequiredService<InMemoryDictionaryStore>());
    builder.Services.AddSingleton<IJunctionStore>(sp => sp.GetRequiredService<InMemoryJunctionStore>());
    builder.Services.AddSingleton<SummaryService>();
    builder.Services.AddSingleton<ContractService>();
    builder.Services.AddSingleton<SampleReceiptService>();
    builder.Services.AddSingleton<SampleService>();
    builder.Services.AddSingleton<TestRecordService>();
    builder.Services.AddSingleton<ReportFlowService>();
    builder.Services.AddSingleton<DictionaryService>();
    builder.Services.AddSingleton<JunctionService>();
    builder.Services.AddSingleton<CatalogService>();
    builder.Services.AddSingleton<CalculationRuleService>();
    builder.Services.AddSingleton<TechnicalRequirementService>();
    builder.Services.AddHostedService<CatalogBrandFkHook>();
}

var app = builder.Build();

app.UseCors("labFrontend");
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
            AuthenticationException or UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            ArgumentException => StatusCodes.Status400BadRequest,
            SaasAuthException s => s.Status,
            _ => StatusCodes.Status500InternalServerError,
        };
        await context.Response.WriteAsJsonAsync(new { error = ex?.Message ?? "internal error" });
    }));

app.MapControllers();

app.Run();

public partial class Program { }
