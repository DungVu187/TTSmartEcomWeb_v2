using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Domain.Products;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Mappings;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Products;

public sealed class MongoProductCatalogRepository(IMongoDatabaseProvider databaseProvider)
    : IProductCatalogRepository
{
    private readonly IMongoCollection<BsonDocument> products =
        databaseProvider.Database.GetCollection<BsonDocument>(ProductDocument.CollectionName);

    private readonly IMongoCollection<BsonDocument> types =
        databaseProvider.Database.GetCollection<BsonDocument>(ProductTypeDocument.CollectionName);

    public async Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = BuildFilter(query);
        SortDefinition<BsonDocument> sort = BuildSort(query.SortBy, query.SortOrder);
        int skip = checked((query.Page - 1) * query.Limit);
        if (query.Adjusted.HasValue)
        {
            List<BsonDocument> matchingDocuments = await products.Find(filter)
                .Sort(sort)
                .ToListAsync(cancellationToken);
            ProductRecord[] matchingProducts = matchingDocuments
                .Select(document => MapProduct(document, query.IncludePrivate))
                .ToArray();
            return ApplyAdjustedFilter(query, matchingProducts);
        }

        long total = await products.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        List<BsonDocument> documents = await products.Find(filter)
            .Sort(sort)
            .Skip(skip)
            .Limit(query.Limit)
            .ToListAsync(cancellationToken);

        return new ProductPage(total, query.Page, query.Limit,
            documents.Select(document => MapProduct(document, query.IncludePrivate)).ToArray());
    }

    public async Task<ProductRecord?> FindByIdAsync(string id, bool includePrivate, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = BuildIdFilter(id);
        if (!includePrivate)
        {
            filter &= Builders<BsonDocument>.Filter.Eq("display", true);
        }

        BsonDocument? document = await products.Find(filter).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : MapProduct(document, includePrivate);
    }

    public async Task<IReadOnlyList<ProductRecord>> FindByIdsAsync(
        IReadOnlyCollection<string> ids,
        bool includePrivate,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];
        FilterDefinition<BsonDocument> idFilter = Builders<BsonDocument>.Filter.Or(ids.Select(BuildIdFilter));
        FilterDefinition<BsonDocument> filter = includePrivate
            ? idFilter
            : idFilter & Builders<BsonDocument>.Filter.Eq("display", true);
        List<BsonDocument> documents = await products.Find(filter).ToListAsync(cancellationToken);
        return documents.Select(document => MapProduct(document, includePrivate)).ToArray();
    }

    public async Task<IReadOnlyList<ProductTypeRecord>> ListTypesAsync(CancellationToken cancellationToken)
    {
        List<BsonDocument> documents = await types.Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Ascending("Type"))
            .ToListAsync(cancellationToken);
        return documents.Select(MapType).ToArray();
    }

    private static FilterDefinition<BsonDocument> BuildFilter(ProductListQuery query)
    {
        var builder = Builders<BsonDocument>.Filter;
        List<FilterDefinition<BsonDocument>> clauses = [];
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string escaped = RegexEscape(query.Search);
            clauses.Add(builder.Or(
                builder.Regex("name", new BsonRegularExpression(escaped, "i")),
                builder.Regex("nameUnsigned", new BsonRegularExpression(escaped, "i")),
                builder.Regex("code", new BsonRegularExpression(escaped, "i"))));
        }

        if (!string.IsNullOrWhiteSpace(query.Code))
        {
            string escaped = RegexEscape(query.Code);
            clauses.Add(builder.Or(
                builder.Regex("code", new BsonRegularExpression(escaped, "i")),
                builder.Regex("name", new BsonRegularExpression(escaped, "i"))));
        }

        AddExact(clauses, builder, "type", query.Type);
        AddExact(clauses, builder, "brand", query.Brand);
        AddExact(clauses, builder, "section", query.Section);
        AddExact(clauses, builder, "value", query.Value);
        if (query.Display.HasValue)
        {
            clauses.Add(builder.Eq("display", query.Display.Value));
        }
        if (query.AllowedProductIds is not null)
        {
            FilterDefinition<BsonDocument>[] allowed = query.AllowedProductIds
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Select(BuildIdFilter)
                .ToArray();
            clauses.Add(allowed.Length == 0
                ? builder.In("_id", Array.Empty<ObjectId>())
                : builder.Or(allowed));
        }
        return clauses.Count == 0 ? FilterDefinition<BsonDocument>.Empty : builder.And(clauses);
    }

    private static void AddExact(
        List<FilterDefinition<BsonDocument>> clauses,
        FilterDefinitionBuilder<BsonDocument> builder,
        string field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) clauses.Add(builder.Eq(field, value));
    }

    private static SortDefinition<BsonDocument> BuildSort(string? sortBy, string? sortOrder)
    {
        string field = sortBy switch
        {
            "averageReviews" => "averageReviews",
            "createdAt" => "createdAt",
            "purchaseCount" => "purchaseCount",
            _ => "purchaseCount",
        };
        int direction = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
        var sort = Builders<BsonDocument>.Sort;
        return direction > 0
            ? sort.Ascending(field).Descending("createdAt")
            : sort.Descending(field).Descending("createdAt");
    }

    private static FilterDefinition<BsonDocument> BuildIdFilter(string id)
    {
        var builder = Builders<BsonDocument>.Filter;
        if (ObjectId.TryParse(id, out ObjectId objectId))
        {
            return builder.Or(builder.Eq("_id", objectId), builder.Eq("_id", id));
        }

        return builder.Eq("_id", id);
    }

    private static ProductTypeRecord MapType(BsonDocument document) =>
        new(ReadId(document), ReadString(document, "Type"), ReadString(document, "icon"),
            ReadDate(document, "createdAt"), ReadDate(document, "updatedAt"));

    internal static ProductRecord MapProduct(BsonDocument document, bool includePrivate)
    {
        List<ProductVariant> variants = ReadDocuments(document, "variant")
            .Select(item => MapVariant(item, includePrivate))
            .ToList();
        string? type = ReadString(document, "type");
        string? brand = ReadString(document, "brand");
        string? section = ReadString(document, "section");
        return new ProductRecord(
            ReadId(document), type, ReadString(document, "name"), ReadString(document, "nameUnsigned"),
            ReadBool(document, "display"), ReadString(document, "code"), ReadString(document, "vat"),
            ReadBool(document, "adjusted"), brand, section, ReadString(document, "value"), variants,
            MapInfo(document), ReadDocuments(document, "documents").Select(MapLink).ToArray(),
            ReadLong(document, "purchaseCount"), ReadDocuments(document, "reviews").Select(MapReview).ToArray(),
            ReadDouble(document, "totalRating"), ReadLong(document, "reviewCount"), ReadDouble(document, "averageReviews"),
            ReadString(document, "warranty"), ReadString(document, "solution"), ReadString(document, "description"),
            ReadString(document, "features"), ReadString(document, "operatingMethod"), ReadString(document, "advantages"),
            ReadString(document, "specifications"), ReadDate(document, "createdAt"), ReadDate(document, "updatedAt"),
            ProductAdjustmentPolicy.IsAdjusted(type, brand, section));
    }

    private static ProductVariant MapVariant(BsonDocument document, bool includePrivate)
    {
        string? price = ReadString(document, "price");
        double earn = ReadDouble(document, "earn");
        double quantity = ReadDouble(document, "quantityForSale");
        bool contact = earn == 0 || ParsePrice(price) <= 0 || quantity <= 0;
        return new ProductVariant(
            ReadId(document), contact ? string.Empty : price,
            includePrivate ? ReadString(document, "importPrice") : null,
            includePrivate ? ReadNullableDouble(document, "earn") : null,
            ReadString(document, "imgUrl"), ReadString(document, "color"), ReadString(document, "shape"),
            ReadString(document, "buttonCount"), ReadString(document, "frame"),
            ReadNullableDouble(document, "quantityForSale"), ReadNullableDouble(document, "quantityInStorage"),
            ReadString(document, "note"), contact);
    }

    private static ProductInfo? MapInfo(BsonDocument document)
    {
        if (!document.TryGetValue("infoDoc", out BsonValue value) || !value.IsBsonDocument) return null;
        BsonDocument info = value.AsBsonDocument;
        return new ProductInfo(ReadString(info, "manual"), ReadString(info, "dataSheet"),
            ReadString(info, "catalog"), ReadString(info, "others"));
    }

    private static ProductLink MapLink(BsonDocument document) =>
        new(ReadId(document), ReadString(document, "label"), ReadString(document, "url"), ReadString(document, "sourceType"));

    private static ProductReview MapReview(BsonDocument document) =>
        new(ReadId(document), ReadString(document, "email"), ReadString(document, "comment"),
            ReadNullableDouble(document, "rating"), ReadDate(document, "createdAt"));

    internal static ProductPage ApplyAdjustedFilter(
        ProductListQuery query,
        IReadOnlyList<ProductRecord> matchingProducts)
    {
        if (!query.Adjusted.HasValue) throw new ArgumentException("Adjusted filter is required.", nameof(query));
        ProductRecord[] adjustedProducts = matchingProducts
            .Where(product => product.AdjustedStatus == query.Adjusted.Value)
            .ToArray();
        int skip = checked((query.Page - 1) * query.Limit);
        return new ProductPage(
            adjustedProducts.Length,
            query.Page,
            query.Limit,
            adjustedProducts.Skip(skip).Take(query.Limit).ToArray());
    }

    private static string RegexEscape(string value) => System.Text.RegularExpressions.Regex.Escape(value.Trim());

    private static double ParsePrice(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        string normalized = value.Trim().Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed) ? parsed : 0;
    }

    private static string ReadId(BsonDocument document)
    {
        if (!document.TryGetValue("_id", out BsonValue value) || value.IsBsonNull) return string.Empty;
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string? ReadString(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && !value.IsBsonNull
            ? value.IsString ? value.AsString : value.ToString()
            : null;

    private static bool? ReadBool(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsBoolean ? value.AsBoolean : null;

    private static long ReadLong(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsNumeric ? value.ToInt64() : 0;

    private static double ReadDouble(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsNumeric ? value.ToDouble() : 0;

    private static double? ReadNullableDouble(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsNumeric ? value.ToDouble() : null;

    private static DateTimeOffset? ReadDate(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsValidDateTime
            ? new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero)
            : null;

    private static List<BsonDocument> ReadDocuments(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsBsonArray
            ? value.AsBsonArray.Where(item => item.IsBsonDocument).Select(item => item.AsBsonDocument).ToList()
            : [];
}
