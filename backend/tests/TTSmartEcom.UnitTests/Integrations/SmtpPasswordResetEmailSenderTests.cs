using System.Net.Mail;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Integrations;
using TTSmartEcom.Application.Abstractions.Users;

namespace TTSmartEcom.UnitTests.Integrations;

public sealed class SmtpPasswordResetEmailSenderTests
{
    [Fact]
    public async Task SendAsync_WithConfiguredTransport_ShouldEncodeUserContentAndDeliver()
    {
        CapturingTransport transport = new();
        SmtpPasswordResetEmailSender sender = Create(transport);

        PasswordResetEmailDeliveryStatus status = await sender.SendAsync(
            new PasswordResetEmailMessage("customer@example.test", "123456", "<script>alert(1)</script>", TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.Equal(PasswordResetEmailDeliveryStatus.Delivered, status);
        SmtpMailEnvelope envelope = Assert.IsType<SmtpMailEnvelope>(transport.Envelope);
        Assert.Equal("smtp.example.test", envelope.Host);
        Assert.Equal(587, envelope.Port);
        Assert.Equal("sender@example.test", envelope.UserName);
        Assert.Equal("synthetic-password", envelope.Password);
        Assert.Contains("&lt;script&gt;", envelope.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", envelope.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("123456", envelope.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_WithoutCredential_ShouldFailClosedBeforeTransport()
    {
        CapturingTransport transport = new();
        SmtpPasswordResetEmailSender sender = Create(transport, password: null);

        PasswordResetEmailDeliveryStatus status = await sender.SendAsync(
            new PasswordResetEmailMessage("customer@example.test", "123456", null, TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.Equal(PasswordResetEmailDeliveryStatus.Unavailable, status);
        Assert.Null(transport.Envelope);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAsync_WhenProviderFails_ShouldReturnUnavailable(bool timeout)
    {
        CapturingTransport transport = new()
        {
            Exception = timeout ? new OperationCanceledException() : new SmtpException("synthetic failure"),
        };
        SmtpPasswordResetEmailSender sender = Create(transport);

        PasswordResetEmailDeliveryStatus status = await sender.SendAsync(
            new PasswordResetEmailMessage("customer@example.test", "123456", "Customer", TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.Equal(PasswordResetEmailDeliveryStatus.Unavailable, status);
    }

    private static SmtpPasswordResetEmailSender Create(CapturingTransport transport, string? password = "synthetic-password") => new(
        transport,
        Options.Create(new ExternalServicesOptions
        {
            GmailUser = "sender@example.test",
            GmailAppPassword = password,
            GmailSmtpHost = "smtp.example.test",
            GmailSmtpPort = 587,
            GmailTimeoutSeconds = 10,
        }),
        NullLogger<SmtpPasswordResetEmailSender>.Instance);

    private sealed class CapturingTransport : ISmtpMailTransport
    {
        public SmtpMailEnvelope? Envelope { get; private set; }
        public Exception? Exception { get; init; }

        public Task SendAsync(SmtpMailEnvelope envelope, CancellationToken cancellationToken)
        {
            Envelope = envelope;
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }
}
