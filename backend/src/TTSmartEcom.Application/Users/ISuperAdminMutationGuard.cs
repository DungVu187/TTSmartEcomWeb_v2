namespace TTSmartEcom.Application.Users;

/// <summary>
/// Serializes mutations that can create or promote a super-administrator across
/// every API process. A null handle means another mutation owns the guard.
/// </summary>
public interface ISuperAdminMutationGuard
{
    Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken);
}
