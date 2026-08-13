namespace TTSmartEcom.Api.Contracts.Stations;

public sealed record StationRequest(string? StationName, string? StationCode, string? Location, bool? AllowPublicSignup);
public sealed record StationProductsRequest(IReadOnlyList<string>? ProductId);
public sealed record StationIdsRequest(IReadOnlyList<string>? Ids);
