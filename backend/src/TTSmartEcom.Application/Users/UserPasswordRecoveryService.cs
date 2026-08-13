using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TTSmartEcom.Application.Abstractions.Users;

namespace TTSmartEcom.Application.Users;

public sealed partial class UserPasswordRecoveryService(
    IUserRepository users,
    IPasswordResetEmailSender emailSender,
    IPasswordHashWriter passwordHasher,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);

    public async Task<PasswordResetRequestResult> RequestResetAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        string normalizedIdentifier = NormalizeIdentifier(identifier);
        PasswordRecoveryUser? user = await users.FindForPasswordRecoveryAsync(
            normalizedIdentifier,
            cancellationToken);
        if (user is null)
        {
            return new PasswordResetRequestResult(PasswordResetRequestStatus.UserNotFound);
        }

        if (!TryMaskEmail(user.Email, out string? maskedEmail))
        {
            return new PasswordResetRequestResult(PasswordResetRequestStatus.EmailMissing);
        }

        string otp = RandomNumberGenerator.GetInt32(100_000, 1_000_000)
            .ToString(CultureInfo.InvariantCulture);
        DateTimeOffset expiresAt = timeProvider.GetUtcNow().Add(OtpLifetime);
        bool stored = await users.StorePasswordResetOtpAsync(
            user.Id,
            otp,
            expiresAt,
            cancellationToken);
        if (!stored)
        {
            return new PasswordResetRequestResult(PasswordResetRequestStatus.UserNotFound);
        }

        PasswordResetEmailDeliveryStatus deliveryStatus = await emailSender.SendAsync(
            new PasswordResetEmailMessage(user.Email!, otp, user.Name, OtpLifetime),
            cancellationToken);
        if (deliveryStatus != PasswordResetEmailDeliveryStatus.Delivered)
        {
            await users.ClearPasswordResetOtpAsync(user.Id, otp, cancellationToken);
            return new PasswordResetRequestResult(PasswordResetRequestStatus.ProviderUnavailable);
        }

        return new PasswordResetRequestResult(
            PasswordResetRequestStatus.Success,
            user.Phone,
            maskedEmail);
    }

    public async Task<PasswordResetResult> ResetAsync(
        string identifier,
        string otp,
        string newPassword,
        CancellationToken cancellationToken)
    {
        string normalizedIdentifier = NormalizeIdentifier(identifier);
        PasswordRecoveryUser? user = await users.FindForPasswordRecoveryAsync(
            normalizedIdentifier,
            cancellationToken);
        if (user is null)
        {
            return new PasswordResetResult(PasswordResetStatus.UserNotFound);
        }

        if (!OtpPattern().IsMatch(otp))
        {
            return new PasswordResetResult(PasswordResetStatus.OtpInvalid);
        }

        DateTimeOffset changedAt = timeProvider.GetUtcNow();
        string replacementLoginToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
            .ToLowerInvariant();
        string passwordHash = passwordHasher.Hash(newPassword);
        bool reset = await users.ResetPasswordWithOtpAsync(
            user.Id,
            otp,
            changedAt,
            passwordHash,
            replacementLoginToken,
            changedAt,
            cancellationToken);
        return new PasswordResetResult(reset
            ? PasswordResetStatus.Success
            : PasswordResetStatus.OtpInvalid);
    }

    private static string NormalizeIdentifier(string identifier)
    {
        string trimmed = identifier.Trim();
        if (EmailPattern().IsMatch(trimmed))
        {
            return trimmed.ToLowerInvariant();
        }

        string phone = PhoneSeparatorPattern().Replace(trimmed, string.Empty);
        if (phone.StartsWith("+84", StringComparison.Ordinal))
        {
            return "0" + phone[3..];
        }

        if (VietnamCountryCodePattern().IsMatch(phone))
        {
            return "0" + phone[2..];
        }

        return phone;
    }

    private static bool TryMaskEmail(string? email, out string? maskedEmail)
    {
        maskedEmail = null;
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        int at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1)
        {
            return false;
        }

        maskedEmail = $"{email[..Math.Min(2, at)]}***@{email[(at + 1)..]}";
        return true;
    }

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"[\s.\-()]", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneSeparatorPattern();

    [GeneratedRegex(@"^84\d{9,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex VietnamCountryCodePattern();

    [GeneratedRegex(@"^\d{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex OtpPattern();
}

public sealed record PasswordResetRequestResult(
    PasswordResetRequestStatus Status,
    string? Phone = null,
    string? MaskedEmail = null);

public enum PasswordResetRequestStatus
{
    Success,
    UserNotFound,
    EmailMissing,
    ProviderUnavailable,
}

public sealed record PasswordResetResult(PasswordResetStatus Status);

public enum PasswordResetStatus
{
    Success,
    UserNotFound,
    OtpInvalid,
}
