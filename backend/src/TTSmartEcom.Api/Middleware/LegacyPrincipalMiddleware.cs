using System.Security.Claims;
using TTSmartEcom.Application.Abstractions.Authentication;

namespace TTSmartEcom.Api.Middleware;

public sealed partial class LegacyPrincipalMiddleware(RequestDelegate next, IUserIdentityReader identityReader, ILogger<LegacyPrincipalMiddleware> logger)
{
    public const string IdentityItemKey = "LegacyIdentity";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (context.Items.ContainsKey(IdentityItemKey))
            {
                await next(context);
                return;
            }

            string? userId = context.User.FindFirstValue("userId") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
            else
            {
                UserIdentitySnapshot? identity = await identityReader.FindByIdAsync(userId, context.RequestAborted);
                if (identity is null || IsIssuedBeforePasswordChange(context.User, identity))
                {
                    LogStaleIdentity(logger);
                    context.User = new ClaimsPrincipal(new ClaimsIdentity());
                }
                else
                {
                    context.Items[IdentityItemKey] = identity;
                }
            }
        }

        await next(context);
    }

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Rejected stale or missing legacy user identity")]
    private static partial void LogStaleIdentity(ILogger logger);

    private static bool IsIssuedBeforePasswordChange(ClaimsPrincipal principal, UserIdentitySnapshot identity)
    {
        Claim? issuedAt = principal.FindFirst("iat");
        return identity.PasswordChangedAt.HasValue
            && issuedAt is not null
            && long.TryParse(issuedAt.Value, out long seconds)
            && DateTimeOffset.FromUnixTimeSeconds(seconds) < identity.PasswordChangedAt.Value;
    }
}
