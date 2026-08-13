using TTSmartEcom.Application.Stations;
using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Domain.Stations;

namespace TTSmartEcom.Application.Products;

public sealed record ProductViewer(
    string? Role,
    IReadOnlyCollection<string>? StationIds = null)
{
    public bool IsPrivileged => Role is "superadmin" or "admin" or "staff";
}

/// <summary>Resolves the legacy customer station union without leaking Mongo types.</summary>
public sealed class ProductAccessScopeService(IStationRepository stations)
{
    public async Task<IReadOnlySet<string>?> ResolveAllowedProductIdsAsync(
        ProductViewer? viewer,
        CancellationToken cancellationToken,
        string? requestedStationId = null)
    {
        if (viewer?.Role != "customer") return null;

        string[] stationIds = (viewer.StationIds ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (stationIds.Length == 0) return null;

        string requested = requestedStationId?.Trim() ?? string.Empty;
        if (requested.Length > 0 && requested != "Tất cả")
        {
            if (!stationIds.Contains(requested, StringComparer.Ordinal))
            {
                throw Error(403, "Bạn không có quyền truy cập trạm này.");
            }

            if (!IsObjectId(requested))
            {
                throw Error(400, "Mã trạm không hợp lệ.");
            }

            stationIds = [requested];
        }
        else
        {
            // Legacy bỏ qua station id không phải ObjectId khi hợp nhất các trạm được gán.
            stationIds = stationIds.Where(IsObjectId).ToArray();
            if (stationIds.Length == 0)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        IReadOnlyList<Station> assigned = await stations.FindByIdsAsync(
            stationIds,
            publicProjection: true,
            cancellationToken);
        return assigned
            .SelectMany(static station => station.ProductIds)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsObjectId(string value) =>
        value.Length == 24 && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static TTSmartEcom.Application.Common.Errors.ApplicationException Error(
        int status,
        string message) =>
        new(new ApplicationError($"TTS-PRODUCT-STATION-{status}", 4000 + status, status, message));
}
