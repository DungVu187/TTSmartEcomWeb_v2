using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Contracts.Users.Requests;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Users;
using TTSmartEcom.Application.Users;
using TTSmartEcom.Application.Security;
using TTSmartEcom.Domain.Security;
using TTSmartEcom.Domain.Stations;
using TTSmartEcom.Domain.Users;
using TTSmartEcom.Application.Stations;

namespace TTSmartEcom.Api.Controllers.Users;

[ApiController]
[Route("users")]
public sealed partial class UserSessionController(
    UserAuthenticationService authentication,
    ControlPlaneAuthenticationService controlPlaneAuth,
    IUserProfileRepository profiles,
    ISuperAdminMutationGuard superAdminGuard,
    IUserRepository users,
    IPasswordHashWriter passwordHasher,
    IStationRepository stations,
    IPasswordHashCompatibilityVerifier passwordVerifier,
    UserPasswordRecoveryService passwordRecovery,
    IOptions<JwtOptions> jwtOptions,
    IOptions<LegacyCompatibilityOptions> compatibility,
    ILogger<UserSessionController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        string? identifier = request.ResolveIdentifier();
        if (identifier is null)
        {
            return BadRequest(new { message = "Vui lòng nhập số điện thoại hoặc email" });
        }

        if (identifier.Length > 160 || string.IsNullOrEmpty(request.Password) || request.Password.Length > 256)
        {
            return BadRequest(new { message = "Thông tin đăng nhập không hợp lệ" });
        }

        // 1. Try Control Plane first
        ControlPlaneAuthResult ctrlResult = await controlPlaneAuth.AuthenticateAsync(identifier, request.Password, cancellationToken);
        if (ctrlResult.Status == ControlPlaneAuthStatus.Success && ctrlResult.UserContext is not null)
        {
            IssueSessionCookie(ctrlResult.UserContext);
            return Ok(new { message = "Đăng nhập thành công" });
        }
        if (ctrlResult.Status == ControlPlaneAuthStatus.AccountLocked)
        {
            return BadRequest(new { message = ctrlResult.Message ?? "Tài khoản đã bị tạm khóa" });
        }
        if (ctrlResult.Status == ControlPlaneAuthStatus.AccountInactive)
        {
            return BadRequest(new { message = ctrlResult.Message ?? "Tài khoản chưa được kích hoạt hoặc đã bị vô hiệu hóa" });
        }

        // Only an identifier absent from Control Plane may fall back to the legacy
        // customer store. A rejected internal identity must never be authenticated
        // against an operational account with the same login identifier.
        if (ctrlResult.Status != ControlPlaneAuthStatus.UserNotFound)
        {
            LogLoginFailure(logger);
            return BadRequest(new { message = "Thông tin đăng nhập không hợp lệ" });
        }

        // 2. Fallback to operational database for customer
        UserRecord? user = await authentication.AuthenticateAsync(identifier, request.Password, cancellationToken);
        if (user is null)
        {
            LogLoginFailure(logger);
            return BadRequest(new { message = "Thông tin đăng nhập không hợp lệ" });
        }

        if (!string.IsNullOrWhiteSpace(request.InviteCode))
        {
            string code = request.InviteCode.Trim();
            Station? station = await stations.FindByCodeAsync(code[..Math.Min(code.Length, 100)], false, cancellationToken);
            if (station is not null)
            {
                await profiles.AddStationAsync(user.Id, user.Role, station.Id, cancellationToken);
            }
        }

        IssueSessionCookie(user);
        return Ok(new { message = "Đăng nhập thành công" });
    }

    [HttpPost("admin/login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> AdminLoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        string? identifier = request.Phone?.Trim() ?? request.ResolveIdentifier();
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 160 || string.IsNullOrEmpty(request.Password) || request.Password.Length > 256)
        {
            return BadRequest(new { message = "Thông tin đăng nhập không hợp lệ" });
        }

        // 1. Try Control Plane for internal user
        ControlPlaneAuthResult ctrlResult = await controlPlaneAuth.AuthenticateAsync(identifier, request.Password, cancellationToken);
        if (ctrlResult.Status == ControlPlaneAuthStatus.Success && ctrlResult.UserContext is not null)
        {
            IssueSessionCookie(ctrlResult.UserContext);
            return Ok(new { message = "Đăng nhập admin thành công" });
        }
        if (ctrlResult.Status == ControlPlaneAuthStatus.AccountLocked)
        {
            return BadRequest(new { message = ctrlResult.Message ?? "Tài khoản đã bị tạm khóa" });
        }
        if (ctrlResult.Status == ControlPlaneAuthStatus.AccountInactive)
        {
            return BadRequest(new { message = ctrlResult.Message ?? "Tài khoản chưa được kích hoạt hoặc đã bị vô hiệu hóa" });
        }

        // Only legacy identifiers that are absent from Control Plane can use the
        // compatibility path. This prevents bypassing a Control Plane password,
        // lockout or inactive status through a duplicate operational account.
        if (ctrlResult.Status != ControlPlaneAuthStatus.UserNotFound)
        {
            LogLoginFailure(logger);
            return BadRequest(new { message = "Thông tin đăng nhập không hợp lệ" });
        }

        // 2. Fallback to operational database for legacy admin
        UserRecord? user = await authentication.AuthenticateAsync(identifier, request.Password, cancellationToken);
        if (user is null)
        {
            LogLoginFailure(logger);
            return BadRequest(new { message = "Thông tin đăng nhập không hợp lệ" });
        }

        if (user.Role is not (SystemRoles.SuperAdmin or SystemRoles.Admin or SystemRoles.Staff))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Truy cập bị từ chối. Chỉ dành cho admin hoặc nhân viên" });
        }

        IssueSessionCookie(user);
        return Ok(new { message = "Đăng nhập admin thành công" });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("authToken", CookieOptions(null));
        return Ok(new { message = "Logout successful" });
    }

    [HttpPut("change-password")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        UserIdentitySnapshot? identity = HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
        if (identity is null)
        {
            return Unauthorized(new { message = "Access denied, no token provided" });
        }

        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 6 || request.NewPassword.Length > 256)
        {
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự" });
        }

        if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 256)
        {
            return BadRequest(new { message = "Mật khẩu hiện tại không đúng" });
        }

        UserPasswordRecord? password = await profiles.FindPasswordAsync(identity.Id, cancellationToken);
        if (password is null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }

        bool verified;
        try
        {
            verified = passwordVerifier.Verify(request.CurrentPassword, password.PasswordHash);
        }
        catch (ArgumentException)
        {
            verified = false;
        }

        if (!verified)
        {
            return BadRequest(new { message = "Mật khẩu hiện tại không đúng" });
        }

        DateTimeOffset changedAt = DateTimeOffset.UtcNow;
        string newHash = passwordVerifier.Hash(request.NewPassword);
        string loginToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        bool updated = await profiles.ReplacePasswordAsync(identity.Id, newHash, loginToken, changedAt, cancellationToken);
        return updated
            ? Ok(new { message = "Đổi mật khẩu thành công" })
            : NotFound(new { message = "Không tìm thấy người dùng" });
    }

    [HttpGet("permission-catalog")]
    [Authorize(Roles = $"{SystemRoles.SuperAdmin},{SystemRoles.Admin}")]
    public IActionResult PermissionCatalog() => Ok(new
    {
        success = true,
        catalog = LegacyPermissionCatalog.Catalog,
        adminFixed = LegacyPermissionCatalog.AdminFixed,
    });

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        UserIdentitySnapshot? actor = HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
        if (!compatibility.Value.PublicSignupEnabled && actor is null)
        {
            return Unauthorized(new { message = "Access denied, no token provided" });
        }
        if (!compatibility.Value.PublicSignupEnabled && actor is not null && actor.Role is not (SystemRoles.SuperAdmin or SystemRoles.Admin or SystemRoles.Staff))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Truy cập bị từ chối. Chỉ dành cho admin hoặc nhân viên" });
        }

        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6 || request.Password.Length > 256)
        {
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự" });
        }

        string phone = CanonicalPhone(request.Phone);
        if (!System.Text.RegularExpressions.Regex.IsMatch(phone, "^0\\d{9,10}$"))
        {
            return BadRequest(new { message = "Số điện thoại không hợp lệ. Vui lòng nhập số điện thoại Việt Nam gồm 10-11 chữ số, bắt đầu bằng 0." });
        }

        string? email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        if (email is not null && (email.Length > 254 || !email.Contains('@', StringComparison.Ordinal)))
        {
            return BadRequest(new { message = "Email không hợp lệ" });
        }

        if (await users.FindByLoginAsync(phone, cancellationToken) is not null ||
            (email is not null && await users.FindByLoginAsync(email, cancellationToken) is not null))
        {
            return BadRequest(new { message = "Email hoặc số điện thoại đã tồn tại" });
        }

        string role = SystemRoles.Customer;
        IReadOnlyList<string> permissions = [];
        if (!compatibility.Value.PublicSignupEnabled)
        {
            UserIdentitySnapshot privateActor = actor!;
            role = request.Role ?? SystemRoles.Customer;
            if (!SystemRoles.All.Contains(role)) return BadRequest(new { message = "Vai trò không hợp lệ" });
            if (!UserAdministrationPolicy.CanCreateRole(privateActor, role))
            {
                if (privateActor.Role == SystemRoles.Staff && role == SystemRoles.Customer)
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Access denied, missing permission: customer.create" });
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = privateActor.Role == SystemRoles.Admin
                        ? "Admin chỉ được phép tạo tài khoản Staff hoặc Customer"
                        : "Nhân viên chỉ được tạo tài khoản khách hàng",
                });
            }
            if (role is SystemRoles.Admin or SystemRoles.Staff && request.Permissions is not null)
            {
                PermissionValidationResult validation = UserAdministrationPolicy.ValidateGrantablePermissions(request.Permissions);
                if (!validation.IsValid) return BadRequest(new { message = validation.Message });
                permissions = validation.Permissions;
            }
        }

        await using IAsyncDisposable? superAdminHandle = role == SystemRoles.SuperAdmin
            ? await superAdminGuard.TryAcquireAsync(cancellationToken)
            : null;
        if (role == SystemRoles.SuperAdmin && superAdminHandle is null)
            return Conflict(new { message = "Một thao tác Super Admin khác đang được xử lý, vui lòng thử lại sau." });
        if (role == SystemRoles.SuperAdmin &&
            await profiles.HasOtherUserWithRoleAsync(SystemRoles.SuperAdmin, null, cancellationToken))
            return BadRequest(new { message = "Hệ thống chỉ được phép có duy nhất 1 tài khoản Super Admin" });

        List<string> assignedStations = [];
        string? stationCode = request.InviteCode ?? request.StationCode;
        if (!string.IsNullOrWhiteSpace(stationCode))
        {
            Station? station = await stations.FindByCodeAsync(stationCode.Trim()[..Math.Min(stationCode.Trim().Length, 100)], false, cancellationToken);
            if (station is null) return BadRequest(new { message = "Mã link trạm không hợp lệ" });
            if (compatibility.Value.PublicSignupEnabled && !station.AllowPublicSignup)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Trạm hiện không cho phép đăng ký công khai" });
            if (compatibility.Value.PublicSignupEnabled || actor?.Role == SystemRoles.Admin)
                assignedStations.Add(station.Id);
        }

        string loginToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        UserSummary? user = await profiles.CreateUserAsync(new NewUserData(email, phone, request.Name, passwordHasher.Hash(request.Password), role, permissions, loginToken, assignedStations), cancellationToken);
        return user is null ? StatusCode(500, new { message = "Lỗi server" }) : StatusCode(StatusCodes.Status201Created, new { message = "User created successfully", logInString = loginToken, user });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        string? identifier = request.ResolveIdentifier();
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return BadRequest(new { message = "Vui lòng cung cấp số điện thoại hoặc email" });
        }

        if (identifier.Length > 160)
        {
            return BadRequest(new { message = "Thông tin khôi phục mật khẩu không hợp lệ" });
        }

        PasswordResetRequestResult result = await passwordRecovery.RequestResetAsync(
            identifier,
            cancellationToken);
        return result.Status switch
        {
            PasswordResetRequestStatus.Success => Ok(new
            {
                message = $"Mã OTP đã được gửi về email {result.MaskedEmail}",
                phone = result.Phone,
            }),
            PasswordResetRequestStatus.UserNotFound => NotFound(new
            {
                message = "Không tìm thấy tài khoản với thông tin đã cung cấp",
            }),
            PasswordResetRequestStatus.EmailMissing => BadRequest(new
            {
                message = "Tài khoản của bạn chưa được cập nhật email liên kết. Vui lòng liên hệ Admin để được hỗ trợ.",
            }),
            PasswordResetRequestStatus.ProviderUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    success = false,
                    code = "TTS-USERS-EMAIL-0503",
                    message = "Dịch vụ gửi email khôi phục mật khẩu hiện không khả dụng",
                }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi server" }),
        };
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        string? identifier = request.ResolveIdentifier();
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(request.Otp) || string.IsNullOrEmpty(request.NewPassword))
        {
            return BadRequest(new { message = "Vui lòng nhập đầy đủ thông tin yêu cầu" });
        }

        if (identifier.Length > 160 || request.Otp.Length > 32 || request.NewPassword.Length > 256)
        {
            return BadRequest(new { message = "Thông tin khôi phục mật khẩu không hợp lệ" });
        }

        if (request.NewPassword.Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự" });
        }

        PasswordResetResult result = await passwordRecovery.ResetAsync(
            identifier,
            request.Otp,
            request.NewPassword,
            cancellationToken);
        return result.Status switch
        {
            PasswordResetStatus.Success => Ok(new
            {
                message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.",
            }),
            PasswordResetStatus.UserNotFound => NotFound(new { message = "Không tìm thấy tài khoản" }),
            _ => BadRequest(new { message = "Mã OTP không chính xác hoặc đã hết hạn" }),
        };
    }

    [HttpPost("autologin")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> AutologinAsync(AutoLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { message = "Thiếu mã đăng nhập tự động" });
        }

        if (request.Token.Length > 512)
        {
            return BadRequest(new { message = "Mã đăng nhập tự động không hợp lệ" });
        }

        UserRecord? user = await authentication.AuthenticateWithAutologinTokenAsync(request.Token, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { message = "Mã đăng nhập tự động không hợp lệ hoặc đã hết hạn" });
        }

        IssueSessionCookie(user);
        return Ok(new { message = "Đăng nhập tự động thành công" });
    }

    private void IssueSessionCookie(UserRecord user)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<Claim> claims =
        [
            new("userId", user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new("phone", user.Phone),
            new("role", user.Role),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        ];
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Secret)),
            SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddHours(jwtOptions.Value.SessionHours).UtcDateTime,
            signingCredentials: credentials);
        Response.Cookies.Append("authToken", new JwtSecurityTokenHandler().WriteToken(token),
            CookieOptions(TimeSpan.FromHours(jwtOptions.Value.SessionHours)));
    }

    private void IssueSessionCookie(ICurrentUserContext user)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string userIdStr = user.UserId.ToString()!;
        // Existing endpoints still consume the legacy role claim. Non-platform
        // Control Plane users are projected as staff; their actual authority is
        // evaluated from the request-scoped Company/Branch permission context.
        string role = user.IsPlatformSuperAdmin ? SystemRoles.SuperAdmin : SystemRoles.Staff;
        List<Claim> claims =
        [
            new("userId", userIdStr),
            new(ClaimTypes.NameIdentifier, userIdStr),
            new("phone", user.Phone ?? string.Empty),
            new("role", role),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        ];

        if (user.ActiveCompanyId.HasValue)
        {
            claims.Add(new("companyId", user.ActiveCompanyId.Value.ToString()));
        }

        if (user.ActiveBranchId.HasValue)
        {
            claims.Add(new("branchId", user.ActiveBranchId.Value.ToString()));
        }

        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Secret)),
            SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddHours(jwtOptions.Value.SessionHours).UtcDateTime,
            signingCredentials: credentials);
        Response.Cookies.Append("authToken", new JwtSecurityTokenHandler().WriteToken(token),
            CookieOptions(TimeSpan.FromHours(jwtOptions.Value.SessionHours)));
    }

    private static string CanonicalPhone(string value) {
        string normalized = new(value.Where(static ch => !char.IsWhiteSpace(ch) && ch is not '.' and not '-' and not '(' and not ')').ToArray());
        if (normalized.StartsWith("+84", StringComparison.Ordinal)) return "0" + normalized[3..];
        if (normalized.StartsWith("84", StringComparison.Ordinal) && normalized.Length is >= 11 and <= 12) return "0" + normalized[2..];
        return normalized;
    }

    private CookieOptions CookieOptions(TimeSpan? maxAge) => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        MaxAge = maxAge,
        Path = "/",
    };

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Legacy login failed")]
    private static partial void LogLoginFailure(ILogger logger);
}

