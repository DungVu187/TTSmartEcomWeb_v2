using TTSmartEcom.Application.Abstractions.Users;

namespace TTSmartEcom.Api.Integrations;

public sealed class DisabledPasswordResetEmailSender : IPasswordResetEmailSender
{
    public Task<PasswordResetEmailDeliveryStatus> SendAsync(
        PasswordResetEmailMessage message,
        CancellationToken cancellationToken) =>
        Task.FromResult(PasswordResetEmailDeliveryStatus.Unavailable);
}
