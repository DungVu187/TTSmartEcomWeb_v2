using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Contracts.Integrations;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Application.Integrations;
using TTSmartEcom.Domain.Integrations;

namespace TTSmartEcom.Api.Controllers.Integrations;

[ApiController]
[Route("telegram")]
[Authorize(Roles = "superadmin,admin")]
public sealed class TelegramController(
    ProviderSettingsService settings,
    ITelegramMessageSender messages,
    IOptions<ExternalServicesOptions> external,
    ActivityLogWriteService activityLogs) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        TelegramSettings value = await settings.GetTelegramAsync(ct);
        return Ok(new { success = true, data = new { value.Enabled, value.Recipients, botConfigured = !string.IsNullOrWhiteSpace(external.Value.TelegramBotToken) } });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> Update(TelegramEnabledRequest request, CancellationToken ct)
    {
        if (!request.Enabled.HasValue) return BadRequest(new { success = false, message = "Trạng thái bật/tắt không hợp lệ" });
        TelegramSettings value = await settings.SetTelegramEnabledAsync(request.Enabled.Value, ct);
        if (ActorName() is { } actorName)
        {
            await activityLogs.TryAppendAsync(
                IntegrationActivityLogEntries.UpdateTelegramSettings(actorName, value.Enabled),
                ct);
        }
        return Ok(new { success = true, data = new { value.Enabled } });
    }

    [HttpPost("recipients")]
    public async Task<IActionResult> Add(TelegramRecipientRequest request, CancellationToken ct)
    {
        TelegramRecipient recipient = await settings.AddRecipientAsync(ToInput(request), ct);
        if (ActorName() is { } actorName)
        {
            await activityLogs.TryAppendAsync(
                IntegrationActivityLogEntries.CreateTelegramRecipient(actorName, recipient.Label),
                ct);
        }
        return StatusCode(201, new { success = true, data = recipient });
    }

    [HttpPut("recipients/{recipientId}")]
    public async Task<IActionResult> UpdateRecipient(
        string recipientId,
        TelegramRecipientRequest request,
        CancellationToken ct)
    {
        TelegramRecipient? recipient = await settings.UpdateRecipientAsync(
            recipientId,
            ToInput(request),
            ct);
        if (recipient is null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy người nhận Telegram" });
        }
        if (ActorName() is { } actorName)
        {
            await activityLogs.TryAppendAsync(
                IntegrationActivityLogEntries.UpdateTelegramRecipient(actorName, recipient.Label),
                ct);
        }
        return Ok(new { success = true, data = recipient });
    }

    [HttpDelete("recipients/{recipientId}")]
    public async Task<IActionResult> Delete(string recipientId, CancellationToken ct)
    {
        TelegramSettings before = await settings.GetTelegramAsync(ct);
        string? label = before.Recipients.FirstOrDefault(recipient =>
            string.Equals(recipient.Id, recipientId, StringComparison.Ordinal))?.Label;
        bool deleted = await settings.DeleteRecipientAsync(recipientId, ct);
        if (!deleted)
        {
            return NotFound(new { success = false, message = "Không tìm thấy người nhận Telegram" });
        }
        if (ActorName() is { } actorName)
        {
            await activityLogs.TryAppendAsync(
                IntegrationActivityLogEntries.DeleteTelegramRecipient(actorName, label),
                ct);
        }
        return Ok(new { success = true });
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test(TelegramTestRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(external.Value.TelegramBotToken))
            return BadRequest(new { success = false, message = "Chưa cấu hình TELEGRAM_BOT_TOKEN trên máy chủ" });
        TelegramSettings config = await settings.GetTelegramAsync(ct);
        string[] chatIds = string.IsNullOrWhiteSpace(request.ChatId)
            ? config.Recipients.Where(recipient => recipient.Enabled).Select(recipient => recipient.ChatId).ToArray()
            : [request.ChatId.Trim()];
        const string text = "<b>Kiểm tra thông báo Telegram</b>\nHệ thống đã kết nối thành công.";
        bool[] results = await Task.WhenAll(chatIds.Select(chatId => messages.SendAsync(chatId, text, ct)));
        int sent = results.Count(result => result);
        int failed = results.Length - sent;
        return Ok(new { success = failed == 0, sent, failed });
    }

    private static TelegramRecipientInput ToInput(TelegramRecipientRequest request) => new(request.Label, request.ChatId, request.Type, request.Enabled, request.NotifyTypes);

    private string? ActorName()
    {
        string? name = (HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot)?.Name;
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
