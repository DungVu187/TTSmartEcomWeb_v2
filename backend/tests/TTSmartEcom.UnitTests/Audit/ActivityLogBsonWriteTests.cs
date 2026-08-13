using MongoDB.Bson;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Mappings;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Audit;

namespace TTSmartEcom.UnitTests.Audit;

public sealed class ActivityLogBsonWriteTests
{
    [Fact]
    public void ToDocument_WritesExactLegacyShapeWithObjectIdsAndTimestamps()
    {
        LegacyMongoClassMaps.Register();
        DateTime now = new(2026, 8, 13, 4, 5, 6, DateTimeKind.Utc);
        ActivityLogWriteEntry entry = new(
            "Quản trị viên",
            "update_product",
            "507f191e810c19729de860ea",
            "Sản phẩm kiểm thử",
            [new ActivityLogWriteDetail("name", "Tên cũ", "Tên mới")]);

        ActivityLogDocument document = MongoAuditRepository.ToDocument(entry, now);
        BsonDocument bson = document.ToBsonDocument();

        Assert.True(bson["_id"].IsObjectId);
        Assert.Equal(0, bson["__v"].AsInt32);
        Assert.Equal("Quản trị viên", bson["userName"].AsString);
        Assert.Equal("update_product", bson["action"].AsString);
        Assert.Equal(ObjectId.Parse(entry.ProductId), bson["productId"].AsObjectId);
        Assert.Equal("Sản phẩm kiểm thử", bson["productName"].AsString);
        Assert.Equal(now, bson["createdAt"].ToUniversalTime());
        Assert.Equal(now, bson["updatedAt"].ToUniversalTime());

        BsonDocument detail = Assert.Single(bson["details"].AsBsonArray).AsBsonDocument;
        Assert.True(detail["_id"].IsObjectId);
        Assert.Equal("name", detail["field"].AsString);
        Assert.Equal("Tên cũ", detail["oldValue"].AsString);
        Assert.Equal("Tên mới", detail["newValue"].AsString);
    }

    [Fact]
    public void ToDocument_OmitsOptionalProductFieldsAndAppliesLegacyDetailDefaults()
    {
        LegacyMongoClassMaps.Register();
        ActivityLogWriteEntry entry = new(
            "Quản trị viên",
            "update_settings",
            null,
            null,
            [new ActivityLogWriteDetail(null, null, null)]);

        BsonDocument bson = MongoAuditRepository.ToDocument(entry, DateTime.UtcNow).ToBsonDocument();

        Assert.False(bson.Contains("productId"));
        Assert.False(bson.Contains("productName"));
        BsonDocument detail = Assert.Single(bson["details"].AsBsonArray).AsBsonDocument;
        Assert.False(detail.Contains("field"));
        Assert.Equal(string.Empty, detail["oldValue"].AsString);
        Assert.Equal(string.Empty, detail["newValue"].AsString);
    }

    [Fact]
    public void ToDocument_RejectsInvalidProductIdBeforePersistence()
    {
        ActivityLogWriteEntry entry = new(
            "Quản trị viên",
            "delete_product",
            "not-an-object-id",
            "Sản phẩm kiểm thử",
            []);

        Assert.Throws<ArgumentException>(() =>
            MongoAuditRepository.ToDocument(entry, DateTime.UtcNow));
    }

    [Fact]
    public void Repository_DeclaresLegacyRetentionWithoutCreatingIndexes()
    {
        Assert.Equal(TimeSpan.FromDays(90), MongoAuditRepository.LegacyRetention);
        Assert.Equal("activitylogs", ActivityLogDocument.CollectionName);
    }
}
