using TTSmartEcom.Domain.Cart;

namespace TTSmartEcom.Application.Cart;

public interface ICartService
{
    Task<IReadOnlyList<CartItem>?> GetAsync(string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CartItem>> AddAsync(string userId, CartChange change, CancellationToken cancellationToken);
    Task<IReadOnlyList<CartItem>> RemoveAsync(string userId, CartChange change, CancellationToken cancellationToken);
    Task<IReadOnlyList<CartItem>> UpdateItemAsync(string userId, CartChange change, CancellationToken cancellationToken);
    Task<IReadOnlyList<CartItem>> UpdateStatusAsync(string userId, CartChange change, CancellationToken cancellationToken);
    Task<IReadOnlyList<CartItem>> ClearAsync(string userId, CancellationToken cancellationToken);
}

public interface ICartRepository
{
    Task<CartOwner?> FindOwnerAsync(string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CartItem>> ReplaceAsync(string userId, IReadOnlyList<CartItem> items, int? expectedVersion, CancellationToken cancellationToken);
    Task UpdateAfterCustomerOrderAsync(
        string userId,
        IReadOnlyList<CartItem> items,
        string? stationId,
        int expectedVersion,
        CancellationToken cancellationToken);
}

public sealed record CartOwner(string Id, string Phone, string? Name, string Role, IReadOnlyList<string> StationIds, IReadOnlyList<CartItem> Items, int Version);

public interface ICartProductCatalog
{
    Task<ProductVariantSnapshot?> FindVariantAsync(string productId, int variantIndex, CartOwner viewer, CancellationToken cancellationToken);
    Task<IReadOnlySet<string>?> GetVisibleProductIdsAsync(CartOwner viewer, CancellationToken cancellationToken);
}

public sealed record ProductVariantSnapshot(string ProductId, int VariantIndex, string? ProductName, string? Brand, string? Code, string? Price, string? ImageUrl, double QuantityForSale, double QuantityInStorage, double Earn, bool Display);
