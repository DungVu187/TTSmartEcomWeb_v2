using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.Application.Products;

public sealed class ProductCatalogReadService(
    IProductCatalogRepository repository,
    ProductAccessScopeService accessScope)
{
    public Task<IReadOnlyList<ProductTypeRecord>> ListTypesAsync(CancellationToken cancellationToken) =>
        repository.ListTypesAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductRecord>> TopPurchasedAsync(
        ProductViewer? viewer,
        CancellationToken cancellationToken)
    {
        IReadOnlySet<string>? allowed = await accessScope.ResolveAllowedProductIdsAsync(
            viewer,
            cancellationToken);
        ProductPage page = await repository.ListAsync(new ProductListQuery(
            1, 10, null, null, null, null, null, null, "purchaseCount", "desc",
            viewer?.IsPrivileged == true ? null : true,
            IncludePrivate: false,
            AllowedProductIds: allowed,
            BranchId: viewer?.BranchId), cancellationToken);
        return page.Products;
    }

    public async Task<ProductRecord?> GetByIdAsync(
        string id,
        ProductViewer? viewer,
        CancellationToken cancellationToken,
        bool includePrivate = false)
    {
        if (!IsSafeIdentifier(id))
        {
            return null;
        }

        string normalizedId = id.Trim();
        IReadOnlySet<string>? allowed = await accessScope.ResolveAllowedProductIdsAsync(
            viewer,
            cancellationToken);
        if (allowed is not null && !allowed.Contains(normalizedId)) return null;
        return await repository.FindByIdAsync(
            normalizedId,
            includePrivate && viewer?.IsPrivileged == true,
            viewer?.BranchId,
            cancellationToken);
    }

    public async Task<ProductPage> ListAsync(
        IReadOnlyDictionary<string, string?> query,
        ProductViewer? viewer,
        CancellationToken cancellationToken)
    {
        int page = ParseBoundedInt(query, "page", 1, 1, 10_000);
        int limit = ParseBoundedInt(query, "limit", 100, 1, 200);
        string? search = Bounded(query, "search", 200);
        string? code = Bounded(query, "code", 120);
        string? type = Bounded(query, "type", 120);
        string? brand = Bounded(query, "brand", 120);
        string? section = Bounded(query, "section", 200);
        string? value = Bounded(query, "value", 200);
        string? stationId = Bounded(query, "stationId", 100);
        string sortBy = Bounded(query, "sortBy", 40) ?? "purchaseCount";
        string sortOrder = string.Equals(Bounded(query, "sortOrder", 10), "asc", StringComparison.OrdinalIgnoreCase)
            ? "asc"
            : "desc";
        bool? display = ParseOptionalBool(query, "display");
        bool? adjusted = ParseLegacyAdjusted(query);

        // Public/customer callers may only see displayed products. The repository
        // applies this default even when the query omitted display.
        if (viewer?.IsPrivileged != true)
        {
            display = true;
        }

        IReadOnlySet<string>? allowed = await accessScope.ResolveAllowedProductIdsAsync(
            viewer,
            cancellationToken,
            stationId);
        return await repository.ListAsync(
            new ProductListQuery(page, limit, search, code, type, brand, section, value,
                sortBy, sortOrder, display, viewer?.IsPrivileged == true, allowed, adjusted, viewer?.BranchId), cancellationToken);
    }

    public async Task<(bool Valid, IReadOnlyList<ProductRecord> Products)> FetchByIdsAsync(
        IReadOnlyCollection<string>? ids,
        ProductViewer? viewer,
        CancellationToken cancellationToken,
        bool includePrivate = false)
    {
        if (ids is null || ids.Count > 200)
        {
            return (false, []);
        }

        string[] safeIds = ids
            .Where(IsSafeIdentifier)
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (safeIds.Length == 0 && ids.Count > 0)
        {
            return (false, []);
        }

        IReadOnlySet<string>? allowed = await accessScope.ResolveAllowedProductIdsAsync(
            viewer,
            cancellationToken);
        if (allowed is not null)
        {
            safeIds = safeIds.Where(allowed.Contains).ToArray();
        }
        IReadOnlyList<ProductRecord> products = safeIds.Length == 0
            ? []
            : await repository.FindByIdsAsync(
                safeIds,
                includePrivate && viewer?.IsPrivileged == true,
                viewer?.BranchId,
                cancellationToken);
        return (true, products);
    }

    private static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 100 &&
        value.Trim().All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');

    private static string? Bounded(
        IReadOnlyDictionary<string, string?> query,
        string key,
        int maxLength)
    {
        if (!query.TryGetValue(key, out var values)) return null;
        string? value = values;
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static int ParseBoundedInt(
        IReadOnlyDictionary<string, string?> query,
        string key,
        int fallback,
        int min,
        int max)
    {
        if (!query.TryGetValue(key, out string? raw) || !int.TryParse(raw, out int value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }

    private static bool? ParseOptionalBool(
        IReadOnlyDictionary<string, string?> query,
        string key)
    {
        if (!query.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw)) return null;
        return bool.TryParse(raw, out bool value) ? value : null;
    }

    private static bool? ParseLegacyAdjusted(IReadOnlyDictionary<string, string?> query)
    {
        if (!query.TryGetValue("adjusted", out string? raw)) return null;
        if (raw == string.Empty) return null;
        return string.Equals(raw, "true", StringComparison.Ordinal);
    }
}
