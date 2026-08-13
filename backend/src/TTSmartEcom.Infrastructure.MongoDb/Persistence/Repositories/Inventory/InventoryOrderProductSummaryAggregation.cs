using MongoDB.Bson;
using TTSmartEcom.Domain.Inventory;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Inventory;

internal static class InventoryOrderProductSummaryAggregation
{
    private const int PageSize = 10;

    public static BsonDocument[] Build(int page)
    {
        int skip = checked((page - 1) * PageSize);
        BsonDocument convertProductId = new("$convert", new BsonDocument
        {
            ["input"] = "$productList.productId",
            ["to"] = "objectId",
            ["onError"] = BsonNull.Value,
            ["onNull"] = BsonNull.Value,
        });

        return
        [
            new("$unwind", "$productList"),
            new("$set", new BsonDocument("normalizedProductId", convertProductId)),
            new("$match", new BsonDocument("normalizedProductId", new BsonDocument("$ne", BsonNull.Value))),
            new("$group", new BsonDocument
            {
                ["_id"] = "$normalizedProductId",
                ["totalOrdered"] = new BsonDocument("$sum", new BsonDocument(
                    "$convert", new BsonDocument
                    {
                        ["input"] = "$productList.quantity",
                        ["to"] = "double",
                        ["onError"] = 0,
                        ["onNull"] = 0,
                    })),
            }),
            new("$facet", new BsonDocument
            {
                ["products"] = new BsonArray
                {
                    new BsonDocument("$lookup", new BsonDocument
                    {
                        ["from"] = "products",
                        ["localField"] = "_id",
                        ["foreignField"] = "_id",
                        ["as"] = "productInfo",
                    }),
                    new BsonDocument("$unwind", "$productInfo"),
                    new BsonDocument("$project", new BsonDocument
                    {
                        ["_id"] = 1,
                        ["name"] = "$productInfo.name",
                        ["brand"] = "$productInfo.brand",
                        ["variant"] = "$productInfo.variant",
                        ["totalOrdered"] = 1,
                    }),
                    new BsonDocument("$sort", new BsonDocument { ["name"] = 1, ["_id"] = 1 }),
                    new BsonDocument("$skip", skip),
                    new BsonDocument("$limit", PageSize),
                },
                ["metadata"] = new BsonArray
                {
                    new BsonDocument("$count", "total"),
                },
            }),
        ];
    }

    public static (IReadOnlyList<InventoryOrderProductSummary> Products, long Total) Map(BsonDocument? facet)
    {
        if (facet is null) return ([], 0);

        BsonArray rows = facet.TryGetValue("products", out BsonValue products) && products.IsBsonArray
            ? products.AsBsonArray
            : [];
        long total = facet.TryGetValue("metadata", out BsonValue metadata) && metadata.IsBsonArray &&
                     metadata.AsBsonArray.FirstOrDefault() is { IsBsonDocument: true } count &&
                     count.AsBsonDocument.TryGetValue("total", out BsonValue value) && value.IsNumeric
            ? value.ToInt64()
            : 0;

        return (rows.Where(static row => row.IsBsonDocument).Select(static row => MapProduct(row.AsBsonDocument)).ToArray(), total);
    }

    private static InventoryOrderProductSummary MapProduct(BsonDocument document) => new(
        ReadId(document),
        ReadString(document, "name"),
        ReadString(document, "brand"),
        ReadArray(document, "variant")
            .Where(static value => value.IsBsonDocument)
            .Select(static value => MapVariant(value.AsBsonDocument))
            .ToArray(),
        ReadDouble(document, "totalOrdered"));

    private static InventoryOrderProductVariant MapVariant(BsonDocument document) => new(
        ReadNullableId(document),
        ReadString(document, "price"),
        ReadString(document, "importPrice"),
        ReadNullableDouble(document, "earn"),
        ReadString(document, "imgUrl"),
        ReadString(document, "color"),
        ReadString(document, "shape"),
        ReadString(document, "buttonCount"),
        ReadString(document, "frame"),
        ReadNullableDouble(document, "quantityForSale"),
        ReadNullableDouble(document, "quantityInStorage"),
        ReadString(document, "note"));

    private static string ReadId(BsonDocument document) =>
        document.TryGetValue("_id", out BsonValue value) && !value.IsBsonNull
            ? value.ToString() ?? string.Empty
            : string.Empty;

    private static string? ReadNullableId(BsonDocument document) =>
        document.TryGetValue("_id", out BsonValue value) && !value.IsBsonNull ? value.ToString() : null;

    private static string? ReadString(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && !value.IsBsonNull
            ? value.IsString ? value.AsString : value.ToString()
            : null;

    private static BsonArray ReadArray(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsBsonArray ? value.AsBsonArray : [];

    private static double ReadDouble(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsNumeric ? value.ToDouble() : 0;

    private static double? ReadNullableDouble(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsNumeric ? value.ToDouble() : null;
}
