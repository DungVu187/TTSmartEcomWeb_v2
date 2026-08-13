using TTSmartEcom.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace TTSmartEcom.Api.Middleware;

public sealed partial class LegacyCsrfOriginMiddleware(RequestDelegate next, IOptions<CorsOptions> options, ILogger<LegacyCsrfOriginMiddleware> logger)
{
    private static readonly HashSet<string> UnsafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete,
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (UnsafeMethods.Contains(context.Request.Method)
            && context.Request.Cookies.ContainsKey("authToken")
            && !IsTrustedBrowserRequest(context.Request))
        {
            LogUntrustedOrigin(logger);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers["X-Error-Code"] = "TTS-CSRF-0001";
            await context.Response.WriteAsJsonAsync(new { message = "Forbidden", errorCode = "TTS-CSRF-0001" });
            return;
        }

        await next(context);
    }

    private bool IsTrustedBrowserRequest(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Origin", out StringValues origins) && !StringValues.IsNullOrEmpty(origins))
        {
            return origins.Count == 1 && IsAllowedOrigin(origins[0]);
        }

        if (request.Headers.TryGetValue("Referer", out StringValues referers) && referers.Count == 1
            && Uri.TryCreate(referers[0], UriKind.Absolute, out Uri? referer))
        {
            return IsAllowedOrigin(referer.GetLeftPart(UriPartial.Authority));
        }

        // A browser that omits both Origin and Referer must prove an exact same-origin
        // navigation through Fetch Metadata. "same-site" is insufficient: an unrelated
        // or compromised sibling origin can be same-site but outside the configured CORS
        // allowlist. Non-browser clients should authenticate without the browser cookie.
        if (!request.Headers.TryGetValue("Sec-Fetch-Site", out StringValues sites) || sites.Count != 1)
        {
            return false;
        }

        string? site = sites[0];
        return string.Equals(site, "same-origin", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAllowedOrigin(string? origin) => !string.IsNullOrWhiteSpace(origin)
        && options.Value.AllowedOrigins.Contains(origin.TrimEnd('/'), StringComparer.OrdinalIgnoreCase);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Rejected state-changing request from untrusted origin")]
    private static partial void LogUntrustedOrigin(ILogger logger);
}
