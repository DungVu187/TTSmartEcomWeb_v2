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
[Route("zalo")]
public sealed class ZaloController(
    ProviderSettingsService settings,
    ZaloOAuthService oauth,
    IOptions<ExternalServicesOptions> external,
    IWebHostEnvironment environment,
    ActivityLogWriteService activityLogs) : ControllerBase
{
    [HttpGet("settings")]
    [Authorize(Roles = "superadmin,admin")]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(new { success = true, data = await settings.GetZaloAsync(ct) });

    [HttpPost("settings")]
    [Authorize(Roles = "superadmin,admin")]
    public async Task<IActionResult> Update(ZaloSettingsRequest request, CancellationToken ct)
    {
        ZaloSettings data = await settings.UpdateZaloAsync(new ZaloSettingsInput(request.AppId, request.SecretKey, request.OaId, request.RecipientUserId), ct);
        if (ActorName() is { } actorName)
        {
            await activityLogs.TryAppendAsync(
                IntegrationActivityLogEntries.UpdateZaloSettings(actorName, data.AppId),
                ct);
        }
        return Ok(new { success = true, message = "Cập nhật cấu hình Zalo thành công", data });
    }

    [HttpGet("auth-url")]
    [Authorize(Roles = "superadmin,admin")]
    public async Task<IActionResult> AuthUrl(CancellationToken ct)
    {
        string? subject = User.FindFirst("userId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return Unauthorized(new { success = false, message = "Access denied, no token provided" });

        if (!TryRedirectUri(out string redirectUri)) return OAuthConfigurationUnavailable();
        ZaloOAuthAuthorizationResult result = await oauth.CreateAuthorizationUrlAsync(subject, redirectUri, ct);
        return result.Status switch
        {
            ZaloOAuthAuthorizationStatus.Success => Ok(new { success = true, authUrl = result.AuthorizationUrl }),
            ZaloOAuthAuthorizationStatus.MissingAppId => BadRequest(new { success = false, message = "Vui lòng nhập và lưu App ID trước khi liên kết." }),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                success = false,
                code = "TTS-ZALO-STATE-0503",
                message = "Liên kết Zalo OAuth chưa được cấu hình an toàn",
            }),
        };
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery(Name = "oa_id")] string? oaId,
        CancellationToken ct)
    {
        if (!TryRedirectUri(out string redirectUri) || !TryFrontendSuccessUrl(out string successUrl))
            return OAuthConfigurationUnavailable();
        ZaloOAuthCallbackResult result = await oauth.CompleteAsync(code, state, redirectUri, oaId, ct);
        return result.Status switch
        {
            ZaloOAuthCallbackStatus.Success => Redirect(successUrl),
            ZaloOAuthCallbackStatus.InvalidRequest => BadRequest("Không nhận được authorization code/state hợp lệ từ Zalo. Vui lòng thử lại."),
            ZaloOAuthCallbackStatus.InvalidState => BadRequest("State OAuth không hợp lệ, đã hết hạn hoặc đã được sử dụng."),
            ZaloOAuthCallbackStatus.StateUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, "Liên kết Zalo OAuth chưa được cấu hình an toàn."),
            ZaloOAuthCallbackStatus.MissingConfiguration => BadRequest("Thiếu thông tin App ID hoặc Secret Key. Cần lưu cấu hình trước."),
            ZaloOAuthCallbackStatus.ProviderRejected => BadRequest("Zalo từ chối yêu cầu liên kết OAuth."),
            ZaloOAuthCallbackStatus.InvalidProviderResponse => StatusCode(StatusCodes.Status502BadGateway, "Phản hồi OAuth từ Zalo không hợp lệ."),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, "Dịch vụ Zalo OAuth tạm thời không khả dụng."),
        };
    }

    private bool TryRedirectUri(out string redirectUri)
    {
        redirectUri = string.Empty;
        string? address = external.Value.PublicAddress?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(address))
        {
            if (!TryConfiguredBaseAddress(address, out string configured)) return false;
            redirectUri = $"{configured}/zalo/callback";
            return true;
        }

        if (!environment.IsDevelopment() && !environment.IsEnvironment("Test")) return false;
        if (!Request.Host.HasValue || Request.Host.Value.Length > 255) return false;
        string scheme = Request.IsHttps ? "https" : "http";
        redirectUri = $"{scheme}://{Request.Host}/zalo/callback";
        return Uri.TryCreate(redirectUri, UriKind.Absolute, out _);
    }

    private bool TryFrontendSuccessUrl(out string successUrl)
    {
        successUrl = string.Empty;
        string? frontend = external.Value.FrontendUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(frontend))
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Test")) return false;
            frontend = "http://localhost:5173";
        }
        else if (!TryConfiguredBaseAddress(frontend, out frontend))
        {
            return false;
        }

        successUrl = $"{frontend}/admin/zalo?link=success";
        return true;
    }

    private bool TryConfiguredBaseAddress(string value, out string normalized)
    {
        normalized = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            (!environment.IsDevelopment() && uri.Scheme != Uri.UriSchemeHttps) ||
            uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;
        normalized = value.TrimEnd('/');
        return true;
    }

    private ObjectResult OAuthConfigurationUnavailable() => StatusCode(
        StatusCodes.Status503ServiceUnavailable,
        new { success = false, code = "TTS-ZALO-CONFIG-0503", message = "Liên kết Zalo OAuth chưa được cấu hình an toàn" });

    private string? ActorName()
    {
        string? name = (HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot)?.Name;
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
