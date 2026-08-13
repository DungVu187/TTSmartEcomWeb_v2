using System.Text.Json.Serialization;
using TTSmartEcom.Domain.Catalog;

namespace TTSmartEcom.Api.Contracts.Catalog;

public sealed record BrandResponse(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("Brand")] string? Brand);

public sealed record SectionItemResponse(
    [property: JsonPropertyName("_id")] string? Id,
    string? Name,
    IReadOnlyList<string> Value,
    string? ImgUrl);

public sealed record SectionDocumentResponse(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("Section")] IReadOnlyList<SectionItemResponse> Sections);

public sealed record ManageResponse(bool Success, ManageRecord Data);
public sealed record PoliciesResponse(bool Success, IReadOnlyList<ManagePolicyRecord> Data);

public sealed record SectionImagesRequest(string[]? Names);

public static class CatalogContractMapper
{
    public static BrandResponse Map(BrandRecord value) => new(value.Id, value.Brand);

    public static SectionDocumentResponse Map(SectionDocumentRecord value) => new(value.Id,
        value.Sections.Select(section => new SectionItemResponse(section.Id, section.Name, section.Values, section.ImageUrl)).ToArray());
}
