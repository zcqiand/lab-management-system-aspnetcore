namespace Lab.AspNetCore.Auth.Sso;

/// <summary>
/// saas upstream 调用失败聚合。子类对应不同 HTTP 状态码语义：
/// <list type="bullet">
///   <item>{@link InvalidGrant} — HTTP 400，code 已用 / 过期 / grant_type 错</item>
///   <item>{@link UnauthorizedClient} — HTTP 401，client_id / client_secret 错</item>
///   <item>{@link UpstreamUnavailable} — HTTP 5xx / 连接失败</item>
/// </list>
/// </summary>
public abstract class SaasAuthException : Exception
{
    public int Status { get; }

    protected SaasAuthException(string message, int status)
        : base(message)
    {
        Status = status;
    }

    public sealed class InvalidGrant : SaasAuthException
    {
        public InvalidGrant(string message) : base(message, 400) { }
    }

    public sealed class UnauthorizedClient : SaasAuthException
    {
        public UnauthorizedClient(string message) : base(message, 401) { }
    }

    public sealed class UpstreamUnavailable : SaasAuthException
    {
        public UpstreamUnavailable(string message) : base(message, 502) { }
        public UpstreamUnavailable(string message, Exception inner) : base(message, 502)
        {
            // Exception inner param unused; we just track via base
        }
    }
}
