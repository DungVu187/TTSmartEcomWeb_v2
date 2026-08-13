namespace TTSmartEcom.Api.Contracts.Stations.Requests;

public sealed record CreateStationRequest(string? StationName, string? StationCode, string? Location, bool? AllowPublicSignup);
public sealed record UpdateStationRequest(string? StationName, string? StationCode, string? Location, bool? AllowPublicSignup);
public sealed record UpdateStationProductsRequest(IReadOnlyList<string>? ProductId);
