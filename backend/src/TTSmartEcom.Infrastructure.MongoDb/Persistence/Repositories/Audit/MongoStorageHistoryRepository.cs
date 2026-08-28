using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Domain.Audit;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Audit;

public sealed class MongoStorageHistoryRepository(IMongoDatabaseProvider databaseProvider) : IStorageHistoryRepository, IStorageHistoryWriter
{
    private const int MaximumExportRows = 10_000;
    private readonly IMongoCollection<BsonDocument> history = databaseProvider.Database.GetCollection<BsonDocument>(StorageHistoryDocument.CollectionName);

    public async Task AppendAsync(StorageHistoryWriteEntry entry, CancellationToken cancellationToken)
    {
        BsonValue productId = ObjectId.TryParse(entry.ProductId, out ObjectId id)
            ? id
            : entry.ProductId;
        DateTime now = DateTime.UtcNow;
        BsonDocument document = new()
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["productId"] = productId,
            ["productName"] = entry.ProductName,
            ["quantity"] = entry.Quantity,
            ["note"] = entry.Note ?? string.Empty,
            ["isAIScan"] = entry.IsAiScan,
            ["transactionDate"] = (entry.TransactionDate ?? new DateTimeOffset(now, TimeSpan.Zero)).UtcDateTime,
            ["createdAt"] = now,
            ["updatedAt"] = now,
        };
        Put(document, "userName", entry.UserName);
        Put(document, "orderId", entry.OrderId);
        Put(document, "orderName", entry.OrderName);
        Put(document, "source", entry.Source);
        if (entry.QuantityBefore.HasValue) document["quantityBefore"] = entry.QuantityBefore.Value;
        if (entry.QuantityAfter.HasValue) document["quantityAfter"] = entry.QuantityAfter.Value;
        await history.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task<StorageHistoryPage> QueryAsync(StorageHistoryQuery query, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = BuildFilter(query.StartDate, query.EndDate, query.OrderName, query.UserName, query.NoteType, query.Direction);
        long total = await history.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        int take = query.ExportAll ? MaximumExportRows : query.Limit;
        int skip = query.ExportAll ? 0 : checked((query.Page - 1) * query.Limit);
        List<BsonDocument> documents = await history.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("transactionDate").Descending("createdAt"))
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
        long responseLimit = query.ExportAll ? Math.Min(total, MaximumExportRows) : query.Limit;
        int totalPages = query.ExportAll ? (total > 0 ? 1 : 0) : (int)Math.Ceiling(total / (double)query.Limit);
        return new StorageHistoryPage(true, query.Page, responseLimit, total, totalPages, documents.Select(Map).ToArray());
    }

    public async Task<StorageHistoryPage> QueryProductAsync(string productId, int page, int limit, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken cancellationToken)
    {
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> product = ObjectId.TryParse(productId, out ObjectId id)
            ? b.Or(b.Eq("productId", id), b.Eq("productId", productId))
            : b.Eq("productId", productId);
        FilterDefinition<BsonDocument> filter = product & BuildDateFilter(startDate, endDate);
        long total = await history.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        List<BsonDocument> documents = await history.Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("transactionDate").Descending("createdAt"))
            .Skip(checked((page - 1) * limit)).Limit(limit).ToListAsync(cancellationToken);
        return new StorageHistoryPage(true, page, limit, total, (int)Math.Ceiling(total / (double)limit), documents.Select(Map).ToArray());
    }

    public async Task<StorageHistoryFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> usersFilter = b.Ne("userName", BsonNull.Value) & b.Ne("userName", string.Empty);
        FilterDefinition<BsonDocument> ordersFilter = b.Ne("orderName", BsonNull.Value) & b.Ne("orderName", string.Empty);
        List<string> users = await history.Distinct<string>("userName", usersFilter, cancellationToken: cancellationToken).ToListAsync(cancellationToken);
        List<string> orders = await history.Distinct<string>("orderName", ordersFilter, cancellationToken: cancellationToken).ToListAsync(cancellationToken);
        return new StorageHistoryFilterOptions(true, Sort(users), Sort(orders));
    }

    public async Task<long> UpdateOrderNameAsync(string orderId, string newOrderName, CancellationToken cancellationToken)
    {
        UpdateResult result = await history.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("orderId", orderId),
            Builders<BsonDocument>.Update.Set("orderName", newOrderName).Set("updatedAt", DateTime.UtcNow),
            cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    public async Task UpdateTransactionDateAsync(string orderId, DateTimeOffset transactionDate, CancellationToken cancellationToken)
    {
        await history.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("orderId", orderId),
            Builders<BsonDocument>.Update.Set("transactionDate", transactionDate.UtcDateTime).Set("updatedAt", DateTime.UtcNow),
            cancellationToken: cancellationToken);
    }

    private static FilterDefinition<BsonDocument> BuildFilter(DateTimeOffset? startDate, DateTimeOffset? endDate, string? orderName, string? userName, string? noteType, string? direction)
    {
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        List<FilterDefinition<BsonDocument>> clauses = [BuildDateFilter(startDate, endDate)];
        AddRegex(clauses, "orderName", orderName);
        AddRegex(clauses, "userName", userName);
        if (!string.IsNullOrWhiteSpace(noteType)) clauses.Add(NoteTypeFilter(noteType.Trim()));
        if (direction == "import") clauses.Add(b.Or(b.Gt("quantity", 0), b.Eq("source", "import_quantity_adjustment")));
        else if (direction == "export") clauses.Add(b.Lt("quantity", 0));
        return b.And(clauses);
    }

    private static FilterDefinition<BsonDocument> BuildDateFilter(DateTimeOffset? startDate, DateTimeOffset? endDate)
    {
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        List<FilterDefinition<BsonDocument>> clauses = [];
        if (startDate.HasValue) clauses.Add(DateFallbackFilter(b, "$gte", StartOfBangkokDay(startDate.Value)));
        if (endDate.HasValue) clauses.Add(DateFallbackFilter(b, "$lte", EndOfBangkokDay(endDate.Value)));
        return clauses.Count == 0 ? b.Empty : b.And(clauses);
    }

    private static FilterDefinition<BsonDocument> NoteTypeFilter(string noteType)
    {
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> noOrder = b.Eq("orderName", BsonNull.Value) | b.Eq("orderName", string.Empty) | b.Exists("orderName", false);
        FilterDefinition<BsonDocument> hasOrder = b.Ne("orderName", BsonNull.Value) & b.Ne("orderName", string.Empty);
        FilterDefinition<BsonDocument> notAi = b.Ne("isAIScan", true);
        FilterDefinition<BsonDocument> legacySource = b.Exists("source", false);
        FilterDefinition<BsonDocument> notOnline = b.Nin("note", ["Đơn hàng bán online", "Hoàn tác đơn bán online"]);
        return noteType switch
        {
            "nhap_don" => b.Gt("quantity", 0) & hasOrder & notAi & legacySource & notOnline,
            "xuat_don" => b.Lt("quantity", 0) & hasOrder & notAi & legacySource & notOnline,
            "nhap_thu_cong" => b.Gt("quantity", 0) & noOrder & notAi & (legacySource | b.Eq("source", "product_manual")),
            "xuat_thu_cong" => b.Lt("quantity", 0) & noOrder & notAi & (legacySource | b.Eq("source", "product_manual")),
            "nhap_ai" => b.Gt("quantity", 0) & b.Eq("isAIScan", true),
            "xuat_ai" => b.Lt("quantity", 0) & b.Eq("isAIScan", true),
            "order_line_manual" or "order_line_complete" or "order_bulk_complete" or "import_quantity_adjustment" or "product_manual" => b.Eq("source", noteType),
            "ban_online" => b.In("source", ["online_sale", "online_sale_revert"]) | (legacySource & b.In("note", ["Đơn hàng bán online", "Hoàn tác đơn bán online"])),
            _ => b.Empty,
        };
    }

    private static void AddRegex(List<FilterDefinition<BsonDocument>> clauses, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        string trimmed = value.Trim()[..Math.Min(value.Trim().Length, 100)];
        clauses.Add(Builders<BsonDocument>.Filter.Regex(field, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(trimmed), "i")));
    }

    private static StorageHistoryEntry Map(BsonDocument document) => new(
        Read(document, "_id") ?? string.Empty, Read(document, "productId"), Read(document, "productName"), ReadDouble(document, "quantity"),
        Read(document, "userName"), Read(document, "orderId"), Read(document, "orderName"), Read(document, "note"),
        document.TryGetValue("isAIScan", out BsonValue ai) && ai.IsBoolean && ai.AsBoolean, Read(document, "source"),
        ReadDate(document, "transactionDate") ?? ReadDate(document, "createdAt"), ReadNullableDouble(document, "quantityBefore"), ReadNullableDouble(document, "quantityAfter"),
        ReadDate(document, "createdAt"), ReadDate(document, "updatedAt"));

    private static string? Read(BsonDocument document, string field) => document.TryGetValue(field, out BsonValue value) && !value.IsBsonNull ? value.IsString ? value.AsString : value.ToString() : null;
    private static double ReadDouble(BsonDocument document, string field) => document.TryGetValue(field, out BsonValue value) && value.IsNumeric ? value.ToDouble() : 0;
    private static double? ReadNullableDouble(BsonDocument document, string field) => document.TryGetValue(field, out BsonValue value) && value.IsNumeric ? value.ToDouble() : null;
    private static DateTimeOffset? ReadDate(BsonDocument document, string field) => document.TryGetValue(field, out BsonValue value) && value.IsValidDateTime ? new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero) : null;
    private static string[] Sort(IEnumerable<string> values) => values.Where(static x => !string.IsNullOrWhiteSpace(x)).Select(static x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Create(new System.Globalization.CultureInfo("vi-VN"), true)).ToArray();
    private static void Put(BsonDocument document, string field, string? value)
    {
        if (value is not null) document[field] = value;
    }
    private static DateTime StartOfBangkokDay(DateTimeOffset value) => new DateTimeOffset(value.Year, value.Month, value.Day, 0, 0, 0, TimeSpan.FromHours(7)).UtcDateTime;
    private static DateTime EndOfBangkokDay(DateTimeOffset value) => new DateTimeOffset(value.Year, value.Month, value.Day, 23, 59, 59, 999, TimeSpan.FromHours(7)).UtcDateTime;
    private static FilterDefinition<BsonDocument> DateFallbackFilter(FilterDefinitionBuilder<BsonDocument> builder, string comparison, DateTime value)
    {
        FilterDefinition<BsonDocument> transaction = comparison == "$gte" ? builder.Gte("transactionDate", value) : builder.Lte("transactionDate", value);
        FilterDefinition<BsonDocument> created = comparison == "$gte" ? builder.Gte("createdAt", value) : builder.Lte("createdAt", value);
        return builder.Or(transaction, builder.And(builder.Eq("transactionDate", BsonNull.Value), created));
    }
}
