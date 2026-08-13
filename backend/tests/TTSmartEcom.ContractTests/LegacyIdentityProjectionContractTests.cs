using System.Text.Json;
using TTSmartEcom.Domain.Orders;
using TTSmartEcom.Domain.Users;

namespace TTSmartEcom.ContractTests;

public sealed class LegacyIdentityProjectionContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SalesOrderEnvelope_UsesLegacyUnderscoreIdsForOrderAndCartItems()
    {
        SalesOrder order = new(
            "507f191e810c19729de860ea",
            "ORD-1",
            "0900000000",
            "Khách tổng hợp",
            [new SalesOrderItem("507f191e810c19729de860eb", 2, 3, "507f191e810c19729de860ec")],
            123.45m,
            "Processing",
            false,
            "Processing",
            null,
            [],
            null,
            null,
            0);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new { order }, WebJson));
        JsonElement value = document.RootElement.GetProperty("order");

        Assert.Equal(order.Id, value.GetProperty("_id").GetString());
        Assert.False(value.TryGetProperty("id", out _));
        Assert.Equal(order.CartItems[0].SubdocumentId, value.GetProperty("cartItems")[0].GetProperty("_id").GetString());
        Assert.False(value.GetProperty("cartItems")[0].TryGetProperty("subdocumentId", out _));
    }

    [Fact]
    public void UserProfileAndSummary_UseLegacyUnderscoreIdsForNestedDocuments()
    {
        UserAddress address = new("507f191e810c19729de860ed", "Nhà", "Người nhận", "0900000000", "Địa chỉ", true);
        UserOrderTemplate template = new(
            "507f191e810c19729de860ee",
            "Mẫu 1",
            "Ghi chú",
            [new UserTemplateProduct("507f191e810c19729de860ef", 2)]);
        UserProfile profile = new(
            "507f191e810c19729de860ea",
            "user@example.test",
            "0900000000",
            "Người dùng",
            "customer",
            [],
            [],
            [],
            [address],
            [template]);

        UserSummary summary = new(
            profile.Id,
            profile.Email,
            profile.Phone,
            profile.Name,
            profile.Role,
            profile.Functions,
            profile.Permissions,
            profile.Stations,
            profile.Addresses,
            profile.OrderTemplates);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new { profile, user = summary }, WebJson));
        JsonElement profileValue = document.RootElement.GetProperty("profile");
        JsonElement summaryValue = document.RootElement.GetProperty("user");

        Assert.Equal(profile.Id, profileValue.GetProperty("_id").GetString());
        Assert.False(profileValue.TryGetProperty("id", out _));
        Assert.Equal(address.Id, profileValue.GetProperty("addresses")[0].GetProperty("_id").GetString());
        Assert.False(profileValue.GetProperty("addresses")[0].TryGetProperty("id", out _));
        Assert.Equal(template.Id, profileValue.GetProperty("orderTemplate")[0].GetProperty("_id").GetString());
        Assert.False(profileValue.GetProperty("orderTemplate")[0].TryGetProperty("id", out _));
        Assert.Equal(summary.Id, summaryValue.GetProperty("_id").GetString());
        Assert.False(summaryValue.TryGetProperty("id", out _));
    }
}
