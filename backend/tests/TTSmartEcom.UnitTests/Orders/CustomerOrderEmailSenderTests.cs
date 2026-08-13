using System.Net.Mail;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Integrations;
using TTSmartEcom.Application.Orders;

namespace TTSmartEcom.UnitTests.Orders;

public sealed class CustomerOrderEmailSenderTests
{
    [Fact]
    public async Task SendAsync_WithConfiguredTransport_ShouldBuildLegacyEnvelopeAndEncodeUserContent()
    {
        CapturingTransport transport = new();
        CustomerOrderEmailSender sender = Create(transport);
        CustomerOrderNotification notification = Notification() with
        {
            UserName = "<script>alert('customer')</script>",
            UserPhone = "0900&123",
            StationNames = "Trạm <A>",
            StationCodes = "STA&01",
        };

        bool sent = await sender.SendAsync(notification, CancellationToken.None);

        Assert.True(sent);
        SmtpMailEnvelope envelope = Assert.IsType<SmtpMailEnvelope>(transport.Envelope);
        Assert.Equal("smtp.example.test", envelope.Host);
        Assert.Equal(587, envelope.Port);
        Assert.Equal("sender@example.test", envelope.UserName);
        Assert.Equal("synthetic-password", envelope.Password);
        Assert.Equal("admin@example.test", envelope.Recipient);
        Assert.Equal("Đơn hàng mới #TTS-01 — 251.000 ₫", envelope.Subject);
        Assert.Equal(TimeSpan.FromSeconds(10), envelope.Timeout);
        Assert.Contains("https://portal.example.test/admin/order", envelope.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", envelope.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("0900&amp;123", envelope.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Trạm &lt;A&gt;", envelope.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("STA&amp;01", envelope.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", envelope.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_WithoutRequiredConfiguration_ShouldReturnFalseBeforeTransport()
    {
        CapturingTransport transport = new();
        CustomerOrderEmailSender sender = Create(transport, password: null);

        bool sent = await sender.SendAsync(Notification(), CancellationToken.None);

        Assert.False(sent);
        Assert.Null(transport.Envelope);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAsync_WhenProviderFails_ShouldReturnFalse(bool timeout)
    {
        CapturingTransport transport = new()
        {
            Exception = timeout
                ? new OperationCanceledException("synthetic timeout")
                : new SmtpException("synthetic provider failure"),
        };
        CustomerOrderEmailSender sender = Create(transport);

        bool sent = await sender.SendAsync(Notification(), CancellationToken.None);

        Assert.False(sent);
    }

    private static CustomerOrderEmailSender Create(
        CapturingTransport transport,
        string? password = "synthetic-password") => new(
        transport,
        Options.Create(new ExternalServicesOptions
        {
            PublicAddress = "https://portal.example.test/",
            GmailUser = "sender@example.test",
            GmailAppPassword = password,
            GmailSmtpHost = "smtp.example.test",
            GmailSmtpPort = 587,
            GmailTimeoutSeconds = 10,
            AdminNotifyEmail = "admin@example.test",
        }),
        NullLogger<CustomerOrderEmailSender>.Instance);

    private static CustomerOrderNotification Notification() => new(
        "TTS-01",
        "0900000000",
        "Khách hàng",
        251000,
        new DateTimeOffset(2026, 8, 13, 1, 2, 0, TimeSpan.Zero),
        "Trạm A",
        "STA");

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
