using Lab.AspNetCore.Directory;
using Lab.AspNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// B1 认证域底座（镜像 lab-springboot SecurityConfig）：
//   permitAll = login / refresh / sso/**，其余 authenticated。
builder.Services.AddControllers();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true; // claim 名保持 "sub"/"tenant_id" 原样（对齐 spring 侧）
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = false,
            RequireSignedTokens = false, // dev：容忍 alg=none 模拟 token；production 换 true + issuer-uri
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

var app = builder.Build();

app.UseCors("labFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// 测试宿主（MvcTesting）需要这个入口点声明 —— partial class 由 SDK 生成。
public partial class Program { }
