namespace Lab.AspNetCore.Auth.Sso;

using System.Net;

/// <summary>
/// DelegatingHandler 把 saas upstream 4xx/5xx 映射成 {@link SaasAuthException} 子类。
/// </summary>
public sealed class SaasErrorMappingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            var resp = await base.SendAsync(request, ct);
            if (resp.IsSuccessStatusCode) return resp;
            var body = await resp.Content.ReadAsStringAsync(ct);
            var status = (int)resp.StatusCode;
            if (status == 401)
            {
                throw new SaasAuthException.UnauthorizedClient($"saas 401: {Truncate(body, 200)}");
            }
            if (status >= 400 && status < 500)
            {
                throw new SaasAuthException.InvalidGrant($"saas {status}: {Truncate(body, 200)}");
            }
            throw new SaasAuthException.UpstreamUnavailable($"saas {status}: {Truncate(body, 200)}");
        }
        catch (HttpRequestException e)
        {
            throw new SaasAuthException.UpstreamUnavailable("saas connect failed", e);
        }
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}
