using System.Text.Json;
using TTSmartEcom.Api.Contracts.Products;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.UnitTests.Products;

public sealed class ProductContractCompatibilityTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ProductEmbeddedIds_UseLegacyUnderscoreIdProperty()
    {
        string json = JsonSerializer.Serialize(
            new
            {
                Link = new ProductLink("link-id", "Tài liệu", "/documents/manual.pdf", "file"),
                Review = new ProductReview("review-id", "customer@example.test", "Tốt", 5, null),
            },
            WebJson);

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("link-id", document.RootElement.GetProperty("link").GetProperty("_id").GetString());
        Assert.Equal("review-id", document.RootElement.GetProperty("review").GetProperty("_id").GetString());
        Assert.False(document.RootElement.GetProperty("link").TryGetProperty("id", out _));
        Assert.False(document.RootElement.GetProperty("review").TryGetProperty("id", out _));
    }

    [Fact]
    public void ProductDocumentMutation_CarriesLegacyUnderscoreIdToApplication()
    {
        const string documentId = "507f191e810c19729de860ea";
        ProductMutationRequest? request = JsonSerializer.Deserialize<ProductMutationRequest>(
            $$"""{"documents":[{"_id":"{{documentId}}","label":"Manual","url":"/documents/manual.pdf","sourceType":"file"}]}""",
            WebJson);

        ProductLinkMutation document = Assert.Single(request!.ToMutation().Documents!);
        Assert.Equal(documentId, document.Id);
    }

    [Theory]
    [InlineData("\"\"", 25D)]
    [InlineData("\"30\"", 30D)]
    [InlineData("30", 30D)]
    public void EarnRequest_AcceptsLegacyBlankAndNumericScalarForms(string jsonValue, double expected)
    {
        ProductVariantMutationRequest? request = JsonSerializer.Deserialize<ProductVariantMutationRequest>(
            $"{{\"earn\":{jsonValue}}}", WebJson);

        Assert.NotNull(request);
        Assert.Equal(expected, request!.Earn);
    }

    [Theory]
    [InlineData("{\"value\":30}")]
    [InlineData("[30]")]
    [InlineData("true")]
    [InlineData("\"not-a-number\"")]
    public void EarnRequest_RejectsNonScalarOrNonNumericForms(string jsonValue)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProductVariantMutationRequest>(
            $"{{\"earn\":{jsonValue}}}", WebJson));
    }
}
