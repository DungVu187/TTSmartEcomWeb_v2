using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Domain.Orders;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Orders;

public sealed class MongoOrderRepository(IMongoDatabaseProvider databaseProvider) : IOrderRepository
{
    private static readonly Dictionary<string, SortDefinition<OrderDocument>> AllowedSorts =
        new Dictionary<string, SortDefinition<OrderDocument>>(StringComparer.Ordinal)
        {
            ["createdAt"] = Builders<OrderDocument>.Sort.Descending(x => x.CreatedAt),
            ["completedAt"] = Builders<OrderDocument>.Sort.Descending(x => x.CompletedAt),
        };
    private readonly IMongoCollection<OrderDocument> orders = databaseProvider.Database.GetCollection<OrderDocument>(OrderDocument.CollectionName);
    private readonly IMongoCollection<CounterDocument> counters = databaseProvider.Database.GetCollection<CounterDocument>(CounterDocument.CollectionName);

    public async Task<(IReadOnlyList<SalesOrder> Orders, long Total)> ListAsync(SalesOrderListQuery query, CancellationToken cancellationToken)
    {
        FilterDefinition<OrderDocument> filter = BuildFilter(query);
        int skip = checked((query.Page - 1) * query.Limit);
        List<OrderDocument> docs = await orders.Find(filter)
            .Sort(AllowedSorts.GetValueOrDefault(query.SortField, AllowedSorts["createdAt"]))
            .Skip(skip).Limit(query.Limit).ToListAsync(cancellationToken);
        long total = await orders.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        return (docs.Select(Map).ToArray(), total);
    }

    public async Task<IReadOnlyList<SalesOrder>> ListByPhoneAsync(string phone, string? state, CancellationToken cancellationToken)
    {
        FilterDefinition<OrderDocument> filter = Builders<OrderDocument>.Filter.Eq(x => x.UserPhone, phone);
        if (!string.IsNullOrWhiteSpace(state))
        {
            filter &= state.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
                ? Builders<OrderDocument>.Filter.Eq(x => x.State, "Cancelled")
                : Builders<OrderDocument>.Filter.And(
                    Builders<OrderDocument>.Filter.Eq(x => x.State, "Processing"),
                    Builders<OrderDocument>.Filter.Eq(x => x.Status, state));
        }
        List<OrderDocument> docs = await orders.Find(filter).Sort(Builders<OrderDocument>.Sort.Descending(x => x.CreatedAt)).ToListAsync(cancellationToken);
        return docs.Select(Map).ToArray();
    }

