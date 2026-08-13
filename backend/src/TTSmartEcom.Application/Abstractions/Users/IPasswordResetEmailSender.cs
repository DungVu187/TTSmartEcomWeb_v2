namespace TTSmartEcom.Application.Abstractions.Users;

public interface IPasswordResetEmailSender
{
    Task<PasswordResetEmailDeliveryStatus> SendAsync(
        PasswordResetEmailMessage message,
        CancellationToken cancellationToken);
}

public sealed record PasswordResetEmailMessage(
    string RecipientEmail,
    string Otp,
    string? RecipientName,
    TimeSpan ValidFor);

public enum PasswordResetEmailDeliveryStatus
{
    Delivered,
    Unavailable,
}
