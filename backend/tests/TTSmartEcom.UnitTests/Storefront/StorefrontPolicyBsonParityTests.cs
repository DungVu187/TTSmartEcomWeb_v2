using MongoDB.Bson;
using TTSmartEcom.Domain.Storefront;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Storefront;

namespace TTSmartEcom.UnitTests.Storefront;

public sealed class StorefrontPolicyBsonParityTests
{
    [Fact]
    public void MapPolicy_ReadsLegacyTranslationsAndUpdatedAt()
    {
        DateTime updatedAt = new(2026, 7, 22, 0, 0, 0, DateTimeKind.Utc);
        BsonDocument fixture = PolicyFixture(updatedAt);

        StorefrontPolicy policy = MongoStorefrontRepository.MapPolicy(fixture);

        Assert.Equal("Mua hàng", policy.Translations.Vietnamese?.Title);
        Assert.Equal("购买政策", policy.Translations.Chinese?.Title);
        Assert.Equal("Purchase policy", policy.Translations.English?.Title);
        Assert.Equal("English content", Assert.Single(policy.Translations.English!.Sections).Content);
        Assert.Equal(new DateTimeOffset(updatedAt), policy.UpdatedAt);
    }

    [Fact]
    public void ToPolicy_PreservesTranslationsAndUpdatedAtDuringWrite()
    {
        DateTime updatedAt = new(2026, 7, 22, 0, 0, 0, DateTimeKind.Utc);
        StorefrontPolicy source = MongoStorefrontRepository.MapPolicy(PolicyFixture(updatedAt));

        BsonDocument written = MongoStorefrontRepository.ToPolicy(source);

        BsonDocument translations = written["translations"].AsBsonDocument;
        Assert.Equal("Mua hàng", translations["vi"]["title"].AsString);
        Assert.Equal("购买政策", translations["zh"]["title"].AsString);
        Assert.Equal("Purchase policy", translations["en"]["title"].AsString);
        Assert.Equal("English content", translations["en"]["sections"][0]["content"].AsString);
        Assert.Equal(updatedAt, written["updatedAt"].ToUniversalTime());
    }

    [Fact]
    public void ResolvePolicyTimestamps_PreservesUnchangedAndRefreshesChangedPolicies()
    {
        DateTimeOffset previous = new(2026, 7, 22, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
        StorefrontPolicy current = MongoStorefrontRepository.MapPolicy(PolicyFixture(previous.UtcDateTime));
        StorefrontPolicy unchangedWithoutTimestamp = current with { UpdatedAt = null };
        StorefrontPolicy changedWithoutTimestamp = current with { Summary = "Đã thay đổi", UpdatedAt = null };

        StorefrontPolicy unchanged = Assert.Single(MongoStorefrontRepository.ResolvePolicyTimestamps(
            [unchangedWithoutTimestamp], [current], now));
        StorefrontPolicy changed = Assert.Single(MongoStorefrontRepository.ResolvePolicyTimestamps(
            [changedWithoutTimestamp], [current], now));

        Assert.Equal(previous, unchanged.UpdatedAt);
        Assert.Equal(now, changed.UpdatedAt);
    }

    private static BsonDocument PolicyFixture(DateTime updatedAt) => new()
    {
        ["key"] = "purchase",
        ["title"] = "Mua hàng",
        ["summary"] = "Tóm tắt",
        ["sections"] = Sections("Nội dung tiếng Việt"),
        ["translations"] = new BsonDocument
        {
            ["vi"] = Content("Mua hàng", "Nội dung tiếng Việt"),
            ["zh"] = Content("购买政策", "中文内容"),
            ["en"] = Content("Purchase policy", "English content"),
        },
        ["updatedAt"] = updatedAt,
    };

    private static BsonDocument Content(string title, string content) => new()
    {
        ["title"] = title,
        ["summary"] = string.Empty,
        ["sections"] = Sections(content),
    };

    private static BsonArray Sections(string content) =>
    [
        new BsonDocument
        {
            ["title"] = "Mục",
            ["content"] = content,
        },
    ];
}
