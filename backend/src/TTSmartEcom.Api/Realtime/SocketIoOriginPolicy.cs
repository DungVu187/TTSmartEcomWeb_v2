using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;

namespace TTSmartEcom.Api.Realtime;

internal sealed class SocketIoOriginPolicy(IOptions<CorsOptions> options)
{
    private readonly HashSet<string> allowedOrigins = new(
        options.Value.AllowedOrigins
            .Select(static origin => origin.TrimEnd('/'))
            .Where(static origin => Uri.TryCreate(origin, UriKind.Absolute, out _)),
        StringComparer.OrdinalIgnoreCase);

    public bool IsAllowed(HttpRequest request)
    {
        string origin = request.Headers.Origin.ToString();
        return string.IsNullOrEmpty(origin) || allowedOrigins.Contains(origin.TrimEnd('/'));
    }

    public static bool IsSameOrigin(string? expected, HttpRequest request)
    {
        string actual = request.Headers.Origin.ToString();
        return string.Equals(expected, string.IsNullOrEmpty(actual) ? null : actual, StringComparison.OrdinalIgnoreCase);
    }
}
