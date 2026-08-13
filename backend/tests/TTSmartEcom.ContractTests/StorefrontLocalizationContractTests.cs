using System.Text.Json;
using TTSmartEcom.Api.Contracts.Storefront;
using TTSmartEcom.Domain.Catalog;
using TTSmartEcom.Domain.Storefront;

namespace TTSmartEcom.ContractTests;

public sealed class StorefrontLocalizationContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void LocalizedText_UsesLegacyLocaleKeysForRequestsAndResponses()
    {
        const string json = """
            {
              "introduction": "Giới thiệu",
              "introductionTranslations": {
                "vi": "Tiếng Việt",
                "zh": "中文",
                "en": "English"
              }
            }
            """;

        StorefrontPatchRequest request = Assert.IsType<StorefrontPatchRequest>(
            JsonSerializer.Deserialize<StorefrontPatchRequest>(json, WebJson));
        Assert.Equal("Tiếng Việt", request.IntroductionTranslations?.Vietnamese);
        Assert.Equal("中文", request.IntroductionTranslations?.Chinese);
        Assert.Equal("English", request.IntroductionTranslations?.English);

        using JsonDocument serialized = JsonDocument.Parse(JsonSerializer.Serialize(
            request.IntroductionTranslations,
            WebJson));
        JsonElement root = serialized.RootElement;
        Assert.Equal("Tiếng Việt", root.GetProperty("vi").GetString());
        Assert.Equal("中文", root.GetProperty("zh").GetString());
        Assert.Equal("English", root.GetProperty("en").GetString());
        Assert.False(root.TryGetProperty("vietnamese", out _));
        Assert.False(root.TryGetProperty("chinese", out _));
        Assert.False(root.TryGetProperty("english", out _));
    }

    [Fact]
    public void StorefrontPolicyTranslations_UseLegacyLocaleKeys()
    {
        StorefrontPolicy policy = new(
            "purchase",
            "Chính sách mua hàng",
            null,
            [],
            new StorefrontPolicyTranslations(
                new StorefrontPolicyContent("Mua hàng", null, []),
                new StorefrontPolicyContent("购买政策", null, []),
                new StorefrontPolicyContent("Purchase policy", null, [])),
            null);

        using JsonDocument serialized = JsonDocument.Parse(JsonSerializer.Serialize(policy, WebJson));
        JsonElement translations = serialized.RootElement.GetProperty("translations");
        Assert.Equal("Mua hàng", translations.GetProperty("vi").GetProperty("title").GetString());
        Assert.Equal("购买政策", translations.GetProperty("zh").GetProperty("title").GetString());
        Assert.Equal("Purchase policy", translations.GetProperty("en").GetProperty("title").GetString());
        Assert.False(translations.TryGetProperty("vietnamese", out _));
    }

    [Fact]
    public void CatalogManageLocalization_UsesLegacyLocaleKeys()
    {
        LocalizedTextRecord translations = new("Tiếng Việt", "中文", "English");

        using JsonDocument serialized = JsonDocument.Parse(JsonSerializer.Serialize(translations, WebJson));
        JsonElement root = serialized.RootElement;
        Assert.Equal("Tiếng Việt", root.GetProperty("vi").GetString());
        Assert.Equal("中文", root.GetProperty("zh").GetString());
        Assert.Equal("English", root.GetProperty("en").GetString());
        Assert.False(root.TryGetProperty("vietnamese", out _));
    }
}
