using System.Security.Cryptography;

namespace DmarcAnalyzer.Web.Infrastructure;

/// <summary>
/// Adds baseline hardening response headers to every request, including a per-request CSP nonce for
/// the one page (Dashboard/DomainDetail) that still needs an inline &lt;script&gt; block for Chart.js
/// wiring. Registered before UseStaticFiles in Program.cs so these headers also cover static assets.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string NonceItemKey = "csp-nonce";

    public async Task InvokeAsync(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[NonceItemKey] = nonce;

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // All first-party JS/CSS is vendored under wwwroot/lib (Bootstrap, jQuery, Chart.js) — none
            // of it is loaded from a CDN — so a strict default-src 'self' costs nothing functionally
            // while blocking exfiltration to (or code loaded from) any other origin. script-src uses the
            // per-request nonce rather than 'unsafe-inline', since only one page has inline JS; style-src
            // keeps 'unsafe-inline' for the handful of inline style="" attributes scattered across the
            // views rather than a broader refactor — a much lower-value target for injection than
            // inline script.
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                $"script-src 'self' 'nonce-{nonce}'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self'; " +
                "object-src 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'; " +
                "frame-ancestors 'none'";

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), payment=()";
            headers.Remove("Server");

            return Task.CompletedTask;
        });

        await next(context);
    }
}
