using MongoDB.Bson;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Audit;

namespace TTSmartEcom.UnitTests.Audit;

public sealed class ActivityLogProjectionTests
{
    [Fact]
    public void LegacyActionLabels_IncludeChipAttributeActions()
    {
        Assert.Equal(
            "Thêm thuộc tính sản phẩm",
            MongoAuditRepository.LegacyActionLabels["add_chip_attr"]);
        Assert.Equal(
            "Xóa thuộc tính sản phẩm",
            MongoAuditRepository.LegacyActionLabels["remove_chip_attr"]);
    }

    [Fact]
    public void BuildProductReferenceLabel_PrefersCodeThenNameThenId()
    {
        ObjectId id = ObjectId.Parse("507f191e810c19729de860ea");

        Assert.Equal("SP-001", MongoAuditRepository.BuildProductReferenceLabel(new BsonDocument
        {
            ["_id"] = id,
            ["code"] = "SP-001",
            ["name"] = "Sản phẩm",
        }));
        Assert.Equal("Sản phẩm", MongoAuditRepository.BuildProductReferenceLabel(new BsonDocument
        {
            ["_id"] = id,
            ["name"] = "Sản phẩm",
        }));
        Assert.Equal(id.ToString(), MongoAuditRepository.BuildProductReferenceLabel(new BsonDocument
        {
            ["_id"] = id,
        }));
    }

    [Theory]
    [InlineData(" T01 ", " Trạm 01 ", "T01 - Trạm 01")]
    [InlineData("T01", "", "T01")]
    [InlineData("", "Trạm 01", "Trạm 01")]
    public void BuildStationReferenceLabel_CombinesAvailableTrimmedCodeAndName(
        string code,
        string name,
        string expected)
    {
        BsonDocument document = new()
        {
            ["_id"] = ObjectId.Parse("507f191e810c19729de860eb"),
            ["stationCode"] = code,
            ["stationName"] = name,
        };

        Assert.Equal(expected, MongoAuditRepository.BuildStationReferenceLabel(document));
    }
}
