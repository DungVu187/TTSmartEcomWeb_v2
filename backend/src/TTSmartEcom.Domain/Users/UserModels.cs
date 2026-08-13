using System.Text.Json.Serialization;

namespace TTSmartEcom.Domain.Users;

public sealed record UserProfile(
    [property: JsonPropertyName("_id")] string Id,
    string? Email,
    string Phone,
    string? Name,
    string Role,
    IReadOnlyList<string> Functions,
    IReadOnlyList<string> Permissions,
    [property: JsonPropertyName("station")] IReadOnlyList<string> Stations,
    IReadOnlyList<UserAddress> Addresses,
    [property: JsonPropertyName("orderTemplate")] IReadOnlyList<UserOrderTemplate> OrderTemplates);

public sealed record UserAddress(
    [property: JsonPropertyName("_id")] string Id,
    string? Label,
    string? ReceiverName,
    string? ReceiverPhone,
    string? AddressDetail,
    bool IsDefault);

public sealed record UserOrderTemplate(
    [property: JsonPropertyName("_id")] string Id,
    string? DisplayName,
    string? Note,
    IReadOnlyList<UserTemplateProduct> Products);

public sealed record UserTemplateProduct(string? ProductId, double Quantity);

public sealed record UserSummary(
    [property: JsonPropertyName("_id")] string Id,
    string? Email,
    string Phone,
    string? Name,
    string Role,
    IReadOnlyList<string> Functions,
    IReadOnlyList<string> Permissions,
    [property: JsonPropertyName("station")] IReadOnlyList<string> Stations,
    IReadOnlyList<UserAddress> Addresses,
    [property: JsonPropertyName("orderTemplate")] IReadOnlyList<UserOrderTemplate> OrderTemplates);

public sealed record UserPage(long Total, int Page, int Limit, IReadOnlyList<UserSummary> Users);
