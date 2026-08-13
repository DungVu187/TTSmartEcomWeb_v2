using System.Globalization;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Application.Orders;

namespace TTSmartEcom.Api.Integrations;

public sealed partial class CustomerOrderEmailSender(
    ISmtpMailTransport transport,
    IOptions<ExternalServicesOptions> options,
    ILogger<CustomerOrderEmailSender> logger) : ICustomerOrderEmailSender
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<bool> SendAsync(
        CustomerOrderNotification notification,
        CancellationToken cancellationToken)
    {
        ExternalServicesOptions value = options.Value;
        if (!TryConfiguration(value, out string user, out string password, out string recipient))
        {
            LogUnavailable(logger);
            return false;
        }

        string orderId = Html(notification.OrderId, 120);
        string userName = Html(Default(notification.UserName, "Chưa có tên"), 200);
        string phone = Html(Default(notification.UserPhone, "Không có"), 40);
        string stationCodes = Html(Default(notification.StationCodes, "Không có"), 500);
        string stationNames = Html(Default(notification.StationNames, "Không có"), 500);
        string amount = notification.Total.ToString("#,0.################", Vietnamese) + " ₫";
        string orderTime = notification.CreatedAt.ToOffset(VietnamOffset).ToString("f", Vietnamese);
        string adminPanelUrl = BuildAdminOrderUrl(value.PublicAddress);
        string body = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;border:1px solid #e0e0e0;border-radius:8px;overflow:hidden">
              <div style="background-color:#1565c0;padding:20px 24px"><h2 style="color:#fff;margin:0">Đơn hàng mới — TTSmart</h2></div>
              <div style="padding:24px;background-color:#fff">
                <p>Có một đơn hàng mới vừa được đặt trên hệ thống. Vui lòng xử lý sớm.</p>
                <table style="width:100%;border-collapse:collapse">
                  <tr><td><strong>Mã đơn hàng</strong></td><td>{orderId}</td></tr>
                  <tr><td><strong>Khách hàng</strong></td><td>{userName}</td></tr>
                  <tr><td><strong>Số điện thoại</strong></td><td>{phone}</td></tr>
                  <tr><td><strong>Mã trạm</strong></td><td>{stationCodes}</td></tr>
                  <tr><td><strong>Tên trạm</strong></td><td>{stationNames}</td></tr>
                  <tr><td><strong>Tổng tiền</strong></td><td>{amount}</td></tr>
                  <tr><td><strong>Thời gian đặt</strong></td><td>{WebUtility.HtmlEncode(orderTime)}</td></tr>
                </table>
                <p style="text-align:center"><a href="{WebUtility.HtmlEncode(adminPanelUrl)}">Xem đơn hàng trong Admin</a></p>
              </div>
            </div>
            """;

        try
        {
            await transport.SendAsync(new SmtpMailEnvelope(
                value.GmailSmtpHost.Trim(),
                value.GmailSmtpPort,
                user,
                password,
                recipient,
                $"Đơn hàng mới #{Plain(notification.OrderId, 120)} — {amount}",
                body,
                TimeSpan.FromSeconds(Math.Clamp(value.GmailTimeoutSeconds, 5, 60))),
                cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogTimeout(logger);
            return false;
        }
        catch (Exception exception) when (exception is SmtpException or FormatException or InvalidOperationException)
        {
            LogFailure(logger, exception.GetType().Name);
            return false;
        }
    }

    private static bool TryConfiguration(
        ExternalServicesOptions value,
        out string user,
        out string password,
        out string recipient)
    {
        user = value.GmailUser?.Trim() ?? string.Empty;
        password = value.GmailAppPassword ?? string.Empty;
        recipient = value.AdminNotifyEmail?.Trim() ?? string.Empty;
        return MailAddress.TryCreate(user, out _) &&
            user.Length <= 320 &&
            password.Length is > 0 and <= 1_024 &&
            MailAddress.TryCreate(recipient, out _) &&
            recipient.Length <= 320 &&
            value.GmailSmtpHost.Trim().Length is > 0 and <= 255 &&
            value.GmailSmtpPort is > 0 and <= 65_535;
    }

    private static string BuildAdminOrderUrl(string? address)
    {
        string value = address?.TrimEnd('/') ?? string.Empty;
        return value.Length == 0 ? "/admin/order" : value + "/admin/order";
    }

    private static string Html(string? value, int maximum) =>
        WebUtility.HtmlEncode(Plain(value, maximum));

    private static string Plain(string? value, int maximum)
    {
        string normalized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized[..Math.Min(normalized.Length, maximum)];
    }

    private static string Default(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    [LoggerMessage(EventId = 4921, Level = LogLevel.Warning, Message = "Order notification email provider is not configured")]
    private static partial void LogUnavailable(ILogger logger);

    [LoggerMessage(EventId = 4922, Level = LogLevel.Warning, Message = "Order notification email provider timed out")]
    private static partial void LogTimeout(ILogger logger);

    [LoggerMessage(EventId = 4923, Level = LogLevel.Warning, Message = "Order notification email provider failed with {ErrorType}")]
    private static partial void LogFailure(ILogger logger, string errorType);
}
