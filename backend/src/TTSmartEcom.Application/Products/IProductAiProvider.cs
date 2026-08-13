using System.Text.Json;

namespace TTSmartEcom.Application.Products;

public interface IProductAiProvider
{
    bool IsConfigured { get; }

    Task<ProductAiResult> AnalyzeInvoiceAsync(
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);

    Task<ProductAiResult> AnalyzeVoiceAsync(
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);
}

public sealed record ProductAiResult(ProductAiStatus Status, JsonElement Payload)
{
    public static ProductAiResult Success(JsonElement payload) =>
        new(ProductAiStatus.Success, payload.Clone());

    public static ProductAiResult Failure(ProductAiStatus status) =>
        new(status, default);
}

public enum ProductAiStatus
{
    Success,
    Unavailable,
    InvalidResponse,
}
