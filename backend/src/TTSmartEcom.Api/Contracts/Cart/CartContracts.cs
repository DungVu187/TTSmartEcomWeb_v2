using System.ComponentModel.DataAnnotations;

namespace TTSmartEcom.Api.Contracts.Cart;

public sealed record CartChangeRequest(
    [param: Required] string ProductId,
    int VariantIndex,
    int? Quantity,
    bool? Status);