internal static class LegacyPermissionCatalog
{
    public static IReadOnlyList<object> Catalog { get; } =
    [
        Module("product", "Sản phẩm", "products", "grantable", Action("product.view", "Xem"), Action("product.create", "Thêm"), Action("product.edit", "Sửa"), Action("product.delete", "Xóa")),
        Module("order", "Đơn bán hàng", "orders", "grantable", Action("order.view", "Xem"), Action("order.create", "Thêm"), Action("order.edit", "Sửa"), Action("order.delete", "Xóa"), Action("order.excel", "Excel (mẫu/nhập/xuất)", "order.edit"), Action("order.scan_ai", "Quét hóa đơn AI", "order.edit")),
        Module("iporder", "Đơn nhập hàng", "orders", "grantable", Action("iporder.view", "Xem"), Action("iporder.create", "Thêm"), Action("iporder.edit", "Sửa"), Action("iporder.delete", "Xóa"), Action("iporder.excel", "Excel (nhập/xuất)", "iporder.edit"), Action("iporder.scan_ai", "Quét hóa đơn AI", "iporder.edit")),
        Module("eporder", "Đơn xuất hàng", "orders", "grantable", Action("eporder.view", "Xem"), Action("eporder.create", "Thêm"), Action("eporder.edit", "Sửa"), Action("eporder.delete", "Xóa"), Action("eporder.excel", "Excel (nhập/xuất)", "eporder.edit"), Action("eporder.scan_ai", "Quét hóa đơn AI", "eporder.edit")),
        Module("station", "Trạm", "stations", "grantable", Action("station.view", "Xem"), Action("station.create", "Thêm"), Action("station.edit", "Sửa"), Action("station.delete", "Xóa")),
        Module("customer", "Khách hàng", "stations", "grantable", Action("customer.view", "Xem"), Action("customer.create", "Thêm"), Action("customer.edit", "Sửa"), Action("customer.delete", "Xóa"), Action("customer.assign_station", "Gán trạm")),
        Module("storefront", "Giao diện ngoài (banner + hiển thị sản phẩm)", "storefront", "grantable", Action("storefront.manage", "Quản lý")),
        Module("history_import", "Lịch sử nhập kho", "system", "grantable", Action("history_import.view", "Xem")),
        Module("history_export", "Lịch sử xuất kho", "system", "grantable", Action("history_export.view", "Xem")),
        Module("voice", "Từ vựng Voice", "system", "grantable", Action("voice.manage", "Quản lý")),
        Module("account", "Phân quyền", "admin", "adminFixed", Action("account.manage", "Quản lý")),
        Module("zalo", "Cấu hình Zalo", "admin", "adminFixed", Action("zalo.manage", "Quản lý")),
        Module("activitylog", "Lịch sử hoạt động", "admin", "grantable", Action("activitylog.view", "Xem")),
    ];

    public static IReadOnlyList<string> AdminFixed { get; } = ["account.manage", "zalo.manage"];

    private static object Module(string key, string label, string group, string scope, params object[] actions) =>
        new { key, label, group, scope, actions };

    private static object Action(string key, string label, string? dependsOn = null) =>
        dependsOn is null ? new { key, label } : new { key, label, dependsOn };
}
