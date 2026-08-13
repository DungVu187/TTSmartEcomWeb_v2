using System.Text.Json;
using TTSmartEcom.Domain.Audit;

namespace TTSmartEcom.ContractTests;

public sealed class ActivityLogProjectionContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ActivityLog_UsesLegacyUnderscoreIdAndReferenceEnvelope()
    {
        ActivityLogPage page = new(
            true,
            1,
            20,
            1,
            1,
            [new ActivityLog(
                "507f191e810c19729de860ea",
                "Quản trị viên",
                "add_chip_attr",
                null,
                "Sản phẩm kiểm thử",
                [],
                null,
                null)],
            new Dictionary<string, string>
            {
                ["add_chip_attr"] = "Thêm thuộc tính sản phẩm",
                ["remove_chip_attr"] = "Xóa thuộc tính sản phẩm",
            },
            new ActivityLogReferences(
                new Dictionary<string, string> { ["507f191e810c19729de860eb"] = "SP-001" },
                new Dictionary<string, string> { ["507f191e810c19729de860ec"] = "T01 - Trạm 01" }));

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(page, WebJson));
        JsonElement root = document.RootElement;
        JsonElement log = root.GetProperty("logs")[0];

        Assert.Equal("507f191e810c19729de860ea", log.GetProperty("_id").GetString());
        Assert.False(log.TryGetProperty("id", out _));
        Assert.Equal("Thêm thuộc tính sản phẩm", root.GetProperty("actionLabels").GetProperty("add_chip_attr").GetString());
        Assert.Equal("Xóa thuộc tính sản phẩm", root.GetProperty("actionLabels").GetProperty("remove_chip_attr").GetString());
        Assert.Equal("SP-001", root.GetProperty("references").GetProperty("products").GetProperty("507f191e810c19729de860eb").GetString());
        Assert.Equal("T01 - Trạm 01", root.GetProperty("references").GetProperty("stations").GetProperty("507f191e810c19729de860ec").GetString());
    }
}
