using System.Text.Json.Serialization;

namespace TTSmartEcom.Domain.Stations;

public sealed record Station(
    string Id,
    string? StationName,
    [property: JsonPropertyName("imgUrl")] string? ImageUrl,
    string? StationCode,
    bool AllowPublicSignup,
    string? Location,
    [property: JsonPropertyName("productId")] IReadOnlyList<string> ProductIds);

public sealed record StationPage(long Total, int Page, int Limit, IReadOnlyList<Station> Stations);
