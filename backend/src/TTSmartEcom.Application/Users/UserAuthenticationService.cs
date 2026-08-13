using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Users;
using System.Security.Cryptography;

namespace TTSmartEcom.Application.Users;

public sealed class UserAuthenticationService(
    IUserRepository users,
    IPasswordHashCompatibilityVerifier passwordVerifier)
{
    public async Task<UserRecord?> AuthenticateAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        UserRecord? user = await users.FindByLoginAsync(identifier.Trim(), cancellationToken);
        return user is not null && passwordVerifier.Verify(password, user.PasswordHash)
            ? user
            : null;
    }

    public async Task<UserRecord?> AuthenticateWithAutologinTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        // Keep the compatibility boundary bounded. The legacy AES format is
        // intentionally not accepted here because its key must never be read
        // or reproduced in V2. Direct legacy logInString values remain valid.
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512)
        {
            return null;
        }

        string replacementToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        return await users.ConsumeAutologinTokenAsync(token.Trim(), replacementToken, cancellationToken);
    }
}