    public async Task<SalesOrder?> FindAsync(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out ObjectId objectId)) return null;
        OrderDocument? doc = await orders.Find(Builders<OrderDocument>.Filter.Eq(x => x.Id, objectId)).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return doc is null ? null : Map(doc);
    }

    public async Task<SalesOrder> InsertAsync(SalesOrder order, CancellationToken cancellationToken)
    {
        OrderDocument doc = ToDocument(order);
        await orders.InsertOneAsync(doc, cancellationToken: cancellationToken);
        return Map(doc);
    }

    public async Task<SalesOrder?> UpdateAsync(SalesOrder order, int expectedVersion, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(order.Id, out ObjectId id)) return null;
        OrderDocument? current = await orders.Find(Builders<OrderDocument>.Filter.Eq(x => x.Id, id)).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (current is null) return null;
        List<OrderCartItemDocument> cartItems = MergeCartItems(order.CartItems, current.CartItems ?? []);
        FilterDefinition<OrderDocument> filter = Builders<OrderDocument>.Filter.And(
            Builders<OrderDocument>.Filter.Eq(x => x.Id, id),
            VersionFilter(expectedVersion));
        UpdateDefinition<OrderDocument> update = Builders<OrderDocument>.Update
            .Set(x => x.OrderCode, order.OrderCode)
            .Set(x => x.UserPhone, order.UserPhone)
            .Set(x => x.UserName, order.UserName)
            .Set(x => x.CartItems, cartItems)
            .Set(x => x.Total, Convert.ToDouble(order.Total))
            .Set(x => x.Status, order.Status)
            .Set(x => x.Payment, order.Payment)
            .Set(x => x.State, order.State)
            .Set(x => x.CompletedAt, order.CompletedAt?.UtcDateTime)
            .Set(x => x.Images, order.Images.ToList())
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .Inc(x => x.Version, 1);
        OrderDocument? updated = await orders.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<OrderDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is null ? null : Map(updated);
    }

    public async Task<bool> DeleteAsync(string id, int expectedVersion, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out ObjectId objectId)) return false;
        DeleteResult result = await orders.DeleteOneAsync(Builders<OrderDocument>.Filter.And(
            Builders<OrderDocument>.Filter.Eq(x => x.Id, objectId),
            VersionFilter(expectedVersion),
            Builders<OrderDocument>.Filter.Ne(x => x.Status, "Completed")), cancellationToken);
        return result.DeletedCount == 1;
    }

    public async Task<long> CountProcessingAsync(CancellationToken cancellationToken) =>
        await orders.CountDocumentsAsync(Builders<OrderDocument>.Filter.And(
            Builders<OrderDocument>.Filter.Eq(x => x.State, "Processing"),
            Builders<OrderDocument>.Filter.Eq(x => x.Status, "Processing")), cancellationToken: cancellationToken);

    public async Task<long> NextOrderCodeAsync(CancellationToken cancellationToken)
    {
        CounterDocument counter = await counters.FindOneAndUpdateAsync(
            Builders<CounterDocument>.Filter.Eq(x => x.CounterId, "orderCode"),
            Builders<CounterDocument>.Update.Inc(x => x.Sequence, 1).SetOnInsert(x => x.CounterId, "orderCode"),
            new FindOneAndUpdateOptions<CounterDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, cancellationToken);
        return counter.Sequence ?? 1;
    }

    private static FilterDefinition<OrderDocument> BuildFilter(SalesOrderListQuery query)
    {
        FilterDefinitionBuilder<OrderDocument> b = Builders<OrderDocument>.Filter;
        FilterDefinition<OrderDocument> filter = b.Empty;
        if (!string.IsNullOrWhiteSpace(query.IdOrCode))
        {
            string escaped = RegexEscape(query.IdOrCode);
            FilterDefinition<OrderDocument> idFilter = ObjectId.TryParse(query.IdOrCode, out ObjectId id)
                ? b.Eq(x => x.Id, id)
                : b.Regex(x => x.OrderCode, new BsonRegularExpression(escaped, "i"));
            filter &= idFilter;
        }
        if (query.ByCompletedDate) filter &= b.Eq(x => x.Status, "Completed");
        else if (!string.IsNullOrWhiteSpace(query.Status)) filter &= b.Eq(x => x.Status, query.Status);
        if (query.Payment.HasValue) filter &= b.Eq(x => x.Payment, query.Payment.Value);
        if (!string.IsNullOrWhiteSpace(query.State)) filter &= b.Eq(x => x.State, query.State);
        if (!string.IsNullOrWhiteSpace(query.Phone)) filter &= b.Regex(x => x.UserPhone, new BsonRegularExpression(RegexEscape(query.Phone), "i"));
        if (!string.IsNullOrWhiteSpace(query.Name)) filter &= b.Regex(x => x.UserName, new BsonRegularExpression(RegexEscape(query.Name), "i"));
        if (query.StartDate.HasValue || query.EndDate.HasValue)
        {
            DateTime? start = query.StartDate?.UtcDateTime.Date;
            DateTime? end = query.EndDate?.UtcDateTime.Date.AddDays(1).AddTicks(-1);
            FilterDefinition<OrderDocument> date = b.Empty;
            if (start.HasValue) date &= b.Gte(x => x.CreatedAt, start.Value);
            if (end.HasValue) date &= b.Lte(x => x.CreatedAt, end.Value);
            if (query.ByCompletedDate)
            {
                FilterDefinition<OrderDocument> completed = b.Empty;
                if (start.HasValue) completed &= b.Gte(x => x.CompletedAt, start.Value);
                if (end.HasValue) completed &= b.Lte(x => x.CompletedAt, end.Value);
                filter &= b.Or(completed, b.And(b.Exists(x => x.CompletedAt, false), date), b.And(b.Eq(x => x.CompletedAt, null), date));
            }
            else filter &= date;
        }
        return filter;
    }

    private static string RegexEscape(string value) => System.Text.RegularExpressions.Regex.Escape(value);

    internal static SalesOrder Map(OrderDocument doc) => new(
        doc.Id.ToString(), doc.OrderCode, doc.UserPhone ?? string.Empty, doc.UserName,
        (doc.CartItems ?? []).Select(x => new SalesOrderItem(x.ProductId ?? string.Empty, ToInt(x.VariantIndex), ToInt(x.Quantity), x.Id?.ToString())).ToArray(),
        Convert.ToDecimal(doc.Total ?? 0), doc.Status ?? "Processing", doc.Payment ?? false, doc.State ?? "Processing",
        ToOffset(doc.CompletedAt), (doc.Images ?? []).ToArray(), ToOffset(doc.CreatedAt), ToOffset(doc.UpdatedAt), doc.Version ?? 0);

    internal static OrderDocument ToDocument(SalesOrder order)
    {
        ObjectId id = ObjectId.TryParse(order.Id, out ObjectId parsed) ? parsed : ObjectId.GenerateNewId();
        return new OrderDocument
        {
            Id = id, Version = order.Version, OrderCode = order.OrderCode, UserPhone = order.UserPhone, UserName = order.UserName,
            CartItems = order.CartItems.Select(x => new OrderCartItemDocument { Id = ObjectId.TryParse(x.SubdocumentId, out ObjectId lineId) ? lineId : ObjectId.GenerateNewId(), ProductId = x.ProductId, VariantIndex = x.VariantIndex, Quantity = x.Quantity }).ToList(),
            Total = Convert.ToDouble(order.Total), Status = order.Status, Payment = order.Payment, State = order.State,
            CompletedAt = order.CompletedAt?.UtcDateTime, Images = order.Images.ToList(), CreatedAt = order.CreatedAt?.UtcDateTime ?? DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
    }

    private static int ToInt(double? value) => value.HasValue && double.IsFinite(value.Value) ? Convert.ToInt32(value.Value) : 0;
    private static DateTimeOffset? ToOffset(DateTime? value) => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;

    private static FilterDefinition<OrderDocument> VersionFilter(int expectedVersion) => expectedVersion == 0
        ? Builders<OrderDocument>.Filter.Or(
            Builders<OrderDocument>.Filter.Eq(x => x.Version, 0),
            Builders<OrderDocument>.Filter.Exists(x => x.Version, false),
            Builders<OrderDocument>.Filter.Eq(x => x.Version, null))
        : Builders<OrderDocument>.Filter.Eq(x => x.Version, expectedVersion);

    private static List<OrderCartItemDocument> MergeCartItems(IReadOnlyList<SalesOrderItem> desired, IReadOnlyList<OrderCartItemDocument> current)
    {
        Dictionary<ObjectId, OrderCartItemDocument> byId = current
            .Where(x => x.Id.HasValue)
            .GroupBy(x => x.Id!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        return desired.Select(item =>
        {
            ObjectId id = ObjectId.TryParse(item.SubdocumentId, out ObjectId parsed) ? parsed : ObjectId.GenerateNewId();
            byId.TryGetValue(id, out OrderCartItemDocument? existing);
            return new OrderCartItemDocument
            {
                Id = id,
                ExtraElements = existing?.ExtraElements,
                ProductId = item.ProductId,
                VariantIndex = item.VariantIndex,
                Quantity = item.Quantity,
            };
        }).ToList();
    }
}
