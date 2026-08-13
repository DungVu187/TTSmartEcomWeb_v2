using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Application.Abstractions.Users;

namespace TTSmartEcom.Api.Integrations;

public interface ISmtpMailTransport
{
    Task SendAsync(SmtpMailEnvelope envelope, CancellationToken cancellationToken);
}

public sealed record SmtpMailEnvelope(
    string Host,
    int Port,
    string UserName,
    string Password,
    string Recipient,
    string Subject,
    string HtmlBody,
    TimeSpan Timeout);

public sealed class SmtpMailTransport : ISmtpMailTransport
{
    public async Task SendAsync(SmtpMailEnvelope envelope, CancellationToken cancellationToken)
    {
        using MailMessage message = new()
        {
            From = new MailAddress(envelope.UserName, "TTSmart Ecom"),
            Subject = envelope.Subject,
            Body = envelope.HtmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(new MailAddress(envelope.Recipient));

        using SmtpClient client = new(envelope.Host, envelope.Port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(envelope.UserName, envelope.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = checked((int)Math.Clamp(envelope.Timeout.TotalMilliseconds, 1_000, 60_000)),
        };
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(envelope.Timeout);
        await client.SendMailAsync(message, timeout.Token);
    }
}

public sealed partial class SmtpPasswordResetEmailSender(
    ISmtpMailTransport transport,
    IOptions<ExternalServicesOptions> options,
    ILogger<SmtpPasswordResetEmailSender> logger) : IPasswordResetEmailSender
{
    public async Task<PasswordResetEmailDeliveryStatus> SendAsync(
        PasswordResetEmailMessage message,
        CancellationToken cancellationToken)
    {
        ExternalServicesOptions value = options.Value;
        if (!TryConfiguration(value, out string userName, out string password) ||
            !MailAddress.TryCreate(message.RecipientEmail, out _))
        {
            LogUnavailable(logger);
            return PasswordResetEmailDeliveryStatus.Unavailable;
        }

        string name = HtmlEncoder.Default.Encode(string.IsNullOrWhiteSpace(message.RecipientName)
            ? "Khách hàng"
            : message.RecipientName.Trim()[..Math.Min(message.RecipientName.Trim().Length, 160)]);
        string otp = HtmlEncoder.Default.Encode(message.Otp);
        int minutes = Math.Max(1, (int)Math.Ceiling(message.ValidFor.TotalMinutes));
        string body = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto">
              <h2>Khôi phục mật khẩu — TTSmart</h2>
              <p>Xin chào <strong>{name}</strong>,</p>
              <p>Mã OTP dùng để đặt lại mật khẩu của bạn là:</p>
              <p style="font-size:28px;font-weight:bold;letter-spacing:4px">{otp}</p>
              <p>Mã có hiệu lực trong {minutes} phút. Nếu không yêu cầu thao tác này, hãy bỏ qua email.</p>
            </div>
            """;

        try
        {
            await transport.SendAsync(new SmtpMailEnvelope(
                value.GmailSmtpHost.Trim(),
                value.GmailSmtpPort,
                userName,
                password,
                message.RecipientEmail,
                "Mã OTP khôi phục mật khẩu của bạn — TTSmart",
                body,
                TimeSpan.FromSeconds(Math.Clamp(value.GmailTimeoutSeconds, 5, 60))), cancellationToken);
            return PasswordResetEmailDeliveryStatus.Delivered;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimeout(logger);
            return PasswordResetEmailDeliveryStatus.Unavailable;
        }
        catch (SmtpException exception)
        {
            LogFailure(logger, exception);
            return PasswordResetEmailDeliveryStatus.Unavailable;
        }
        catch (FormatException exception)
        {
            LogFailure(logger, exception);
            return PasswordResetEmailDeliveryStatus.Unavailable;
        }
        catch (InvalidOperationException exception)
        {
            LogFailure(logger, exception);
            return PasswordResetEmailDeliveryStatus.Unavailable;
        }
    }

    private static bool TryConfiguration(
        ExternalServicesOptions value,
        out string userName,
        out string password)
    {
        userName = value.GmailUser?.Trim() ?? string.Empty;
        password = value.GmailAppPassword ?? string.Empty;
        return userName.Length is > 0 and <= 320 && MailAddress.TryCreate(userName, out _) &&
            password.Length is > 0 and <= 1_024 &&
            value.GmailSmtpHost.Trim().Length is > 0 and <= 255 &&
            value.GmailSmtpPort is > 0 and <= 65_535;
    }

    [LoggerMessage(EventId = 4901, Level = LogLevel.Warning, Message = "Password reset email provider is not configured")]
    private static partial void LogUnavailable(ILogger logger);

    [LoggerMessage(EventId = 4902, Level = LogLevel.Warning, Message = "Password reset email provider timed out")]
    private static partial void LogTimeout(ILogger logger);

    [LoggerMessage(EventId = 4903, Level = LogLevel.Warning, Message = "Password reset email provider failed")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
