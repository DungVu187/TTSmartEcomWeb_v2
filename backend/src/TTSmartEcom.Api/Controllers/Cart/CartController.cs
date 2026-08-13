using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Contracts.Cart;
using TTSmartEcom.Application.Cart;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Abstractions.Authentication;

namespace TTSmartEcom.Api.Controllers.Cart;

[ApiController]
[Route("carts")]
[Authorize]
public sealed class CartController(ICartService carts) : ControllerBase
{
    [HttpGet("getCart")]
    public async Task<IActionResult> Get(CancellationToken ct) => await Execute(async id =>
    {
        var items = await carts.GetAsync(id, ct);
        return items is null ? NotFound(new { message = "User not found" }) : Ok(new { cart = items });
    });

    [HttpPost("addToCart")]
    public Task<IActionResult> Add(CartChangeRequest request, CancellationToken ct) => Mutate(request, carts.AddAsync, ct);

    [HttpPost("removeFromCart")]
    public Task<IActionResult> Remove(CartChangeRequest request, CancellationToken ct) => Mutate(request, carts.RemoveAsync, ct);

    [HttpPost("clearCart")]
    public async Task<IActionResult> Clear(CancellationToken ct) => await Execute(async id => Ok(new { cart = await carts.ClearAsync(id, ct) }));

    [HttpPut("updateCartItem")]
    public Task<IActionResult> Update(CartChangeRequest request, CancellationToken ct) => Mutate(request, carts.UpdateItemAsync, ct);

    [HttpPut("updateStatus")]
    public Task<IActionResult> Status(CartChangeRequest request, CancellationToken ct) => Mutate(request, carts.UpdateStatusAsync, ct);

    private async Task<IActionResult> Mutate(CartChangeRequest request, Func<string, Domain.Cart.CartChange, CancellationToken, Task<IReadOnlyList<Domain.Cart.CartItem>>> operation, CancellationToken ct) =>
        await Execute(async id => Ok(new { cart = await operation(id, new Domain.Cart.CartChange(request.ProductId, request.VariantIndex, request.Quantity, request.Status), ct) }));

    private async Task<IActionResult> Execute(Func<string, Task<IActionResult>> operation)
    {
        UserIdentitySnapshot? identity = HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
        return identity is null ? Unauthorized(new { message = "Access denied, no token provided" }) : await operation(identity.Id);
    }
}
