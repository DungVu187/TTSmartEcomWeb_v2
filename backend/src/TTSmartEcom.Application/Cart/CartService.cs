using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Domain.Cart;

namespace TTSmartEcom.Application.Cart;

public sealed class CartService(ICartRepository repository, ICartProductCatalog catalog) : ICartService
{
    public async Task<IReadOnlyList<CartItem>?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        CartOwner? owner = await repository.FindOwnerAsync(userId, cancellationToken);
        if (owner is null) return null;
        IReadOnlySet<string>? visible = await catalog.GetVisibleProductIdsAsync(owner, cancellationToken);
        List<CartItem> result = [];
        foreach (CartItem item in owner.Items)
        {
            ProductVariantSnapshot? variant = await catalog.FindVariantAsync(item.ProductId, item.VariantIndex, owner, cancellationToken);
            bool available = variant is not null && !IsContactOnly(variant) && (visible is null || visible.Contains(item.ProductId));
            result.Add(item with { Available = available });
        }
        return result;
    }

    public Task<IReadOnlyList<CartItem>> AddAsync(string userId, CartChange change, CancellationToken cancellationToken) => MutateAsync(userId, change, Mutation.Add, cancellationToken);
    public Task<IReadOnlyList<CartItem>> RemoveAsync(string userId, CartChange change, CancellationToken cancellationToken) => MutateAsync(userId, change, Mutation.Remove, cancellationToken);
    public Task<IReadOnlyList<CartItem>> UpdateItemAsync(string userId, CartChange change, CancellationToken cancellationToken) => MutateAsync(userId, change, Mutation.Update, cancellationToken);
    public Task<IReadOnlyList<CartItem>> UpdateStatusAsync(string userId, CartChange change, CancellationToken cancellationToken) => MutateAsync(userId, change, Mutation.Status, cancellationToken);

    public async Task<IReadOnlyList<CartItem>> ClearAsync(string userId, CancellationToken cancellationToken)
    {
        CartOwner owner = await RequireOwner(userId, cancellationToken);
        return await repository.ReplaceAsync(userId, [], owner.Version, cancellationToken);
    }

    private async Task<IReadOnlyList<CartItem>> MutateAsync(string userId, CartChange change, Mutation mutation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(change.ProductId) || change.VariantIndex < 0) throw Error(400, "Invalid cart item");
        CartOwner owner = await RequireOwner(userId, cancellationToken);
        List<CartItem> items = owner.Items.ToList();
        int index = items.FindIndex(x => x.ProductId == change.ProductId && x.VariantIndex == change.VariantIndex);
        ProductVariantSnapshot? variant = await catalog.FindVariantAsync(change.ProductId, change.VariantIndex, owner, cancellationToken);
        if (mutation is Mutation.Add or Mutation.Update or Mutation.Status && variant is null) throw Error(403, "Sản phẩm không khả dụng cho tài khoản này.");
        if (variant is not null && IsContactOnly(variant) && mutation is not Mutation.Remove) throw Error(409, "Sản phẩm này hiện chỉ nhận liên hệ.");
        if (mutation == Mutation.Add)
        {
            int quantity = Math.Max(1, change.Quantity ?? 1);
            if (index >= 0) items[index] = items[index] with { Quantity = checked(items[index].Quantity + quantity) };
            else items.Add(new CartItem(change.ProductId, change.VariantIndex, quantity));
        }
        else if (mutation == Mutation.Remove)
        {
            if (index >= 0) items.RemoveAt(index);
        }
        else if (index < 0) throw Error(404, "Cart item not found");
        else if (mutation == Mutation.Update) items[index] = items[index] with { Quantity = Math.Max(1, change.Quantity ?? 1) };
        else items[index] = items[index] with { Status = change.Status ?? false };
        return await repository.ReplaceAsync(userId, items, owner.Version, cancellationToken);
    }

    private async Task<CartOwner> RequireOwner(string userId, CancellationToken ct) => await repository.FindOwnerAsync(userId, ct) ?? throw Error(404, "User not found");
    private static bool IsContactOnly(ProductVariantSnapshot x) => x.Earn == 0 || ParseNumber(x.Price) <= 0 || x.QuantityForSale <= 0;
    private static decimal ParseNumber(string? value) => decimal.TryParse((value ?? "").Replace(".", "").Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal n) ? n : 0;
    private static TTSmartEcom.Application.Common.Errors.ApplicationException Error(int status, string message) => new(new ApplicationError($"TTS-CART-{status}", 4200 + status, status, message));
    private enum Mutation { Add, Remove, Update, Status }
}
