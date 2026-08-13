using System.ComponentModel.DataAnnotations;

namespace TTSmartEcom.Api.Contracts.Cart;

public sealed record CartChangeRequest(
    [property: Required] string ProductId,
    int VariantIndex,
    int? Quantity,
    bool? Status);
