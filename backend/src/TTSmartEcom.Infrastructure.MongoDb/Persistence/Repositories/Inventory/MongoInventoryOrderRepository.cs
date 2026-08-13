using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Inventory;
using TTSmartEcom.Domain.Inventory;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Inventory;

public sealed class MongoInventoryOrderRepository(IMongoDatabaseProvider databaseProvider) : IInventoryOrderRepository
{
    private readonly IMongoCollection<IpOrderDocument> imports = databaseProvider.Database.GetCollection<IpOrderDocument>(IpOrderDocument.CollectionName);
    private readonly IMongoCollection<EpOrderDocument> exports = databaseProvider.Database.GetCollection<EpOrderDocument>(EpOrderDocument.CollectionName);
    private readonly IMongoCollection<ProductDocument> products = databaseProvider.Database.GetCollection<ProductDocument>(ProductDocument.CollectionName);

    public async Task<(IReadOnlyList<InventoryOrder> Orders, long Total)> ListAsync(InventoryOrderKind kind, InventoryOrderListQuery query, CancellationToken cancellationToken) =>
        kind == InventoryOrderKind.Import
            ? await ListImportsAsync(query, cancellationToken)
            : await ListExportsAsync(query, cancellationToken);

    public async Task<(IReadOnlyList<InventoryOrderProductSummary> Products, long Total)> ListProductsAsync(
        InventoryOrderKind kind, int page, CancellationToken cancellationToken)
    {
        IMongoCollection<BsonDocument> orders = kind == InventoryOrderKind.Import
            ? databaseProvider.Database.GetCollection<BsonDocument>(IpOrderDocument.CollectionName)
            : databaseProvider.Database.GetCollection<BsonDocument>(EpOrderDocument.CollectionName);
        List<BsonDocument> facets = await orders.Aggregate<BsonDocument>(
                InventoryOrderProductSummaryAggregation.Build(page),
                new AggregateOptions { AllowDiskUse = true },
                cancellationToken)
            .ToListAsync(cancellationToken);
        return InventoryOrderProductSummaryAggregation.Map(facets.FirstOrDefault());
    }

