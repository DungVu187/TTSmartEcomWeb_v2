using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Files;
using TTSmartEcom.Domain.Inventory;

namespace TTSmartEcom.Api.Controllers.Inventory;

[Route("iporders")]
public sealed class ImportOrdersController(
    TTSmartEcom.Application.Inventory.IInventoryOrderService orders,
    IOptions<LegacyCompatibilityOptions> compatibility,
    LocalMediaFileService mediaFiles)
    : InventoryOrdersControllerBase(orders, compatibility, mediaFiles)
{
    protected override InventoryOrderKind Kind => InventoryOrderKind.Import;
    protected override string PermissionPrefix => "iporder";
}
