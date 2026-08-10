namespace DmarcAnalyzer.Web.Infrastructure;

public static class HttpContextCspExtensions
{
    /// <summary>The current request's CSP script nonce, set by <see cref="SecurityHeadersMiddleware"/>.</summary>
    public static string CspNonce(this HttpContext context) =>
        context.Items.TryGetValue(SecurityHeadersMiddleware.NonceItemKey, out var value) ? (string)value! : string.Empty;
}
