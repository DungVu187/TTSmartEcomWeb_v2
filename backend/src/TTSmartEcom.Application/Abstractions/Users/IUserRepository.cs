using TTSmartEcom.Application.Abstractions.Authentication;

namespace TTSmartEcom.Application.Abstractions.Users;

public interface IUserRepository
{
    Task<UserRecord?> FindByLoginAsync(string identifier, CancellationToken cancellationToken);

    Task<PasswordRecoveryUser?> FindForPasswordRecoveryAsync(
        string identifier,
        CancellationToken cancellationToken);

    Task<bool> StorePasswordResetOtpAsync(
        string userId,
        string otp,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task<bool> ClearPasswordResetOtpAsync(
        string userId,
        string expectedOtp,
        CancellationToken cancellationToken);

    Task<bool> ResetPasswordWithOtpAsync(
        string userId,
        string expectedOtp,
        DateTimeOffset now,
        string passwordHash,
        string replacementLoginToken,
        DateTimeOffset passwordChangedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically consumes a legacy autologin token and replaces it with a new
    /// random token. Returning null means that the token was not found or had
    /// already been consumed.
    /// </summary>
    Task<UserRecord?> ConsumeAutologinTokenAsync(string token, string replacementToken, CancellationToken cancellationToken);

    Task<UserIdentitySnapshot?> FindIdentityAsync(string userId, CancellationToken cancellationToken);
}

public sealed record UserRecord(
    string Id,
    string Phone,
    string? Email,
    string? Name,
    string PasswordHash,
    string Role,
    IReadOnlyCollection<string> Functions,
    IReadOnlyCollection<string> Permissions,
    DateTimeOffset? PasswordChangedAt);

public sealed record PasswordRecoveryUser(
    string Id,
    string Phone,
    string? Email,
    string? Name);
