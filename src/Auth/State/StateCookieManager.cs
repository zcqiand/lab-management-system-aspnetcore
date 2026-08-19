namespace Lab.AspNetCore.Auth.State;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// StateCookieManager — OAuth 2.0 state 参数 CSRF 保护（RFC 6749 §10.12）。
///
/// <p>authorize 时生成随机 nonce + 业务载荷 + HS256 签名 = cookie value（nonce.signature.payload）;
/// callback 时校验 body.state == cookie nonce + cookie 签名有效 + 5min 内。
/// </summary>
public sealed class StateCookieManager
{
    public const string CookieName = "lab_sso_state";
    private const int MaxAgeSeconds = 300;

    private readonly byte[] _key;

    public StateCookieManager(string secret)
    {
        if (string.IsNullOrEmpty(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("Lab:Jwt:Secret required for StateCookieManager (>=32 bytes)");
        }
        _key = Encoding.UTF8.GetBytes(secret);
    }

    public SignedState Issue(string businessRedirect)
    {
        var nonceBytes = new byte[16];
        RandomNumberGenerator.Fill(nonceBytes);
        var nonce = Base64UrlEncode(nonceBytes);
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{{\"nonce\":\"{Escape(nonce)}\",\"redirect\":\"{Escape(businessRedirect ?? "")}\",\"ts\":{ts}}}";
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var sig = Hmac($"{nonce}.{payloadB64}");
        return new SignedState(nonce, $"{nonce}.{sig}.{payloadB64}", ts);
    }

    public string Verify(string cookieValue, string bodyState)
    {
        if (string.IsNullOrEmpty(cookieValue))
        {
            throw new InvalidOperationException("missing lab_sso_state cookie");
        }
        if (string.IsNullOrEmpty(bodyState))
        {
            throw new InvalidOperationException("missing state in body");
        }
        var parts = cookieValue.Split('.');
        if (parts.Length != 3)
        {
            throw new InvalidOperationException("malformed lab_sso_state cookie");
        }
        var nonce = parts[0];
        var signature = parts[1];
        var payload = parts[2];
        var expectedSig = Hmac($"{nonce}.{payload}");
        if (!ConstantTimeEquals(expectedSig, signature))
        {
            throw new InvalidOperationException("lab_sso_state signature mismatch");
        }
        if (nonce != bodyState)
        {
            throw new InvalidOperationException("state nonce mismatch (CSRF suspected)");
        }
        var json = Encoding.UTF8.GetString(Base64UrlDecode(payload));
        var sp = JsonSerializer.Deserialize<StatePayload>(json)
            ?? throw new InvalidOperationException("invalid state payload");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (sp.ts == 0 || now - sp.ts > MaxAgeSeconds)
        {
            throw new InvalidOperationException("lab_sso_state expired");
        }
        return sp.redirect ?? "";
    }

    private string Hmac(string input)
    {
        using var hmac = new HMACSHA256(_key);
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Base64UrlEncode(sig);
    }

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

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

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

    public sealed class SignedState
    {
        public string Nonce { get; }
        public string CookieValue { get; }
        public long Ts { get; }

        public SignedState(string nonce, string cookieValue, long ts)
        {
            Nonce = nonce;
            CookieValue = cookieValue;
            Ts = ts;
        }
    }

    private sealed class StatePayload
    {
        public string? nonce { get; set; }
        public string? redirect { get; set; }
        public long ts { get; set; }
    }
}
