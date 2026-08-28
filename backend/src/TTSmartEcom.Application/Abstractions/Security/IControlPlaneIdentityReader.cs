using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Application.Abstractions.Security;

public interface IControlPlaneIdentityReader
{
    Task<ICurrentUserContext?> FindContextByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<ICurrentUserContext?> FindContextByLoginAsync(string identifier, CancellationToken cancellationToken);
}
