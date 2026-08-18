using System.Security.Authentication;
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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // claim 名保持 "sub"/"tenant_id" 原样（对齐 spring 侧）。live smoke 发现：true 会把 sub 映射成 SOAP 长名，AuthService claims["sub"] 取不到
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = false,
            RequireSignedTokens = false, // dev：容忍 alg=none 模拟 token；production 换 true + issuer-uri
            // dev：alg=none token 无签名可验，默认 SignatureValidator 会拒收（live smoke 发现）--
            // 信任 payload 交给上面的 claim 校验（exp/有效期仍在验）。production 必须删掉这一行。
            SignatureValidator = (token, _) => new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token),
        };
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
    .AllowAnyHeader()));

// 用户目录（B1 配置式 demo，1:1 镜像 lab-msw / lab-springboot ConfigUserDirectory）
builder.Services.AddSingleton<IUserDirectory>(sp =>
    new ConfigUserDirectory(
        sp.GetRequiredService<IConfiguration>()["Lab:Auth:DevPassword"] ?? "dev123456"));
builder.Services.AddSingleton<AuthService>(sp =>
    new AuthService(
        sp.GetRequiredService<IUserDirectory>(),
        sp.GetRequiredService<IConfiguration>()["Lab:Sso:SaasBase"] ?? "http://localhost:3000"));

// B2：码表/计算规则/技术要求。双实现可换装（Lab:Data:Provider = memory | ef）：
//   memory -- InMemory*Store 单例（默认；测试与无 DB dev，语义快照）
//   ef     -- Ef*Store + LabDbContext（lab_dev 共库；EF 只镜像 shared SQL 不 Migrate，
//             真实 FK/唯一约束生效，与 springboot JPA 同库同语义）
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
var dataProvider = builder.Configuration["Lab:Data:Provider"] ?? "memory";
if (dataProvider == "ef")
{
    var connectionString = builder.Configuration["Lab:Data:ConnectionString"]
        ?? throw new InvalidOperationException("Lab:Data:Provider=ef 需要 Lab:Data:ConnectionString（lab_dev 共库，镜像 springboot LAB_DB_* env）");
    // IDictionary<string,object> jsonb（config 列）需要 dynamic JSON
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
    // 牌号删除 SET NULL 由 DB 的 ON DELETE SET NULL 承担（V011），不需要内存事件钩子
}
else
{
    builder.Services.AddSingleton<InMemoryCatalogStore>();
    builder.Services.AddSingleton<InMemoryRuleStore>();
    builder.Services.AddSingleton<InMemoryRequirementStore>();
    builder.Services.AddSingleton<InMemoryFlowStore>();
    builder.Services.AddSingleton<InMemoryDictionaryStore>();
    builder.Services.AddSingleton<InMemoryJunctionStore>();
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
    // 牌号删除 -> 技术要求 brand 列 SET NULL（V011 FK 语义联动；ef 模式由 DB 承担）
    builder.Services.AddHostedService<CatalogBrandFkHook>();
}

var app = builder.Build();

app.UseCors("labFrontend");
app.UseAuthentication();
app.UseAuthorization();

// 异常 → HTTP 映射（镜像 springboot GlobalExceptionHandler）：
// KeyNotFound → 404；AuthenticationException/UnauthorizedAccess → 401；ArgumentException → 400
app.UseExceptionHandler(errorApp =>
    errorApp.Run(async context =>
    {
        var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.Response.StatusCode = ex switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            AuthenticationException or UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };
        await context.Response.WriteAsJsonAsync(new { error = ex?.Message ?? "internal error" });
    }));

app.MapControllers();

app.Run();

// 测试宿主（MvcTesting）需要这个入口点声明 —— partial class 由 SDK 生成。
public partial class Program { }
