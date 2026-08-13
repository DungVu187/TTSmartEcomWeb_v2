using System.Text.Json.Serialization;

namespace TTSmartEcom.Domain.Cart;

public sealed record CartItem(
    string ProductId,
    int VariantIndex,
    int Quantity,
    bool Status = true,
    bool? Available = null,
    [property: JsonPropertyName("_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Id = null);

public sealed record CartChange(
    string ProductId,
    int VariantIndex,
    int? Quantity = null,
    bool? Status = null);
