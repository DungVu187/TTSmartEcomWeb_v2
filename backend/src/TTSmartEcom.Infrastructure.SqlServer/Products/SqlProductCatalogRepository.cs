using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.Infrastructure.SqlServer.Products;

#pragma warning disable CA1725

public sealed class SqlProductCatalogRepository(
    ICompanyDbConnectionFactory companyFactory,
    SqlBranchProductReader branchProducts) : IProductCatalogRepository
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = companyFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await ValidateBranchCompanyAsync(connection, query.BranchId, cancellationToken);

        (string predicate, Action<SqlCommand> addParameters) = BuildPredicate(query);
        await using SqlCommand count = new($"SELECT COUNT_BIG(*) FROM dbo.Products p WHERE {predicate};", connection);
        addParameters(count);
        long total = Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

        await using SqlCommand command = new($"""
            SELECT p.ProductId,p.PublicId,p.TypeName,p.Name,p.NameUnsigned,p.Display,p.Code,p.VatRaw,p.Adjusted,
                   p.BrandName,p.CategoryName,p.CategoryValue,p.Description,p.DetailsJson,p.DocumentsJson,
                   p.PurchaseCount,p.SourceCreatedAtUtc,p.SourceUpdatedAtUtc
            FROM dbo.Products p
            WHERE {predicate}
            ORDER BY {SortColumn(query.SortBy)} {(query.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC")},p.ProductId
            OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
            """, connection);
        addParameters(command);
        command.Parameters.AddWithValue("@skip", Math.Max(0, (query.Page - 1) * query.Limit));
        command.Parameters.AddWithValue("@take", query.Limit);
        List<ProductRow> rows = await ReadProductRowsAsync(command, cancellationToken);
        IReadOnlyList<ProductRecord> products = await MaterializeAsync(
            connection, rows, query.IncludePrivate, query.BranchId, cancellationToken);
        return new ProductPage(total, query.Page, query.Limit, products);
    }

    public Task<ProductRecord?> FindByIdAsync(
        string id,
        bool includePrivate,
        CancellationToken cancellationToken) =>
        FindByIdAsync(id, includePrivate, null, cancellationToken);

    public async Task<ProductRecord?> FindByIdAsync(
        string id,
        bool includePrivate,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductRecord> products = await FindByIdsAsync([id], includePrivate, branchId, cancellationToken);
        return products.Count == 0 ? null : products[0];
    }

    public Task<IReadOnlyList<ProductRecord>> FindByIdsAsync(
        IReadOnlyCollection<string> ids,
        bool includePrivate,
        CancellationToken cancellationToken) =>
        FindByIdsAsync(ids, includePrivate, null, cancellationToken);

    public async Task<IReadOnlyList<ProductRecord>> FindByIdsAsync(
        IReadOnlyCollection<string> ids,
        bool includePrivate,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        string[] values = ids.Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (values.Length == 0) return [];

        await using SqlConnection connection = companyFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await ValidateBranchCompanyAsync(connection, branchId, cancellationToken);
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT p.ProductId,p.PublicId,p.TypeName,p.Name,p.NameUnsigned,p.Display,p.Code,p.VatRaw,p.Adjusted,
                   p.BrandName,p.CategoryName,p.CategoryValue,p.Description,p.DetailsJson,p.DocumentsJson,
                   p.PurchaseCount,p.SourceCreatedAtUtc,p.SourceUpdatedAtUtc
            FROM dbo.Products p
            WHERE p.IsDeleted=0 AND (@private=1 OR p.Display=1)
              {(branchId.HasValue ? "AND EXISTS (SELECT 1 FROM dbo.ProductBranchAssignments a WHERE a.ProductId=p.ProductId AND a.BranchId=@branchId AND a.IsActive=1)" : string.Empty)}
              AND p.PublicId IN ({string.Join(',', values.Select((_, index) => "@id" + index))})
            ORDER BY p.Name,p.ProductId;
            """;
        command.Parameters.AddWithValue("@private", includePrivate);
        if (branchId.HasValue) command.Parameters.AddWithValue("@branchId", branchId.Value);
        for (int index = 0; index < values.Length; index++) command.Parameters.AddWithValue("@id" + index, values[index]);
        List<ProductRow> rows = await ReadProductRowsAsync(command, cancellationToken);
        return await MaterializeAsync(connection, rows, includePrivate, branchId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductTypeRecord>> ListTypesAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = companyFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = new("SELECT PublicId,Name,Icon FROM dbo.ProductTypes ORDER BY Name;", connection);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        List<ProductTypeRecord> result = [];
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new ProductTypeRecord(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                null,
                null));
        return result;
    }

    private async Task<IReadOnlyList<ProductRecord>> MaterializeAsync(
        SqlConnection companyConnection,
        IReadOnlyList<ProductRow> products,
        bool includePrivate,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0) return [];
        Guid[] productIds = products.Select(product => product.ProductId).ToArray();
        await using SqlCommand command = companyConnection.CreateCommand();
        command.CommandText = $"""
            SELECT ProductVariantId,ProductId,PublicId,SortOrder,PriceRaw,DetailsJson
            FROM dbo.ProductVariants
            WHERE ProductId IN ({string.Join(',', productIds.Select((_, index) => "@product" + index))})
            ORDER BY ProductId,SortOrder;
            """;
        for (int index = 0; index < productIds.Length; index++) command.Parameters.AddWithValue("@product" + index, productIds[index]);
        List<VariantRow> variants = [];
        await using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                variants.Add(new VariantRow(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? "{}" : reader.GetString(5)));
        }

        IReadOnlyDictionary<Guid, SqlBranchVariantState> states = branchId.HasValue
            ? await branchProducts.LoadStatesAsync(
                variants.ToDictionary(variant => variant.ProductVariantId, variant => variant.ProductId),
                cancellationToken)
            : new Dictionary<Guid, SqlBranchVariantState>();

        Dictionary<Guid, List<ProductVariant>> mappedVariants = [];
        Dictionary<Guid, long> branchPurchaseCounts = [];
        foreach (VariantRow variant in variants)
        {
            states.TryGetValue(variant.ProductVariantId, out SqlBranchVariantState? state);
            using JsonDocument details = Parse(variant.DetailsJson);
            JsonElement value = details.RootElement;
            ProductVariant mapped = new(
                variant.PublicId,
                branchId.HasValue ? state?.PriceRaw : variant.DefaultPriceRaw,
                includePrivate && branchId.HasValue ? state?.ImportPriceRaw : null,
                Number(value, "earn"),
                Text(value, "imgUrl"),
                Text(value, "color"),
                Text(value, "shape"),
                Text(value, "buttonCount"),
                Text(value, "frame"),
                state?.QuantityForSale ?? 0,
                state?.QuantityInStorage ?? 0,
                Text(value, "note"));
            if (!mappedVariants.TryGetValue(variant.ProductId, out List<ProductVariant>? list))
                mappedVariants[variant.ProductId] = list = [];
            list.Add(mapped);
            if (state is not null) branchPurchaseCounts[variant.ProductId] = state.PurchaseCount;
        }

        return products.Select(product => MapProduct(
            product,
            mappedVariants.GetValueOrDefault(product.ProductId) ?? [],
            branchId.HasValue ? branchPurchaseCounts.GetValueOrDefault(product.ProductId) : product.PurchaseCount)).ToArray();
    }

    private static ProductRecord MapProduct(ProductRow row, IReadOnlyList<ProductVariant> variants, long purchaseCount)
    {
        using JsonDocument details = Parse(row.DetailsJson);
        JsonElement root = details.RootElement;
        ProductLink[] documents = Read<ProductLink[]>(row.DocumentsJson) ?? [];
        ProductReview[] reviews = Read<ProductReview[]>(root, "reviews") ?? [];
        ProductInfo? info = Read<ProductInfo>(root, "infoDoc");
        return new ProductRecord(
            row.PublicId, row.TypeName, row.Name, row.NameUnsigned, row.Display, row.Code, row.VatRaw,
            row.Adjusted, row.BrandName, row.CategoryName, row.CategoryValue, variants, info, documents,
            purchaseCount, reviews, Number(root, "totalRating") ?? 0, Long(root, "reviewCount"),
            Number(root, "averageReviews") ?? 0, Text(root, "warranty"), Text(root, "solution"),
            row.Description, Text(root, "features"), Text(root, "operatingMethod"), Text(root, "advantages"),
            Text(root, "specifications"), row.CreatedAt, row.UpdatedAt, false);
    }

    private static async Task<List<ProductRow>> ReadProductRowsAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        List<ProductRow> rows = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ProductRow(
                reader.GetGuid(0), reader.GetString(1), S(reader, 2), S(reader, 3), S(reader, 4), B(reader, 5),
                S(reader, 6), S(reader, 7), B(reader, 8), S(reader, 9), S(reader, 10), S(reader, 11),
                S(reader, 12), reader.IsDBNull(13) ? "{}" : reader.GetString(13),
                reader.IsDBNull(14) ? "[]" : reader.GetString(14), reader.IsDBNull(15) ? 0 : reader.GetInt64(15),
                D(reader, 16), D(reader, 17)));
        }
        return rows;
    }

    private static (string Predicate, Action<SqlCommand> AddParameters) BuildPredicate(ProductListQuery query)
    {
        List<string> clauses = ["p.IsDeleted=0"];
        List<Action<SqlCommand>> parameters = [];
        if (!query.IncludePrivate || query.Display.HasValue)
        {
            bool display = query.IncludePrivate ? query.Display ?? true : true;
            clauses.Add("p.Display=@display");
            parameters.Add(command => command.Parameters.AddWithValue("@display", display));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            clauses.Add("(p.Name LIKE @search OR p.NameUnsigned LIKE @search OR p.Code LIKE @search)");
            parameters.Add(command => command.Parameters.AddWithValue("@search", "%" + query.Search.Trim() + "%"));
        }
        AddEqual(query.Code, "p.Code", "code", clauses, parameters);
        AddEqual(query.Type, "p.TypeName", "type", clauses, parameters);
        AddEqual(query.Brand, "p.BrandName", "brand", clauses, parameters);
        AddEqual(query.Section, "p.CategoryName", "section", clauses, parameters);
        AddEqual(query.Value, "p.CategoryValue", "value", clauses, parameters);
        if (query.Adjusted.HasValue)
        {
            clauses.Add("p.Adjusted=@adjusted");
            parameters.Add(command => command.Parameters.AddWithValue("@adjusted", query.Adjusted.Value));
        }
        if (query.BranchId.HasValue)
        {
            clauses.Add("EXISTS (SELECT 1 FROM dbo.ProductBranchAssignments a WHERE a.ProductId=p.ProductId AND a.BranchId=@branchId AND a.IsActive=1)");
            parameters.Add(command => command.Parameters.AddWithValue("@branchId", query.BranchId.Value));
        }
        if (query.AllowedProductIds is { } allowed)
        {
            string[] ids = allowed.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (ids.Length == 0) clauses.Add("1=0");
            else
            {
                clauses.Add($"p.PublicId IN ({string.Join(',', ids.Select((_, index) => "@allowed" + index))})");
                parameters.Add(command =>
                {
                    for (int index = 0; index < ids.Length; index++) command.Parameters.AddWithValue("@allowed" + index, ids[index]);
                });
            }
        }
        return (string.Join(" AND ", clauses), command =>
        {
            foreach (Action<SqlCommand> add in parameters) add(command);
        });
    }

    private static void AddEqual(
        string? value,
        string column,
        string parameter,
        List<string> clauses,
        List<Action<SqlCommand>> parameters)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        clauses.Add($"{column}=@{parameter}");
        parameters.Add(command => command.Parameters.AddWithValue("@" + parameter, value.Trim()));
    }

    private async Task ValidateBranchCompanyAsync(
        SqlConnection companyConnection,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        if (!branchId.HasValue) return;
        SqlBranchDatabaseScope branchScope = await branchProducts.GetScopeAsync(cancellationToken);
        if (branchScope.BranchId != branchId.Value)
            throw new UnauthorizedAccessException("Requested branch does not match the operational database assignment.");
        await using SqlCommand command = new("SELECT CompanyId FROM dbo.CompanyDatabaseInfo WHERE SingletonKey=1;", companyConnection);
        object? companyId = await command.ExecuteScalarAsync(cancellationToken);
        if (companyId is not Guid value || value != branchScope.CompanyId)
            throw new UnauthorizedAccessException("Requested Company and Branch database assignments do not match.");
    }

    private static string SortColumn(string value) => value switch
    {
        "name" => "p.Name",
        "createdAt" => "p.SourceCreatedAtUtc",
        "updatedAt" => "p.SourceUpdatedAtUtc",
        _ => "p.PurchaseCount",
    };

    private static JsonDocument Parse(string json) => JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
    private static T? Read<T>(string json) { try { return JsonSerializer.Deserialize<T>(json, Json); } catch (JsonException) { return default; } }
    private static T? Read<T>(JsonElement root, string name) { try { return root.TryGetProperty(name, out JsonElement value) ? value.Deserialize<T>(Json) : default; } catch (JsonException) { return default; } }
    private static string? Text(JsonElement root, string name) => root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static double? Number(JsonElement root, string name) => root.TryGetProperty(name, out JsonElement value) && value.TryGetDouble(out double number) ? number : null;
    private static long Long(JsonElement root, string name) => root.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long number) ? number : 0;
    private static string? S(SqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
    private static bool? B(SqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetBoolean(index);
    private static DateTimeOffset? D(SqlDataReader reader, int index) => reader.IsDBNull(index) ? null : new DateTimeOffset(reader.GetDateTime(index), TimeSpan.Zero);

    private sealed record ProductRow(
        Guid ProductId,
        string PublicId,
        string? TypeName,
        string? Name,
        string? NameUnsigned,
        bool? Display,
        string? Code,
        string? VatRaw,
        bool? Adjusted,
        string? BrandName,
        string? CategoryName,
        string? CategoryValue,
        string? Description,
        string DetailsJson,
        string DocumentsJson,
        long PurchaseCount,
        DateTimeOffset? CreatedAt,
        DateTimeOffset? UpdatedAt);

    private sealed record VariantRow(
        Guid ProductVariantId,
        Guid ProductId,
        string PublicId,
        int SortOrder,
        string? DefaultPriceRaw,
        string DetailsJson);
}
