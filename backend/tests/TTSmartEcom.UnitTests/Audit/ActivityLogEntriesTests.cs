using TTSmartEcom.Application.Audit;
using TTSmartEcom.Domain.Products;
using TTSmartEcom.Domain.Users;

namespace TTSmartEcom.UnitTests.Audit;

public sealed class ActivityLogEntriesTests
{
    private static readonly string?[] ProductUpdateFieldOrder =
        ["name", "code", "variant[0].price", "variant[0].earn"];
    private static readonly string?[] UserPermissionFieldOrder = ["name", "role", "permissions"];
    private static readonly string?[] TypeUpdateFieldOrder = ["Type", "icon"];

    [Fact]
    public void UpdateProduct_WhenTrackedValuesAreEqual_ReturnsNull()
    {
        ProductRecord product = Product();

        ActivityLogWriteEntry? entry = ActivityLogEntries.UpdateProduct("Quản trị", product, product);

        Assert.Null(entry);
    }

    [Fact]
    public void UpdateProduct_WritesTrackedFieldsInLegacyOrder()
    {
        ProductRecord before = Product(name: "Cũ", code: "OLD", variant: Variant(price: "100", earn: 20));
        ProductRecord after = Product(name: "Mới", code: "NEW", variant: Variant(price: "120", earn: 25));

        ActivityLogWriteEntry entry = Assert.IsType<ActivityLogWriteEntry>(
            ActivityLogEntries.UpdateProduct("Quản trị", before, after));

        Assert.Equal("update_product", entry.Action);
        Assert.Equal(ProductUpdateFieldOrder,
            entry.Details.Select(static detail => detail.Field));
    }

    [Fact]
    public void UpdateUserPermissions_DoesNotAcceptOrEmitSecretFields()
    {
        UserSummary before = User("Khách", "customer", ["product.view"]);
        UserSummary after = User("Nhân viên", "staff", ["product.edit"]);

        ActivityLogWriteEntry entry = Assert.IsType<ActivityLogWriteEntry>(
            ActivityLogEntries.UpdateUserPermissions("Quản trị", before, after));

        Assert.Equal(UserPermissionFieldOrder, entry.Details.Select(static detail => detail.Field));
        Assert.DoesNotContain(entry.Details, static detail =>
            detail.Field?.Contains("password", StringComparison.OrdinalIgnoreCase) == true ||
            detail.Field?.Contains("token", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void RotateAutologinToken_OnlyRecordsTokenState()
    {
        ActivityLogWriteEntry entry = ActivityLogEntries.RotateAutologinToken("Quản trị", User());

        ActivityLogWriteDetail detail = Assert.Single(entry.Details);
        Assert.Equal("logInString", detail.Field);
        Assert.Equal("Đã có token", detail.OldValue);
        Assert.Equal("Đã xoay token mới", detail.NewValue);
    }

    [Fact]
    public void Manage_UsesOnlyProvidedSafeFieldNames()
    {
        ActivityLogWriteEntry entry = ActivityLogEntries.Manage(
            "Quản trị", "update_policy", "Trang Chính sách", "mainPolicy");

        ActivityLogWriteDetail detail = Assert.Single(entry.Details);
        Assert.Equal("Thay đổi", detail.Field);
        Assert.Equal("Cập nhật các trường: mainPolicy", detail.NewValue);
    }

    [Fact]
    public void Manage_WithoutFields_UsesSanitizedSuccessDetail()
    {
        ActivityLogWriteEntry entry = ActivityLogEntries.Manage(
            "Quản trị", "update_settings", "Cấu hình chung");

        Assert.Equal(new ActivityLogWriteDetail("Cập nhật", "", "Thành công"), Assert.Single(entry.Details));
    }

    [Fact]
    public void UpdateType_PreservesLegacyTwoDetailShapeEvenForEqualValues()
    {
        ActivityLogWriteEntry entry = ActivityLogEntries.UpdateType(
            "Quản trị", "Thiết bị", "ri-box", "Thiết bị", "ri-box");

        Assert.Equal(TypeUpdateFieldOrder, entry.Details.Select(static detail => detail.Field));
    }

    private static ProductRecord Product(
        string name = "Sản phẩm", string code = "CODE", ProductVariant? variant = null) =>
        new("507f191e810c19729de860ea", "Loại", name, null, true, code, null, true,
            "Brand", "Section", "Value", [variant ?? Variant()], null, [], 0, [], 0, 0, 0,
            null, null, null, null, null, null, null, null, null, true);

    private static ProductVariant Variant(string price = "100", double earn = 20) =>
        new("507f191e810c19729de860eb", price, "80", earn, null, "Đỏ", "Tròn", "1", "Nhựa", 0, 0, null);

    private static UserSummary User(
        string name = "Khách", string role = "customer", IReadOnlyList<string>? permissions = null) =>
        new("507f191e810c19729de860ec", "safe@example.test", "0900000000", name, role,
            [], permissions ?? [], [], [], []);
}
