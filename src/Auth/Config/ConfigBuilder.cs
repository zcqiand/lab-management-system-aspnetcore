// 启动期 env 必填 helper。ADR-0019 禁 env 默认值兜底（运行时扩展）。
//
// 镜像 saas-aspnetcore Program.cs:13-32 BuildJwtSigningKey 模式。集中所有
// require-config 路径，缺失即 throw，参考 javadoc 注释指明 dev/prod 配置位置。
//
// 用法：
//   var key = ConfigBuilder.RequireJwtSigningKey(builder.Configuration);
//   var cors = ConfigBuilder.RequireCorsOrigins(builder.Configuration);

using System.Text;

namespace Lab.AspNetCore.Auth.Config;

public static class ConfigBuilder
{
    /// <summary>
    /// JWT signing key（HS256）。缺失或 <32 字节 throw。dev 期 .env.local 注入,
    /// prod 走 deploy 脚本 → VPS env-file → GitHub Secrets。
    /// </summary>
    public static string RequireJwtSigningKey(IConfiguration cfg)
    {
        var key = cfg["JWT_SIGNING_KEY"];
        if (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException(
                "JWT_SIGNING_KEY env is missing or <32 bytes (ADR-0019 禁字面默认值). " +
                "Set in .env.local (dev) or GitHub Secrets → deploy 脚本 (prod).");
        }
        return key;
    }

    /// <summary>
    /// JWT issuer。缺失 throw。dev 用 "lab-management-system",prod 走 Vault/deploy 注入。
    /// </summary>
    public static string RequireJwtIssuer(IConfiguration cfg)
    {
        var v = cfg["JWT_ISSUER"];
        if (string.IsNullOrEmpty(v))
        {
            throw new InvalidOperationException(
                "JWT_ISSUER env is required (ADR-0019 禁字面默认值). " +
                "Set in .env.local (dev) or deploy 脚本 (prod).");
        }
        return v;
    }

    /// <summary>
    /// CORS allowed origins（逗号分隔）。缺失 throw。dev 期用 3 个 family 端口,
    /// prod 走真域名(走 deploy 注入,不允许 localhost 兜底)。
    /// </summary>
    public static string[] RequireCorsOrigins(IConfiguration cfg)
    {
        var v = cfg["LAB_CORS_ALLOWED_ORIGINS"];
        if (string.IsNullOrEmpty(v))
        {
            throw new InvalidOperationException(
                "LAB_CORS_ALLOWED_ORIGINS env is required (ADR-0019 禁 localhost 兜底). " +
                "Set comma-separated origins in .env.local (dev) or deploy 脚本 (prod).");
        }
        return v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // REQ-2026-023：RequireDevPassword（Lab:Auth:DevPassword）随本地口令校验路径退役——
    // 账密校验代理 saas IdP，凭据真源=saas 库。
}
