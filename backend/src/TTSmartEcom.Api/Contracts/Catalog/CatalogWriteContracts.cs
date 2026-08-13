using System.Text.Json.Serialization;

namespace TTSmartEcom.Api.Contracts.Catalog;

public sealed class BrandMutationRequest
{
    [JsonPropertyName("Brand")]
    public string? Brand { get; set; }
}

public sealed record ChipValueMutationRequest(string? Type, string? Value);
public sealed record SectionMutationRequest(string? Name);
public sealed record SectionValueMutationRequest(string? Value, string? OldValue, string? NewValue, string? ImgUrl);
