using TTSmartEcom.Domain.Users;

namespace TTSmartEcom.Application.Users;

public interface IUserProfileRepository
{
    Task<UserProfile?> FindProfileAsync(string userId, CancellationToken cancellationToken);
    Task<UserProfile?> UpdateProfileAsync(string userId, string? name, string? email, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAddress>?> AddAddressAsync(string userId, UserAddress address, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAddress>?> UpdateAddressAsync(string userId, string addressId, UserAddressPatch patch, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAddress>?> DeleteAddressAsync(string userId, string addressId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAddress>?> SetDefaultAddressAsync(string userId, string addressId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserOrderTemplate>?> GetOrderTemplatesAsync(string userId, CancellationToken cancellationToken);
    Task<UserOrderTemplate?> AddOrderTemplateAsync(string userId, string? displayName, IReadOnlyList<UserTemplateProduct> products, CancellationToken cancellationToken);
    Task<UserOrderTemplate?> UpdateOrderTemplateAsync(string userId, int index, string? displayName, IReadOnlyList<UserTemplateProduct>? products, CancellationToken cancellationToken);
    Task<bool> DeleteOrderTemplateAsync(string userId, int index, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserSummary>> ListUsersAsync(string viewerRole, bool customersOnly, CancellationToken cancellationToken);
    Task<UserSummary?> FindUserSummaryAsync(string userId, CancellationToken cancellationToken);
    Task<UserSummary?> FindUserSummaryByPhoneAsync(string phone, CancellationToken cancellationToken);
    Task<bool> HasOtherUserWithRoleAsync(string role, string? excludingUserId, CancellationToken cancellationToken);
    Task<UserSummary?> CreateUserAsync(NewUserData user, CancellationToken cancellationToken);
    Task<UserSummary?> UpdateUserAsync(string userId, string expectedRole, UserUpdateData update, CancellationToken cancellationToken);
    Task<UserSummary?> UpdatePermissionsAsync(string userId, string expectedRole, UserPermissionUpdate update, CancellationToken cancellationToken);
    Task<string?> RotateAutologinTokenAsync(string userId, string expectedRole, CancellationToken cancellationToken);
    Task<UserSummary?> AddStationAsync(string userId, string expectedRole, string stationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>?> ReplaceStationsByPhoneAsync(string phone, string expectedRole, IReadOnlyList<string> stations, CancellationToken cancellationToken);
    Task<bool> DeleteUserAsync(string userId, string expectedRole, CancellationToken cancellationToken);
    Task<UserPasswordRecord?> FindPasswordAsync(string userId, CancellationToken cancellationToken);
    Task<bool> ReplacePasswordAsync(string userId, string passwordHash, string loginToken,
        DateTimeOffset passwordChangedAt, CancellationToken cancellationToken);
}

public sealed record UserAddressPatch(string? Label, string? ReceiverName, string? ReceiverPhone, string? AddressDetail);
public sealed record NewUserData(string? Email, string Phone, string? Name, string PasswordHash, string Role,
    IReadOnlyList<string> Permissions, string LoginToken, IReadOnlyList<string>? Stations = null);
public sealed record UserUpdateData(string? Name, string? Email, string? Phone);
public sealed record UserPermissionUpdate(string? Role, IReadOnlyList<string>? Permissions, string? Name,
    string? Email, string? Phone, string? PasswordHash, string? LoginToken, DateTimeOffset? PasswordChangedAt);
public sealed record UserPasswordRecord(string Id, string PasswordHash);
