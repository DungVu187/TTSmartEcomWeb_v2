using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace TTSmartEcom.Infrastructure.SqlServer.Products;

public sealed record SqlBranchDatabaseScope(Guid CompanyId, Guid BranchId);

public sealed record SqlBranchVariantState(
    Guid ProductId,
    Guid ProductVariantId,
    string? PriceRaw,
    string? ImportPriceRaw,
    double QuantityForSale,
    double QuantityInStorage,
    long PurchaseCount);

public sealed record SqlBranchProductSnapshot(
    Guid ProductId,
    string ProductPublicId,
    string? ProductName,
    string? BrandName,
    string? Code,
    bool Display,
    Guid ProductVariantId,
    string ProductVariantPublicId,
    int VariantIndex,
    string? VariantName,
    string? PriceRaw,
    string? ImportPriceRaw,
    string DetailsJson,
    double QuantityForSale,
    double QuantityInStorage,
    bool IsAssigned);

public sealed class SqlBranchProductReader(
    ICompanyDbConnectionFactory companyFactory,
    IOperationalDbConnectionFactory operationalFactory)
{
    public async Task<SqlBranchDatabaseScope> GetScopeAsync(CancellationToken cancellationToken)
    {
        Guid companyId;
        Guid branchId;
        await using (SqlConnection branch = operationalFactory.Create())
        {
            await branch.OpenAsync(cancellationToken);
            await using SqlCommand command = new("""
                SELECT CompanyId,BranchId
                FROM dbo.BranchDatabaseInfo
                WHERE SingletonKey=1 AND DatabaseKind=N'BranchOperational';
                """, branch);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Branch database metadata is missing.");
            companyId = reader.GetGuid(0);
            branchId = reader.GetGuid(1);
        }

        await using (SqlConnection company = companyFactory.Create())
        {
            await company.OpenAsync(cancellationToken);
            await using SqlCommand command = new("""
                SELECT CompanyId
                FROM dbo.CompanyDatabaseInfo
                WHERE SingletonKey=1 AND DatabaseKind=N'CompanyShared';
                """, company);
            object? value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is not Guid configuredCompanyId || configuredCompanyId != companyId)
                throw new InvalidOperationException("Company and Branch database assignments do not match.");
        }

        return new SqlBranchDatabaseScope(companyId, branchId);
    }

    public async Task<SqlBranchProductSnapshot?> FindVariantAsync(
        string productPublicId,
        int variantIndex,
        bool requireActiveAssignment,
        CancellationToken cancellationToken)
    {
        if (variantIndex < 0) return null;
        IReadOnlyList<SqlBranchProductSnapshot> products = await FindVariantsAsync(
            [productPublicId], requireActiveAssignment, cancellationToken);
        return products.FirstOrDefault(item =>
            item.ProductPublicId.Equals(productPublicId, StringComparison.OrdinalIgnoreCase)
            && item.VariantIndex == variantIndex);
    }

    public async Task<IReadOnlyList<SqlBranchProductSnapshot>> FindVariantsAsync(
        IReadOnlyCollection<string> productPublicIds,
        bool requireActiveAssignment,
        CancellationToken cancellationToken)
    {
        string[] ids = productPublicIds
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0) return [];

        SqlBranchDatabaseScope scope = await GetScopeAsync(cancellationToken);
        List<CompanyVariant> variants = [];
        await using (SqlConnection company = companyFactory.Create())
        {
            await company.OpenAsync(cancellationToken);
            await using SqlCommand command = company.CreateCommand();
            command.CommandText = $"""
                SELECT p.ProductId,p.PublicId,p.Name,p.BrandName,p.Code,p.Display,
                       v.ProductVariantId,v.PublicId,v.SortOrder,v.Name,v.DetailsJson,
                       CASE WHEN a.ProductBranchAssignmentId IS NULL THEN CONVERT(bit,0) ELSE CONVERT(bit,1) END
                FROM dbo.Products p
                INNER JOIN dbo.ProductVariants v ON v.ProductId=p.ProductId
                LEFT JOIN dbo.ProductBranchAssignments a
                    ON a.ProductId=p.ProductId AND a.BranchId=@branchId AND a.IsActive=1
                WHERE p.IsDeleted=0
                  AND p.PublicId IN ({string.Join(',', ids.Select((_, index) => "@product" + index))})
                ORDER BY p.PublicId,v.SortOrder;
                """;
            command.Parameters.AddWithValue("@branchId", scope.BranchId);
            for (int index = 0; index < ids.Length; index++) command.Parameters.AddWithValue("@product" + index, ids[index]);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                bool assigned = reader.GetBoolean(11);
                if (requireActiveAssignment && !assigned) continue;
                variants.Add(new CompanyVariant(
                    reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
                    !reader.IsDBNull(5) && reader.GetBoolean(5), reader.GetGuid(6), reader.GetString(7),
                    reader.GetInt32(8), reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.IsDBNull(10) ? "{}" : reader.GetString(10), assigned));
            }
        }

        IReadOnlyDictionary<Guid, SqlBranchVariantState> states = await LoadStatesAsync(
            variants.ToDictionary(item => item.ProductVariantId, item => item.ProductId),
            cancellationToken);
        return variants.Select(variant =>
        {
            states.TryGetValue(variant.ProductVariantId, out SqlBranchVariantState? state);
            return new SqlBranchProductSnapshot(
                variant.ProductId,
                variant.ProductPublicId,
                variant.ProductName,
                variant.BrandName,
                variant.Code,
                variant.Display,
                variant.ProductVariantId,
                variant.ProductVariantPublicId,
                variant.VariantIndex,
                variant.VariantName,
                state?.PriceRaw,
                state?.ImportPriceRaw,
                variant.DetailsJson,
                state?.QuantityForSale ?? 0,
                state?.QuantityInStorage ?? 0,
                variant.IsAssigned);
        }).ToArray();
    }

    public async Task<IReadOnlyDictionary<Guid, SqlBranchVariantState>> LoadStatesAsync(
        IReadOnlyDictionary<Guid, Guid> variantProductIds,
        CancellationToken cancellationToken)
    {
        Guid[] products = variantProductIds.Values.Distinct().ToArray();
        Guid[] variants = variantProductIds.Keys.Distinct().ToArray();
        if (variants.Length == 0) return new Dictionary<Guid, SqlBranchVariantState>();

        Dictionary<Guid, (double Sale, double Storage)> balances = [];
        Dictionary<Guid, (Guid ProductId, string? Price, string? ImportPrice)> prices = [];
        Dictionary<Guid, long> purchases = [];
        await using SqlConnection branch = operationalFactory.Create();
        await branch.OpenAsync(cancellationToken);

        await using (SqlCommand command = branch.CreateCommand())
        {
            command.CommandText = $"""
                SELECT ProductVariantId,QuantityForSale,QuantityInStorage
                FROM dbo.BranchStockBalances
                WHERE ProductVariantId IN ({string.Join(',', variants.Select((_, index) => "@variant" + index))});
                """;
            for (int index = 0; index < variants.Length; index++) command.Parameters.AddWithValue("@variant" + index, variants[index]);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                balances[reader.GetGuid(0)] = (
                    reader.IsDBNull(1) ? 0 : (double)reader.GetDecimal(1),
                    reader.IsDBNull(2) ? 0 : (double)reader.GetDecimal(2));
        }

        await using (SqlCommand command = branch.CreateCommand())
        {
            command.CommandText = $"""
                SELECT ProductVariantId,ProductId,PriceRaw,ImportPriceRaw
                FROM dbo.BranchProductVariants
                WHERE IsActive=1 AND ProductVariantId IN ({string.Join(',', variants.Select((_, index) => "@variant" + index))});
                """;
            for (int index = 0; index < variants.Length; index++) command.Parameters.AddWithValue("@variant" + index, variants[index]);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                prices[reader.GetGuid(0)] = (
                    reader.GetGuid(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3));
        }

        if (products.Length > 0)
        {
            await using SqlCommand command = branch.CreateCommand();
            command.CommandText = $"""
                SELECT ProductId,PurchaseCount
                FROM dbo.BranchProductStatistics
                WHERE ProductId IN ({string.Join(',', products.Select((_, index) => "@product" + index))});
                """;
            for (int index = 0; index < products.Length; index++) command.Parameters.AddWithValue("@product" + index, products[index]);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) purchases[reader.GetGuid(0)] = reader.GetInt64(1);
        }

        Dictionary<Guid, SqlBranchVariantState> result = [];
        foreach (Guid variantId in variants)
        {
            prices.TryGetValue(variantId, out var price);
            balances.TryGetValue(variantId, out var balance);
            Guid productId = price.ProductId != Guid.Empty
                ? price.ProductId
                : variantProductIds.GetValueOrDefault(variantId);
            if (productId == Guid.Empty) continue;
            result[variantId] = new SqlBranchVariantState(
                productId,
                variantId,
                price.Price,
                price.ImportPrice,
                balance.Sale,
                balance.Storage,
                purchases.GetValueOrDefault(productId));
        }
        return result;
    }

    private sealed record CompanyVariant(
        Guid ProductId,
        string ProductPublicId,
        string? ProductName,
        string? BrandName,
        string? Code,
        bool Display,
        Guid ProductVariantId,
        string ProductVariantPublicId,
        int VariantIndex,
        string? VariantName,
        string DetailsJson,
        bool IsAssigned);
}
