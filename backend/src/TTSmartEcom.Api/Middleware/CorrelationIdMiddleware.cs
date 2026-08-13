using System.Diagnostics;

namespace TTSmartEcom.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ReadCorrelationId(context.Request.Headers[HeaderName].FirstOrDefault())
            ?? Activity.Current?.Id
            ?? Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object?>
        {
            [ItemKey] = correlationId,
        });

        await next(context);
    }

    private static string? ReadCorrelationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return null;
        }

        return value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            ? value
            : null;
    }
}
