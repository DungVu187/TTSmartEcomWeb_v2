using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Application.Security;

public enum ControlPlaneAuthStatus
{
    Success,
    InvalidCredentials,
    UserNotFound,
    AccountInactive,
    AccountLocked,
}

public sealed record ControlPlaneAuthResult(
    ControlPlaneAuthStatus Status,
    ICurrentUserContext? UserContext,
    ControlPlaneUserRecord? UserRecord,
    string? Message)
{
    public static ControlPlaneAuthResult Succeeded(ICurrentUserContext context, ControlPlaneUserRecord user) =>
        new(ControlPlaneAuthStatus.Success, context, user, "Đăng nhập thành công");

    public static ControlPlaneAuthResult Failed(string message = "Thông tin đăng nhập không chính xác") =>
        new(ControlPlaneAuthStatus.InvalidCredentials, null, null, message);

    public static ControlPlaneAuthResult NotFound() =>
        new(ControlPlaneAuthStatus.UserNotFound, null, null, "Thông tin đăng nhập không chính xác");

    public static ControlPlaneAuthResult Inactive(string message = "Tài khoản chưa được kích hoạt hoặc đã bị vô hiệu hóa") =>
        new(ControlPlaneAuthStatus.AccountInactive, null, null, message);

    public static ControlPlaneAuthResult Locked(string message = "Tài khoản đã bị tạm khóa do nhập sai mật khẩu nhiều lần") =>
        new(ControlPlaneAuthStatus.AccountLocked, null, null, message);
}

public sealed class ControlPlaneAuthenticationService(
    IControlPlaneUserRepository userRepository,
    IControlPlaneIdentityReader identityReader,
    IPasswordHashCompatibilityVerifier passwordVerifier)
{
    public async Task<ControlPlaneAuthResult> AuthenticateAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            return ControlPlaneAuthResult.Failed("Thông tin đăng nhập không hợp lệ");
        }

        ControlPlaneUserRecord? user = await userRepository.FindByLoginAsync(identifier.Trim(), cancellationToken);
        if (user is null)
        {
            return ControlPlaneAuthResult.NotFound();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (user.LockedUntilUtc.HasValue && user.LockedUntilUtc.Value > now)
        {
            return ControlPlaneAuthResult.Locked();
        }

        if (user.Status != ControlPlaneUserStatus.Active)
        {
            return ControlPlaneAuthResult.Inactive();
        }

        bool isValidPassword;
        try
        {
            isValidPassword = passwordVerifier.Verify(password, user.PasswordHash);
        }
        catch
        {
            isValidPassword = false;
        }

        if (!isValidPassword)
        {
            // Record failed attempt (lock for 15 minutes after 5 failed attempts)
            await userRepository.RecordFailedLoginAsync(user.UserId, 5, TimeSpan.FromMinutes(15), cancellationToken);
            return ControlPlaneAuthResult.Failed("Thông tin đăng nhập không chính xác");
        }

        // Record successful login
        await userRepository.RecordSuccessfulLoginAsync(user.UserId, now, cancellationToken);

        // Resolve current user context
        ICurrentUserContext? context = await identityReader.FindContextByIdAsync(user.UserId, cancellationToken);
        if (context is null || !context.IsAuthenticated)
        {
            return ControlPlaneAuthResult.Inactive("Không thể tải thông tin phân quyền người dùng");
        }

        return ControlPlaneAuthResult.Succeeded(context, user);
    }
}
