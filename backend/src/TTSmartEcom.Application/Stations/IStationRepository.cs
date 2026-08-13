using TTSmartEcom.Domain.Stations;

namespace TTSmartEcom.Application.Stations;

public interface IStationRepository
{
    Task<StationPage> ListAsync(int page, int limit, string? search, CancellationToken cancellationToken);
    Task<IReadOnlyList<Station>> SearchExactAsync(
        string? name,
        string? code,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    Task<Station?> FindByIdAsync(string id, CancellationToken cancellationToken);
    Task<Station?> FindByCodeAsync(string code, bool publicProjection, CancellationToken cancellationToken);
    Task<IReadOnlyList<Station>> FindByCodesAsync(IReadOnlyList<string> codes, CancellationToken cancellationToken);
    Task<IReadOnlyList<Station>> FindByIdsAsync(IReadOnlyList<string> ids, bool publicProjection, CancellationToken cancellationToken);
    Task<Station?> CreateAsync(NewStationData station, CancellationToken cancellationToken);
    Task<Station?> UpdateAsync(string id, UpdateStationData station, CancellationToken cancellationToken);
    Task<Station?> UpdateProductsAsync(string id, IReadOnlyList<string> productIds, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
    Task<Station?> UpdateImageAsync(string id, string imageUrl, CancellationToken cancellationToken);
    Task<Station?> RemoveImageAsync(string id, CancellationToken cancellationToken);
}

public sealed record NewStationData(string StationName, string StationCode, string? Location, bool AllowPublicSignup);
public sealed record UpdateStationData(string? StationName, string? StationCode, string? Location, bool? AllowPublicSignup);
