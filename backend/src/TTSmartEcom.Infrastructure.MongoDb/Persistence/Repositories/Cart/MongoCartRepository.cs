using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Cart;
using TTSmartEcom.Domain.Cart;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Cart;

public sealed class MongoCartRepository(IMongoDatabaseProvider databaseProvider) : ICartRepository, ICartProductCatalog
{
    private readonly IMongoCollection<UserDocument> users = databaseProvider.Database.GetCollection<UserDocument>(UserDocument.CollectionName);
    private readonly IMongoCollection<ProductDocument> products = databaseProvider.Database.GetCollection<ProductDocument>(ProductDocument.CollectionName);
    private readonly IMongoCollection<StationDocument> stations = databaseProvider.Database.GetCollection<StationDocument>(StationDocument.CollectionName);

    public async Task<CartOwner?> FindOwnerAsync(string userId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(userId, out ObjectId id)) return null;
        UserDocument? user = await users.Find(Builders<UserDocument>.Filter.Eq(x => x.Id, id)).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return user is null ? null : MapOwner(user);
    }

    public async Task<IReadOnlyList<CartItem>> ReplaceAsync(string userId, IReadOnlyList<CartItem> items, int? expectedVersion, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(userId, out ObjectId id)) throw new InvalidOperationException("User not found");
        FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Eq(x => x.Id, id);
        if (expectedVersion.HasValue) filter &= Builders<UserDocument>.Filter.Eq(x => x.Version, expectedVersion.Value);
        UpdateDefinition<UserDocument> update = Builders<UserDocument>.Update
            .Set(x => x.Cart, items.Select(ToDocument).ToList())
            .Inc(x => x.Version, 1);
        FindOneAndUpdateOptions<UserDocument> options = new() { ReturnDocument = ReturnDocument.After };
        UserDocument? updated = await users.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        if (updated is null) throw new InvalidOperationException("Cart was changed by another request");
        return (updated.Cart ?? []).Select(MapItem).ToArray();
    }

    public async Task UpdateAfterCustomerOrderAsync(
        string userId,
        IReadOnlyList<CartItem> items,
        string? stationId,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(userId, out ObjectId id))
        {
            throw new InvalidOperationException("User not found");
        }

        FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.And(
            Builders<UserDocument>.Filter.Eq(x => x.Id, id),
            Builders<UserDocument>.Filter.Eq(x => x.Version, expectedVersion));
        UpdateDefinition<UserDocument> update = Builders<UserDocument>.Update
            .Set(x => x.Cart, items.Select(ToDocument).ToList())
            .Inc(x => x.Version, 1);
        if (!string.IsNullOrWhiteSpace(stationId))
        {
            update = update.AddToSet(x => x.Stations, stationId);
        }

        UpdateResult result = await users.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        if (result.MatchedCount != 1)
        {
            throw new InvalidOperationException("Cart was changed by another request");
        }
    }

    public async Task<ProductVariantSnapshot?> FindVariantAsync(string productId, int variantIndex, CartOwner viewer, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(productId, out ObjectId id) || variantIndex < 0) return null;
        ProductDocument? product = await products.Find(Builders<ProductDocument>.Filter.Eq(x => x.Id, id)).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (product is null || (viewer.Role == "customer" && product.Display != true)) return null;
        if (viewer.Role == "customer")
        {
            IReadOnlySet<string>? visible = await GetVisibleProductIdsAsync(viewer, cancellationToken);
            if (visible is not null && !visible.Contains(productId)) return null;
        }
        ProductVariantDocument? variant = product.Variants is not null && variantIndex < product.Variants.Count ? product.Variants[variantIndex] : null;
        return variant is null ? null : new ProductVariantSnapshot(productId, variantIndex, product.Name, product.Brand, product.Code, variant.Price, variant.ImageUrl, variant.QuantityForSale ?? 0, variant.QuantityInStorage ?? 0, variant.Earn ?? 25, product.Display ?? true);
    }

    public async Task<IReadOnlySet<string>?> GetVisibleProductIdsAsync(CartOwner viewer, CancellationToken cancellationToken)
    {
        if (viewer.Role is "superadmin" or "admin" or "staff") return null;
        if (viewer.StationIds.Count == 0) return null;
        List<ObjectId> stationIds = viewer.StationIds
            .Where(value => ObjectId.TryParse(value, out _))
            .Select(ObjectId.Parse)
            .ToList();
        if (stationIds.Count == 0) return new HashSet<string>(StringComparer.Ordinal);
        List<StationDocument> docs = await stations.Find(Builders<StationDocument>.Filter.In(x => x.Id, stationIds)).ToListAsync(cancellationToken);
        return docs.SelectMany(x => x.ProductIds ?? []).ToHashSet(StringComparer.Ordinal);
    }

    private static CartOwner MapOwner(UserDocument x) => new(x.Id.ToString(), x.Phone ?? string.Empty, x.Name, x.Role ?? "customer", (x.Stations ?? []).ToArray(), (x.Cart ?? []).Select(MapItem).ToArray(), x.Version ?? 0);
    private static UserCartItemDocument ToDocument(CartItem item) => new()
    {
        Id = ObjectId.TryParse(item.Id, out ObjectId id) ? id : ObjectId.GenerateNewId(),
        ProductId = item.ProductId,
        VariantIndex = item.VariantIndex,
        Quantity = item.Quantity,
        Status = item.Status,
    };
    private static CartItem MapItem(UserCartItemDocument x) => new(x.ProductId ?? string.Empty, ToInt(x.VariantIndex), Math.Max(1, ToInt(x.Quantity)), x.Status ?? true, Id: x.Id?.ToString());
    private static int ToInt(double? x) => x.HasValue && double.IsFinite(x.Value) ? Convert.ToInt32(x.Value) : 0;
}
