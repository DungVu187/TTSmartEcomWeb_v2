using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging;
using TTSmartEcom.Application.Integrations;
using TTSmartEcom.Domain.Integrations;

namespace TTSmartEcom.Application.Orders;

/// <summary>
/// Snapshot tối thiểu của đơn khách hàng sau khi order và tồn kho đã commit.
/// Scheduler phải sao chép snapshot này sang scope nền; không giữ entity/request scope.
/// </summary>
public sealed record CustomerOrderNotification(
    string OrderId,
    string UserPhone,
    string? UserName,
    decimal Total,
    DateTimeOffset CreatedAt,
    string? StationNames,
    string? StationCodes);

public interface ICustomerOrderNotificationScheduler
{
    bool TrySchedule(CustomerOrderNotification notification);
}

public interface ICustomerOrderNotificationDispatcher
{
    Task DispatchAsync(CustomerOrderNotification notification, CancellationToken cancellationToken);
}

public interface ICustomerOrderEmailSender
{
    Task<bool> SendAsync(CustomerOrderNotification notification, CancellationToken cancellationToken);
}

public interface IZaloOrderMessageSender
{
    Task<bool> SendAsync(string message, CancellationToken cancellationToken);
}

/// <summary>
/// Credential chỉ dùng nội bộ cho delivery. Type này không được đưa vào HTTP response/log.
/// </summary>
public sealed record ZaloOrderDeliveryCredentials(
    string ConfigurationId,
    int Version,
    string AppId,
    string SecretKey,
    string RecipientUserId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset? ExpiresAt);

public interface IZaloOrderCredentialRepository
{
    Task<ZaloOrderDeliveryCredentials?> FindAsync(CancellationToken cancellationToken);

    Task<bool> TryUpdateTokensAsync(
        string configurationId,
        int expectedVersion,
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}

public sealed partial class CustomerOrderNotificationDispatcher(
    ICustomerOrderEmailSender email,
    IProviderSettingsRepository settings,
    ITelegramMessageSender telegram,
    IZaloOrderMessageSender zalo,
    ILogger<CustomerOrderNotificationDispatcher> logger) : ICustomerOrderNotificationDispatcher
{
    private const int TelegramParallelism = 4;

    public async Task DispatchAsync(
        CustomerOrderNotification notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        Task emailTask = RunChannelAsync(
            "email",
            token => email.SendAsync(notification, token), cancellationToken);
        Task zaloTask = RunChannelAsync(
            "zalo",
            token => zalo.SendAsync(CustomerOrderNotificationText.Zalo(notification), token),
            cancellationToken);
        Task telegramTask = RunChannelAsync(
            "telegram",
            token => SendTelegramAsync(notification, token), cancellationToken);
        await Task.WhenAll(emailTask, zaloTask, telegramTask);
    }

    private async Task<bool> SendTelegramAsync(
        CustomerOrderNotification notification,
        CancellationToken cancellationToken)
    {
        TelegramSettings configuration = await settings.GetTelegramAsync(cancellationToken);
        if (!configuration.Enabled) return false;

        string message = CustomerOrderNotificationText.Telegram(notification);
        TelegramRecipient[] recipients = configuration.Recipients
            .Where(static recipient =>
                recipient.Enabled &&
                !string.IsNullOrWhiteSpace(recipient.ChatId) &&
                recipient.NotifyTypes.Contains("new_order", StringComparer.Ordinal))
            .ToArray();
        if (recipients.Length == 0) return false;

        await Parallel.ForEachAsync(
            recipients,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = TelegramParallelism,
            },
            async (recipient, token) =>
            {
                await RunChannelAsync(
                    "telegram-recipient",
                    innerToken => telegram.SendAsync(recipient.ChatId, message, innerToken),
                    token);
            });
        return true;
    }

    private async Task RunChannelAsync(
        string channel,
        Func<CancellationToken, Task<bool>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogChannelFailure(logger, channel, exception.GetType().Name);
        }
    }

    [LoggerMessage(
        EventId = 4911,
        Level = LogLevel.Warning,
        Message = "Order notification channel {Channel} failed with {ErrorType}")]
    private static partial void LogChannelFailure(
        ILogger logger,
        string channel,
        string errorType);
}

internal static class CustomerOrderNotificationText
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static string Telegram(CustomerOrderNotification value)
    {
        string amount = Amount(value.Total);
        return string.Join('\n',
            "<b>Đơn hàng mới</b>",
            $"<b>Mã đơn:</b> {Html(value.OrderId, 120)}",
            $"<b>Khách hàng:</b> {Html(Default(value.UserName, "Không có"), 200)}",
            $"<b>SĐT:</b> {Html(Default(value.UserPhone, "Không có"), 40)}",
            $"<b>Mã trạm:</b> {Html(Default(value.StationCodes, "Không có"), 500)}",
            $"<b>Tên trạm:</b> {Html(Default(value.StationNames, "Không có"), 500)}",
            $"<b>Tổng tiền:</b> {amount} ₫",
            $"<b>Thời gian đặt:</b> {Html(Time(value.CreatedAt), 100)}");
    }

    public static string Zalo(CustomerOrderNotification value) =>
        $"""
        CO DON HANG MOI!
        ----------------------
        - Ma don: #{Plain(value.OrderId, 120)}
        - Khach hang: {Plain(Default(value.UserName, "Chưa cập nhật"), 200)}
        - So dien thoai: {Plain(Default(value.UserPhone, "Không có"), 40)}
        - Tong tien: {Amount(value.Total)} ₫
        - Thoi gian dat: {Time(value.CreatedAt)}
        ----------------------
        Vui lòng kiểm tra chi tiết trong bảng quản trị Admin.
        """;

    private static string Time(DateTimeOffset value) =>
        value.ToOffset(VietnamOffset).ToString("g", Vietnamese);

    private static string Amount(decimal value) =>
        value.ToString("#,0.################", Vietnamese);

    private static string Html(string value, int maximum) =>
        WebUtility.HtmlEncode(Plain(value, maximum));

    private static string Plain(string value, int maximum)
    {
        string normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized[..Math.Min(normalized.Length, maximum)];
    }

    private static string Default(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
