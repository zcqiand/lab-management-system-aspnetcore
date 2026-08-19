namespace Lab.AspNetCore.Auth.Jwt;

using Microsoft.IdentityModel.Tokens;

/// <summary>
/// 构造 JwtBearer 用的 TokenValidationParameters。HS256 真签名验证 + 强制 issuer/lifetime/signature 全部开启。
/// 替代原 Program.cs 中 RequireSignedTokens=false + SignatureValidator 绕过漏洞。
/// </summary>
public static class LabTokenValidationFactory
{
    public static TokenValidationParameters Build(LabJwtSigner signer)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signer.SymmetricKey(),
            ValidateIssuer = true,
            ValidIssuer = "lab-management-system", // 见 LabOptions.Jwt.Issuer
            ValidateAudience = false,
            ValidateLifetime = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        };
    }
}
