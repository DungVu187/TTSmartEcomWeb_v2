using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TTSmartEcom.Application.Abstractions.Authentication;

namespace TTSmartEcom.Api.Realtime;

internal sealed class SocketIoAuthenticator(IServiceScopeFactory scopeFactory)
{
    private static readonly HashSet<string> AllowedRoles =
        new(["superadmin", "admin", "staff"], StringComparer.Ordinal);

    public async Task<SocketIoAuthenticationState> AuthenticateAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        string? cookie = context.Request.Cookies["authToken"];
        byte[] fingerprint = SocketIoProtocol.CookieFingerprint(cookie);
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return new SocketIoAuthenticationState(null, null, fingerprint, false);
        }

        AuthenticateResult result = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return new SocketIoAuthenticationState(null, null, fingerprint, false);
        }

        string? userId = result.Principal.FindFirstValue("userId")
            ?? result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new SocketIoAuthenticationState(null, null, fingerprint, false);
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IUserIdentityReader identityReader = scope.ServiceProvider.GetRequiredService<IUserIdentityReader>();
        UserIdentitySnapshot? identity = await identityReader.FindByIdAsync(userId, cancellationToken);
        bool authorized = identity is not null
            && AllowedRoles.Contains(identity.Role)
            && !IsIssuedBeforePasswordChange(result.Principal, identity);

        return new SocketIoAuthenticationState(
            identity?.Id ?? userId,
            identity?.Role,
            fingerprint,
            authorized);
    }

    private static bool IsIssuedBeforePasswordChange(
        ClaimsPrincipal principal,
        UserIdentitySnapshot identity)
    {
        Claim? issuedAt = principal.FindFirst("iat");
        return identity.PasswordChangedAt.HasValue
            && issuedAt is not null
            && long.TryParse(
                issuedAt.Value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out long seconds)
            && DateTimeOffset.FromUnixTimeSeconds(seconds) < identity.PasswordChangedAt.Value;
    }
}
