using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Application.Voice;
using TTSmartEcom.Domain.Voice;

namespace TTSmartEcom.Api.Controllers.Voice;

[ApiController]
[Route("voice-vocabs")]
[PermissionAuthorize("voice.manage")]
public sealed class VoiceVocabularyController(
    VoiceVocabularyService vocabulary,
    ActivityLogWriteService activityLogs) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(new { success = true, data = await vocabulary.GetAsync(ct) });

    [HttpPost("{group}")]
    public async Task<IActionResult> Create(
        string group,
        VoiceVocabularyMutation request,
        CancellationToken ct)
    {
        VoiceVocabulary data = await vocabulary.CreateAsync(group, request, ct);
        if (ActorName() is { } actorName)
        {
            await activityLogs.TryAppendAsync(
                IntegrationActivityLogEntries.CreateVoiceVocabulary(actorName, group, request),
                ct);
        }
        return StatusCode(
            StatusCodes.Status201Created,
            new { success = true, message = "Thêm thành công.", data });
    }

    [HttpPut("{group}")]
    public async Task<IActionResult> Update(
        string group,
        VoiceVocabularyMutation request,
        CancellationToken ct)
    {
        VoiceVocabulary data = await vocabulary.UpdateAsync(group, request, ct);
        if (ActorName() is { } actorName)
        {
            await activityLogs.TryAppendAsync(
                IntegrationActivityLogEntries.UpdateVoiceVocabulary(actorName, group, request),
                ct);
        }
        return Ok(new { success = true, message = "Cập nhật thành công.", data });
    }

    [HttpDelete("{group}")]
    public async Task<IActionResult> Delete(
        string group,
        VoiceVocabularyMutation request,
        CancellationToken ct)
    {
        VoiceVocabulary data = await vocabulary.DeleteAsync(group, request, ct);
        if (ActorName() is { } actorName)
        {
            await activityLogs.TryAppendAsync(
                IntegrationActivityLogEntries.DeleteVoiceVocabulary(actorName, group, request),
                ct);
        }
        return Ok(new { success = true, message = "Xóa thành công.", data });
    }

    private string? ActorName()
    {
        string? name = (HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot)?.Name;
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
