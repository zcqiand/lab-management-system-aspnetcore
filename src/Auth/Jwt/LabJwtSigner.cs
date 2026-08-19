namespace Lab.AspNetCore.Auth.Jwt;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// LabJwtSigner — 真 HMAC HS256 JWT 签发/验证（对齐 B1 真后端 OAuth 2.0 + JWT 方案）。
///
/// <p>读取配置 Lab:Jwt:Secret（≥32 字节，缺/弱抛 {@link InvalidOperationException} 阻断 DI 容器）。提供：
/// <list type="bullet">
///   <item>{@link Issue(string, string?)} — access token（typ=access, 1h TTL, 支持 tenant_id claim）</item>
///   <item>{@link IssueRefresh(string, string)} — refresh token（typ=refresh, 7d TTL, 内嵌 saas refresh token）</item>
///   <item>{@link Verify(string)} — HMAC 验签 + iss + exp 校验</item>
///   <item>{@link SymmetricKey()} — 暴露 {@link SymmetricSecurityKey} 给 {@link LabTokenValidationFactory}</item>
/// </list>
///
/// <p>JWT 头 alg 字段固定 HS256（不允许 alg=none）。三段格式 base64url(header).base64url(payload).base64url(HMAC-SHA256)。
/// payload JSON 字段按字典序输出，保证签发和后端判定一致。
/// </summary>
public sealed class LabJwtSigner
{
    private const string Alg = "HS256";
    private const string TypAccess = "access";
    private const string TypRefresh = "refresh";
    private const string HeaderJson = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";

    private readonly SymmetricSecurityKey _key;
    private readonly string _issuer;
    private readonly int _accessTtlSeconds;
    private readonly int _refreshTtlSeconds;

    public LabJwtSigner(string secret, string issuer, int accessTtlSeconds, int refreshTtlSeconds)
    {
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException(
                "Lab:Jwt:Secret required (>=32 bytes). Set via env var or appsettings.");
        }
        if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException(
                $"Lab:Jwt:Secret must be >=32 bytes (got {Encoding.UTF8.GetByteCount(secret)}).");
        }
        if (string.IsNullOrEmpty(issuer))
        {
            throw new InvalidOperationException("Lab:Jwt:Issuer required");
        }
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _issuer = issuer;
        _accessTtlSeconds = accessTtlSeconds;
        _refreshTtlSeconds = refreshTtlSeconds;
    }

    public string Issue(string userId, string? tenantId)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new SortedDictionary<string, object>
        {
            ["sub"] = userId,
            ["iat"] = now,
            ["exp"] = now + _accessTtlSeconds,
            ["typ"] = TypAccess,
            ["iss"] = _issuer,
        };
        if (!string.IsNullOrEmpty(tenantId))
        {
            claims["tenant_id"] = tenantId;
        }
        return Sign(claims);
    }

    public string IssueRefresh(string userId, string saasRefreshToken)
    {
        if (string.IsNullOrEmpty(saasRefreshToken))
        {
            throw new ArgumentException("saasRefreshToken required for refresh token", nameof(saasRefreshToken));
        }
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new SortedDictionary<string, object>
        {
            ["sub"] = userId,
            ["saas_refresh_token"] = saasRefreshToken,
            ["iat"] = now,
            ["exp"] = now + _refreshTtlSeconds,
            ["typ"] = TypRefresh,
            ["iss"] = _issuer,
        };
        return Sign(claims);
    }

    public Dictionary<string, object> Verify(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new ArgumentException("token is empty");
        }
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new ArgumentException($"malformed JWT: expected 3 segments, got {parts.Length}");
        }
        var signingInput = $"{parts[0]}.{parts[1]}";
        var expectedSig = HmacBase64Url(signingInput);
        if (!ConstantTimeEquals(expectedSig, parts[2]))
        {
            throw new ArgumentException("bad signature");
        }
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        var claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson)
            ?? throw new ArgumentException("invalid payload");
        var map = new Dictionary<string, object>();
        foreach (var (k, v) in claims)
        {
            map[k] = v.ValueKind switch
            {
                JsonValueKind.String => v.GetString()!,
                JsonValueKind.Number => v.GetRawText(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                _ => v.GetRawText(),
            };
        }
        if (!map.TryGetValue("iss", out var iss) || iss?.ToString() != _issuer)
        {
            throw new ArgumentException("bad issuer");
        }
        if (map.TryGetValue("exp", out var expRaw)
            && long.TryParse(expRaw?.ToString(), out var expSec)
            && expSec < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            throw new ArgumentException("token expired");
        }
        return map;
    }

    public SymmetricSecurityKey SymmetricKey() => _key;

    private string Sign(IDictionary<string, object> claims)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes(HeaderJson));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(SerializeSorted(claims)));
        var signingInput = $"{header}.{payload}";
        return $"{signingInput}.{HmacBase64Url(signingInput)}";
    }

    private string HmacBase64Url(string input)
    {
        using var hmac = new HMACSHA256(_key.Key);
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Base64UrlEncode(sig);
    }

    private static string SerializeSorted(IDictionary<string, object> claims)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        bool first = true;
        foreach (var (k, v) in claims)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(Escape(k)).Append("\":");
            if (v == null) sb.Append("null");
            else if (v is bool b) sb.Append(b ? "true" : "false");
            else if (v is long || v is int || v is double || v is decimal) sb.Append(v);
            else sb.Append('"').Append(Escape(v.ToString()!)).Append('"');
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }
        return diff == 0;
    }
}
