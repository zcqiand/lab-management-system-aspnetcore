using System.Security.Authentication;
using Lab.AspNetCore.Data;
using Lab.AspNetCore.Directory;
using Lab.AspNetCore.Security;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

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

// B2：码表/计算规则/技术要求（内存存储，镜像 springboot B2 语义；换 EF 仓储时 service 不动）
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddSingleton<InMemoryCatalogStore>();
builder.Services.AddSingleton<InMemoryRuleStore>();
builder.Services.AddSingleton<InMemoryRequirementStore>();
builder.Services.AddSingleton<InMemoryFlowStore>();
builder.Services.AddSingleton<SummaryService>();
builder.Services.AddSingleton<ContractService>();
builder.Services.AddSingleton<SampleReceiptService>();
builder.Services.AddSingleton<SampleService>();
builder.Services.AddSingleton<TestRecordService>();
builder.Services.AddSingleton<ReportFlowService>();
builder.Services.AddSingleton<InMemoryDictionaryStore>();
builder.Services.AddSingleton<InMemoryJunctionStore>();
builder.Services.AddSingleton<DictionaryService>();
builder.Services.AddSingleton<JunctionService>();
builder.Services.AddSingleton<CatalogService>();
builder.Services.AddSingleton<CalculationRuleService>();
builder.Services.AddSingleton<TechnicalRequirementService>();
// 牌号删除 → 技术要求 brand 列 SET NULL（V011 FK 语义联动）
builder.Services.AddHostedService<CatalogBrandFkHook>();

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
