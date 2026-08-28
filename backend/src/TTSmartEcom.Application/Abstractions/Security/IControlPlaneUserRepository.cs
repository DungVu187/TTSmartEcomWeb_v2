using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Application.Abstractions.Security;

public interface IControlPlaneUserRepository
{
    Task<ControlPlaneUserRecord?> FindByLoginAsync(string identifier, CancellationToken cancellationToken);

    Task<ControlPlaneUserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> RecordSuccessfulLoginAsync(Guid userId, DateTimeOffset loginTime, CancellationToken cancellationToken);

    Task<bool> RecordFailedLoginAsync(Guid userId, int maxFailedAttempts, TimeSpan lockoutDuration, CancellationToken cancellationToken);

    Task<IReadOnlyList<ControlPlaneCompanyMembership>> GetCompanyMembershipsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ControlPlaneBranchMembership>> GetBranchMembershipsAsync(Guid userId, CancellationToken cancellationToken);
}