    public async Task<InventoryOrder?> FindAsync(InventoryOrderKind kind, string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out ObjectId objectId)) return null;
        InventoryOrder? order;
        if (kind == InventoryOrderKind.Import)
        {
            IpOrderDocument? document = await imports.Find(Builders<IpOrderDocument>.Filter.Eq(x => x.Id, objectId)).Limit(1).FirstOrDefaultAsync(cancellationToken);
            order = document is null ? null : Map(document);
        }
        else
        {
            EpOrderDocument? document = await exports.Find(Builders<EpOrderDocument>.Filter.Eq(x => x.Id, objectId)).Limit(1).FirstOrDefaultAsync(cancellationToken);
            order = document is null ? null : Map(document);
        }
        return order is null ? null : await EnrichAsync(order, cancellationToken);
    }

    public async Task<InventoryOrder> InsertAsync(InventoryOrder order, CancellationToken cancellationToken)
    {
        if (order.Kind == InventoryOrderKind.Import)
        {
            IpOrderDocument document = ToImport(order, null);
            await imports.InsertOneAsync(document, cancellationToken: cancellationToken);
            return Map(document) with { Version = document.Version ?? 0 };
        }
        EpOrderDocument export = ToExport(order, null);
        await exports.InsertOneAsync(export, cancellationToken: cancellationToken);
        return Map(export) with { Version = export.Version ?? 0 };
    }

    public async Task<InventoryOrder?> UpdateAsync(InventoryOrder order, int expectedVersion, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(order.Id, out ObjectId id)) return null;
        if (order.Kind == InventoryOrderKind.Import)
        {
            IpOrderDocument? current = await imports.Find(Builders<IpOrderDocument>.Filter.Eq(x => x.Id, id)).Limit(1).FirstOrDefaultAsync(cancellationToken);
            if (current is null) return null;
            IpOrderDocument desired = ToImport(order, current);
            UpdateDefinition<IpOrderDocument> update = Builders<IpOrderDocument>.Update
                .Set(x => x.OrderName, desired.OrderName).Set(x => x.Note, desired.Note).Set(x => x.UserName, desired.UserName)
                .Set(x => x.ProductList, desired.ProductList).Set(x => x.Images, desired.Images).Set(x => x.Total, desired.Total)
                .Set(x => x.Status, desired.Status).Set(x => x.CompletedAt, desired.CompletedAt).Set(x => x.UpdatedAt, DateTime.UtcNow)
                .Inc(x => x.Version, 1);
            IpOrderDocument? updated = await imports.FindOneAndUpdateAsync(
                Builders<IpOrderDocument>.Filter.And(Builders<IpOrderDocument>.Filter.Eq(x => x.Id, id), VersionFilter<IpOrderDocument>(expectedVersion)),
                update, new FindOneAndUpdateOptions<IpOrderDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
            return updated is null ? null : Map(updated) with { Version = expectedVersion + 1 };
        }
        else
        {
            EpOrderDocument? current = await exports.Find(Builders<EpOrderDocument>.Filter.Eq(x => x.Id, id)).Limit(1).FirstOrDefaultAsync(cancellationToken);
            if (current is null) return null;
            EpOrderDocument desired = ToExport(order, current);
            UpdateDefinition<EpOrderDocument> update = Builders<EpOrderDocument>.Update
                .Set(x => x.OrderName, desired.OrderName).Set(x => x.Note, desired.Note).Set(x => x.UserName, desired.UserName)
                .Set(x => x.ProductList, desired.ProductList).Set(x => x.Images, desired.Images).Set(x => x.Total, desired.Total)
                .Set(x => x.Status, desired.Status).Set(x => x.CompletedAt, desired.CompletedAt).Set(x => x.UpdatedAt, DateTime.UtcNow)
                .Inc(x => x.Version, 1);
            EpOrderDocument? updated = await exports.FindOneAndUpdateAsync(
                Builders<EpOrderDocument>.Filter.And(Builders<EpOrderDocument>.Filter.Eq(x => x.Id, id), VersionFilter<EpOrderDocument>(expectedVersion)),
                update, new FindOneAndUpdateOptions<EpOrderDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
            return updated is null ? null : Map(updated) with { Version = expectedVersion + 1 };
        }
    }

    public async Task<bool> DeleteAsync(InventoryOrderKind kind, string id, int expectedVersion, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out ObjectId objectId)) return false;
        if (kind == InventoryOrderKind.Import)
        {
            DeleteResult result = await imports.DeleteOneAsync(Builders<IpOrderDocument>.Filter.And(
                Builders<IpOrderDocument>.Filter.Eq(x => x.Id, objectId), VersionFilter<IpOrderDocument>(expectedVersion)), cancellationToken);
            return result.DeletedCount == 1;
        }
        DeleteResult exported = await exports.DeleteOneAsync(Builders<EpOrderDocument>.Filter.And(
            Builders<EpOrderDocument>.Filter.Eq(x => x.Id, objectId), VersionFilter<EpOrderDocument>(expectedVersion)), cancellationToken);
        return exported.DeletedCount == 1;
    }

    private async Task<(IReadOnlyList<InventoryOrder>, long)> ListImportsAsync(InventoryOrderListQuery query, CancellationToken ct)
    {
        FilterDefinition<IpOrderDocument> filter = BuildImportFilter(query);
        List<IpOrderDocument> documents = await imports.Find(filter)
            .Sort(query.ByCompletedDate ? Builders<IpOrderDocument>.Sort.Descending(x => x.CompletedAt) : Builders<IpOrderDocument>.Sort.Descending(x => x.CreatedAt))
            .Skip((query.Page - 1) * 20).Limit(20).ToListAsync(ct);
        long total = await imports.CountDocumentsAsync(filter, cancellationToken: ct);
        return (documents.Select(Map).ToArray(), total);
    }

    private async Task<(IReadOnlyList<InventoryOrder>, long)> ListExportsAsync(InventoryOrderListQuery query, CancellationToken ct)
    {
        FilterDefinition<EpOrderDocument> filter = BuildExportFilter(query);
        List<EpOrderDocument> documents = await exports.Find(filter)
            .Sort(query.ByCompletedDate ? Builders<EpOrderDocument>.Sort.Descending(x => x.CompletedAt) : Builders<EpOrderDocument>.Sort.Descending(x => x.CreatedAt))
            .Skip((query.Page - 1) * 20).Limit(20).ToListAsync(ct);
        long total = await exports.CountDocumentsAsync(filter, cancellationToken: ct);
        return (documents.Select(Map).ToArray(), total);
    }

    private static FilterDefinition<IpOrderDocument> BuildImportFilter(InventoryOrderListQuery query)
    {
        FilterDefinitionBuilder<IpOrderDocument> b = Builders<IpOrderDocument>.Filter;
        FilterDefinition<IpOrderDocument> filter = b.Empty;
        if (!string.IsNullOrWhiteSpace(query.OrderName)) filter &= b.Regex(x => x.OrderName, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query.OrderName), "i"));
        if (!string.IsNullOrWhiteSpace(query.UserName)) filter &= b.Regex(x => x.UserName, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query.UserName), "i"));
        if (query.ByCompletedDate) filter &= b.Eq(x => x.Status, true); else if (query.Status.HasValue) filter &= b.Eq(x => x.Status, query.Status.Value);
        return AddDates(filter, query, b, x => x.CreatedAt, x => x.CompletedAt);
    }

    private static FilterDefinition<EpOrderDocument> BuildExportFilter(InventoryOrderListQuery query)
    {
        FilterDefinitionBuilder<EpOrderDocument> b = Builders<EpOrderDocument>.Filter;
        FilterDefinition<EpOrderDocument> filter = b.Empty;
        if (!string.IsNullOrWhiteSpace(query.OrderName)) filter &= b.Regex(x => x.OrderName, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query.OrderName), "i"));
        if (!string.IsNullOrWhiteSpace(query.UserName)) filter &= b.Regex(x => x.UserName, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query.UserName), "i"));
        if (query.ByCompletedDate) filter &= b.Eq(x => x.Status, true); else if (query.Status.HasValue) filter &= b.Eq(x => x.Status, query.Status.Value);
        return AddDates(filter, query, b, x => x.CreatedAt, x => x.CompletedAt);
    }

    private static FilterDefinition<T> AddDates<T>(FilterDefinition<T> filter, InventoryOrderListQuery query, FilterDefinitionBuilder<T> b,
        System.Linq.Expressions.Expression<Func<T, DateTime?>> created, System.Linq.Expressions.Expression<Func<T, DateTime?>> completed)
    {
        if (!query.StartDate.HasValue && !query.EndDate.HasValue) return filter;
        DateTime? start = query.StartDate?.UtcDateTime;
        DateTime? end = query.EndDate?.UtcDateTime;
        FilterDefinition<T> createdRange = b.Empty;
        FilterDefinition<T> completedRange = b.Empty;
        if (start.HasValue) { createdRange &= b.Gte(created, start.Value); completedRange &= b.Gte(completed, start.Value); }
        if (end.HasValue) { createdRange &= b.Lte(created, end.Value); completedRange &= b.Lte(completed, end.Value); }
        return query.ByCompletedDate
            ? filter & b.Or(completedRange, b.And(b.Eq(completed, null), createdRange))
            : filter & createdRange;
    }

    private async Task<InventoryOrder> EnrichAsync(InventoryOrder order, CancellationToken ct)
    {
        ObjectId[] ids = order.ProductList.Select(x => x.ProductId).Where(x => ObjectId.TryParse(x, out _)).Select(ObjectId.Parse).Distinct().ToArray();
        if (ids.Length == 0) return order;
        List<ProductDocument> found = await products.Find(Builders<ProductDocument>.Filter.In(x => x.Id, ids)).ToListAsync(ct);
        Dictionary<string, ProductDocument> map = found.ToDictionary(x => x.Id.ToString(), StringComparer.Ordinal);
        return order with
        {
            ProductList = order.ProductList.Select(line => map.TryGetValue(line.ProductId ?? string.Empty, out ProductDocument? product)
                ? line with { Name = product.Name ?? string.Empty, Brand = product.Brand ?? string.Empty, Image = product.Variants?.FirstOrDefault()?.ImageUrl ?? string.Empty }
                : line with { Name = string.Empty, Brand = string.Empty, Image = string.Empty }).ToArray(),
        };
    }

    private static InventoryOrder Map(IpOrderDocument x) => new(x.Id.ToString(), x.OrderName ?? string.Empty, x.Note ?? string.Empty, x.UserName ?? string.Empty,
        (x.ProductList ?? []).Select(line => new InventoryOrderLine(line.Status ?? false, line.ProductId, line.Price, null, null, line.Unit, ToInt(line.Quantity), ToDouble(line.QuantityRe), line.StockAppliedQuantity, false, line.Note, line.Vat, SubdocumentId: line.Id?.ToString())).ToArray(),
        (x.Images ?? []).ToArray(), x.Total ?? "0", x.Status ?? false, Offset(x.CompletedAt), Offset(x.CreatedAt), Offset(x.UpdatedAt), x.Version ?? 0, InventoryOrderKind.Import);

    private static InventoryOrder Map(EpOrderDocument x) => new(x.Id.ToString(), x.OrderName ?? string.Empty, x.Note ?? string.Empty, x.UserName ?? string.Empty,
        (x.ProductList ?? []).Select(line => new InventoryOrderLine(line.Status ?? false, line.ProductId, line.Price, line.ImportPriceSnapshot, line.ProfitPercent, line.Unit, ToInt(line.Quantity), ToDouble(line.ExportedQuantity), line.StockAppliedQuantity, line.StockUpdateSkipped ?? false, line.Note, line.Vat, SubdocumentId: line.Id?.ToString())).ToArray(),
        (x.Images ?? []).ToArray(), x.Total ?? "0", x.Status ?? false, Offset(x.CompletedAt), Offset(x.CreatedAt), Offset(x.UpdatedAt), x.Version ?? 0, InventoryOrderKind.Export);

    private static IpOrderDocument ToImport(InventoryOrder order, IpOrderDocument? current)
    {
        Dictionary<ObjectId, IpOrderLineDocument> existing = (current?.ProductList ?? []).Where(x => x.Id.HasValue).ToDictionary(x => x.Id!.Value);
        return new IpOrderDocument
        {
            Id = ObjectId.TryParse(order.Id, out ObjectId id) ? id : ObjectId.GenerateNewId(), Version = order.Version, OrderName = order.OrderName, Note = order.Note, UserName = order.UserName,
            ProductList = order.ProductList.Select(x => ToImportLine(x, existing)).ToList(), Images = order.Images.ToList(), Total = order.Total, Status = order.Status,
            CompletedAt = order.CompletedAt?.UtcDateTime, CreatedAt = order.CreatedAt?.UtcDateTime ?? DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
    }

    private static EpOrderDocument ToExport(InventoryOrder order, EpOrderDocument? current)
    {
        Dictionary<ObjectId, EpOrderLineDocument> existing = (current?.ProductList ?? []).Where(x => x.Id.HasValue).ToDictionary(x => x.Id!.Value);
        return new EpOrderDocument
        {
            Id = ObjectId.TryParse(order.Id, out ObjectId id) ? id : ObjectId.GenerateNewId(), Version = order.Version, OrderName = order.OrderName, Note = order.Note, UserName = order.UserName,
            ProductList = order.ProductList.Select(x => ToExportLine(x, existing)).ToList(), Images = order.Images.ToList(), Total = order.Total, Status = order.Status,
            CompletedAt = order.CompletedAt?.UtcDateTime, CreatedAt = order.CreatedAt?.UtcDateTime ?? DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
    }

    private static IpOrderLineDocument ToImportLine(InventoryOrderLine x, Dictionary<ObjectId, IpOrderLineDocument> existing)
    {
        ObjectId id = ObjectId.TryParse(x.SubdocumentId, out ObjectId parsed) ? parsed : ObjectId.GenerateNewId();
        existing.TryGetValue(id, out IpOrderLineDocument? original);
        return new IpOrderLineDocument { Id = id, ExtraElements = original?.ExtraElements, Status = x.Status, ProductId = x.ProductId, Price = x.Price, Unit = x.Unit, Quantity = x.Quantity, QuantityRe = x.ProgressQuantity, StockAppliedQuantity = x.StockAppliedQuantity, Note = x.Note, Vat = x.Vat };
    }

    private static EpOrderLineDocument ToExportLine(InventoryOrderLine x, Dictionary<ObjectId, EpOrderLineDocument> existing)
    {
        ObjectId id = ObjectId.TryParse(x.SubdocumentId, out ObjectId parsed) ? parsed : ObjectId.GenerateNewId();
        existing.TryGetValue(id, out EpOrderLineDocument? original);
        return new EpOrderLineDocument { Id = id, ExtraElements = original?.ExtraElements, Status = x.Status, ProductId = x.ProductId, Price = x.Price, ImportPriceSnapshot = x.ImportPriceSnapshot, ProfitPercent = x.ProfitPercent, Unit = x.Unit, Quantity = x.Quantity, ExportedQuantity = x.ProgressQuantity, StockAppliedQuantity = x.StockAppliedQuantity, StockUpdateSkipped = x.StockUpdateSkipped, Note = x.Note, Vat = x.Vat };
    }

    private static int ToInt(double? value) => value.HasValue && double.IsFinite(value.Value) ? Convert.ToInt32(value.Value) : 0;
    private static double ToDouble(double? value) => value.HasValue && double.IsFinite(value.Value) ? value.Value : 0;
    private static DateTimeOffset? Offset(DateTime? value) => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;

    private static FilterDefinition<T> VersionFilter<T>(int expectedVersion)
    {
        FilterDefinitionBuilder<T> builder = Builders<T>.Filter;
        return expectedVersion == 0
            ? builder.Or(builder.Eq("__v", 0), builder.Exists("__v", false), builder.Eq("__v", BsonNull.Value))
            : builder.Eq("__v", expectedVersion);
    }
}
