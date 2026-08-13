namespace TTSmartEcom.Application.Abstractions.Authentication;

/// <summary>Loads the current account state used to validate a legacy JWT.</summary>
public interface IUserIdentityReader
{
    Task<UserIdentitySnapshot?> FindByIdAsync(string userId, CancellationToken cancellationToken);
}
