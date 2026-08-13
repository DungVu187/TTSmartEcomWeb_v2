using System.Globalization;
using TTSmartEcom.Domain.Products;
using TTSmartEcom.Domain.Stations;
using TTSmartEcom.Domain.Users;

namespace TTSmartEcom.Application.Audit;

/// <summary>
/// Builds the legacy-compatible, deliberately allow-listed ActivityLog payloads.
/// Passwords, password hashes, login tokens, OTP values and provider secrets are
/// not accepted by this factory, so callers cannot accidentally persist them.
/// </summary>
public static class ActivityLogEntries
{
    private static readonly (string Field, Func<ProductRecord, object?> Value)[] ProductFields =
    [
        ("name", product => product.Name),
        ("code", product => product.Code),
        ("brand", product => product.Brand),
        ("type", product => product.Type),
        ("section", product => product.Section),
        ("value", product => product.Value),
        ("warranty", product => product.Warranty),
        ("vat", product => product.Vat),
        ("solution", product => product.Solution),
        ("description", product => product.Description),
        ("features", product => product.Features),
        ("operatingMethod", product => product.OperatingMethod),
        ("advantages", product => product.Advantages),
        ("specifications", product => product.Specifications),
    ];

    private static readonly (string Field, Func<ProductVariant, object?> Value)[] VariantFields =
    [
        ("price", variant => variant.Price),
        ("importPrice", variant => variant.ImportPrice),
        ("earn", variant => variant.Earn),
        ("note", variant => variant.Note),
        ("color", variant => variant.Color),
        ("shape", variant => variant.Shape),
        ("buttonCount", variant => variant.ButtonCount),
        ("frame", variant => variant.Frame),
    ];

    public static ActivityLogWriteEntry CreateProduct(string actor, ProductRecord product) =>
        Entry(actor, "create_product", product.Id, product.Name,
            new ActivityLogWriteDetail("Tạo mới", "", Text(product.Name)));

    public static ActivityLogWriteEntry? UpdateProduct(string actor, ProductRecord before, ProductRecord after)
    {
        List<ActivityLogWriteDetail> details = CompareFields(before, after, ProductFields);
        int count = Math.Max(before.Variants.Count, after.Variants.Count);
        for (int index = 0; index < count; index++)
        {
            ProductVariant? oldVariant = index < before.Variants.Count ? before.Variants[index] : null;
            ProductVariant? newVariant = index < after.Variants.Count ? after.Variants[index] : null;
            foreach ((string field, Func<ProductVariant, object?> value) in VariantFields)
            {
                string oldValue = oldVariant is null ? "" : Text(value(oldVariant));
                string newValue = newVariant is null ? "" : Text(value(newVariant));
                if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
                    details.Add(new ActivityLogWriteDetail($"variant[{index}].{field}", oldValue, newValue));
            }
        }
        return details.Count == 0 ? null : Entry(actor, "update_product", after.Id, after.Name, details);
    }

    public static ActivityLogWriteEntry DeleteProduct(string actor, ProductRecord product, bool bulk = false) =>
        Entry(actor, "delete_product", product.Id, product.Name,
            new ActivityLogWriteDetail(bulk ? "Xóa sản phẩm hàng loạt" : "Xóa sản phẩm", Text(product.Name), ""));

    public static ActivityLogWriteEntry ToggleProductDisplay(string actor, ProductRecord before, ProductRecord after) =>
        Entry(actor, "toggle_display", after.Id, after.Name,
            new ActivityLogWriteDetail("display", Display(before.Display), Display(after.Display)));

    public static ActivityLogWriteEntry AddVariant(string actor, ProductRecord product, ProductVariant variant, int index) =>
        Entry(actor, "add_variant", product.Id, product.Name,
            new ActivityLogWriteDetail($"variant[{index}]", "", $"Giá: {DefaultZero(variant.Price)}, Giá nhập: {DefaultZero(variant.ImportPrice)}"));

    public static ActivityLogWriteEntry? UpdateVariant(
        string actor, ProductRecord product, ProductVariant before, ProductVariant after, int index)
    {
        List<ActivityLogWriteDetail> details = CompareFields(before, after, VariantFields)
            .Select(detail => detail with { Field = $"variant[{index}].{detail.Field}" })
            .ToList();
        return details.Count == 0 ? null : Entry(actor, "update_variant", product.Id, product.Name, details);
    }

    public static ActivityLogWriteEntry DeleteVariant(string actor, ProductRecord product, ProductVariant variant, int index) =>
        Entry(actor, "delete_variant", product.Id, product.Name,
            new ActivityLogWriteDetail($"variant[{index}]",
                $"Giá: {DefaultZero(variant.Price)}, Giá nhập: {DefaultZero(variant.ImportPrice)}", ""));

    public static ActivityLogWriteEntry? UpdateEarn(
        string actor, ProductRecord product, ProductVariant before, ProductVariant after, int index)
    {
        List<ActivityLogWriteDetail> details = [];
        if (!Equal(before.Earn, after.Earn))
            details.Add(new($"variant[{index}].earn", $"{Text(before.Earn)}%", $"{Text(after.Earn)}%"));
        AddChanged(details, $"variant[{index}].price", before.Price, after.Price);
        return details.Count == 0 ? null : Entry(actor, "update_earn", product.Id, product.Name, details);
    }

    public static ActivityLogWriteEntry? UpdateImportPrice(
        string actor, ProductRecord product, ProductVariant before, ProductVariant after, int index)
    {
        List<ActivityLogWriteDetail> details = [];
        if (!Equal(before.ImportPrice, after.ImportPrice))
            details.Add(new($"variant[{index}].importPrice", DefaultZero(before.ImportPrice), Text(after.ImportPrice)));
        AddChanged(details, $"variant[{index}].price", before.Price, after.Price);
        return details.Count == 0 ? null : Entry(actor, "update_import_price", product.Id, product.Name, details);
    }

    public static ActivityLogWriteEntry CreateType(string actor, string? name, string? icon, bool includeIcon) =>
        Entry(actor, "create_type", null, name, includeIcon
            ? [new("Type", "", Text(name)), new("icon", "", Text(icon))]
            : [new("Type", "", Text(name))]);

    public static ActivityLogWriteEntry UpdateType(
        string actor, string? oldName, string? oldIcon, string? newName, string? newIcon) =>
        Entry(actor, "update_type", null, newName,
        [
            new("Type", Text(oldName), Text(newName)),
            new("icon", Text(oldIcon), Text(newIcon)),
        ]);

    public static ActivityLogWriteEntry DeleteType(string actor, string? name) =>
        Entry(actor, "delete_type", null, name, new ActivityLogWriteDetail("Type", Text(name), ""));

    public static ActivityLogWriteEntry CreateBrand(string actor, string? name) =>
        Entry(actor, "create_brand", null, name, new ActivityLogWriteDetail("Brand", "", Text(name)));

    public static ActivityLogWriteEntry DeleteBrand(string actor, string? name) =>
        Entry(actor, "delete_brand", null, name, new ActivityLogWriteDetail("Brand", Text(name), ""));

    public static ActivityLogWriteEntry CreateSection(string actor, string name) =>
        Entry(actor, "create_section", null, name, new ActivityLogWriteDetail("Phân loại", "", name));

    public static ActivityLogWriteEntry UpdateSection(string actor, string oldName, string newName) =>
        Entry(actor, "update_section", null, newName, new ActivityLogWriteDetail("Phân loại", oldName, newName));

    public static ActivityLogWriteEntry DeleteSection(string actor, string name) =>
        Entry(actor, "delete_section", null, name, new ActivityLogWriteDetail("Phân loại", name, ""));

    public static ActivityLogWriteEntry CreateSectionValue(string actor, string section, string value) =>
        Entry(actor, "create_section_value", null, $"{section}: {value}", new ActivityLogWriteDetail("Giá trị", "", value));

    public static ActivityLogWriteEntry UpdateSectionValue(
        string actor, string section, string oldValue, string newValue, string? oldImage, string? newImage)
    {
        List<ActivityLogWriteDetail> details = [new("value", oldValue, newValue)];
        if (!string.IsNullOrWhiteSpace(newImage) && !Equal(oldImage, newImage))
            details.Add(new("imgUrl", Text(oldImage), Text(newImage)));
        return Entry(actor, "update_section_value", null, $"{section}: {newValue}", details);
    }

    public static ActivityLogWriteEntry DeleteSectionValue(string actor, string section, string value) =>
        Entry(actor, "delete_section_value", null, $"{section}: {value}", new ActivityLogWriteDetail("Giá trị", value, ""));

    public static ActivityLogWriteEntry CreateUser(string actor, UserSummary user) =>
        Entry(actor, "create_user", null, UserLabel(user),
            new ActivityLogWriteDetail("Tạo tài khoản", "", $"{Text(user.Name)}, SĐT: {user.Phone}, Vai trò: {user.Role}"));

    public static ActivityLogWriteEntry? UpdateUserPermissions(string actor, UserSummary before, UserSummary after)
    {
        List<ActivityLogWriteDetail> details = UserDetails(before, after, includeAuthorization: true);
        return details.Count == 0 ? null : Entry(actor, "update_user_permissions", null, UserLabel(after), details);
    }

    public static ActivityLogWriteEntry RotateAutologinToken(string actor, UserSummary user) =>
        Entry(actor, "rotate_autologin_token", null, UserLabel(user),
            new ActivityLogWriteDetail("logInString", "Đã có token", "Đã xoay token mới"));

    public static ActivityLogWriteEntry ReplaceUserStations(
        string actor, UserSummary user, IReadOnlyList<string> before, IReadOnlyList<string> after) =>
        Entry(actor, "assign_user_stations", null, UserLabel(user),
            new ActivityLogWriteDetail("station", string.Join(", ", before), string.Join(", ", after)));

    public static ActivityLogWriteEntry AddUserStation(string actor, UserSummary user, string stationId) =>
        Entry(actor, "assign_user_stations", null, UserLabel(user),
            new ActivityLogWriteDetail("station", "", $"Đã gán thêm trạm: {stationId}"));

    public static ActivityLogWriteEntry DeleteUser(string actor, UserSummary user) =>
        Entry(actor, "delete_user", null, UserLabel(user),
            new ActivityLogWriteDetail("Xóa tài khoản", $"{Text(user.Name)}, SĐT: {user.Phone}, Vai trò: {user.Role}", ""));

    public static ActivityLogWriteEntry? UpdateUser(string actor, UserSummary before, UserSummary after)
    {
        List<ActivityLogWriteDetail> details = UserDetails(before, after, includeAuthorization: false);
        return details.Count == 0 ? null : Entry(actor, "update_user", null, UserLabel(after), details);
    }

    public static ActivityLogWriteEntry CreateStation(string actor, Station station) =>
        Entry(actor, "create_station", null, station.StationName,
            new ActivityLogWriteDetail("Tạo trạm", "", $"Mã trạm: {Text(station.StationCode)}, Địa điểm: {Text(station.Location)}"));

    public static ActivityLogWriteEntry UpdateStationProducts(string actor, Station before, Station after) =>
        Entry(actor, "update_station_products", null, after.StationName,
            new ActivityLogWriteDetail("productId", string.Join(", ", before.ProductIds), string.Join(", ", after.ProductIds)));

    public static ActivityLogWriteEntry? UpdateStation(string actor, Station before, Station after)
    {
        List<ActivityLogWriteDetail> details = [];
        AddChanged(details, "stationName", before.StationName, after.StationName);
        AddChanged(details, "stationCode", before.StationCode, after.StationCode);
        AddChanged(details, "location", before.Location, after.Location);
        if (before.AllowPublicSignup != after.AllowPublicSignup)
            details.Add(new("allowPublicSignup", Allowed(before.AllowPublicSignup), Allowed(after.AllowPublicSignup)));
        return details.Count == 0 ? null : Entry(actor, "update_station", null, after.StationName, details);
    }

    public static ActivityLogWriteEntry DeleteStation(string actor, Station station) =>
        Entry(actor, "delete_station", null, station.StationName,
            new ActivityLogWriteDetail("Xóa trạm", $"Mã trạm: {Text(station.StationCode)}", ""));

    public static ActivityLogWriteEntry Manage(string actor, string action, string targetName, params string[] safeFields) =>
        Entry(actor, action, null, targetName, safeFields.Length == 0
            ? [new("Cập nhật", "", "Thành công")]
            : [new("Thay đổi", "", $"Cập nhật các trường: {string.Join(", ", safeFields)}")]);

    private static List<ActivityLogWriteDetail> UserDetails(UserSummary before, UserSummary after, bool includeAuthorization)
    {
        List<ActivityLogWriteDetail> details = [];
        AddChanged(details, "name", before.Name, after.Name);
        AddChanged(details, "email", before.Email, after.Email);
        AddChanged(details, "phone", before.Phone, after.Phone);
        if (includeAuthorization)
        {
            AddChanged(details, "role", before.Role, after.Role);
            AddChanged(details, "functions", string.Join(", ", before.Functions), string.Join(", ", after.Functions));
            AddChanged(details, "permissions", string.Join(", ", before.Permissions), string.Join(", ", after.Permissions));
        }
        return details;
    }

    private static List<ActivityLogWriteDetail> CompareFields<T>(
        T before, T after, IReadOnlyList<(string Field, Func<T, object?> Value)> fields)
    {
        List<ActivityLogWriteDetail> details = [];
        foreach ((string field, Func<T, object?> value) in fields)
            AddChanged(details, field, value(before), value(after));
        return details;
    }

    private static void AddChanged(List<ActivityLogWriteDetail> details, string field, object? before, object? after)
    {
        if (!Equal(before, after)) details.Add(new(field, Text(before), Text(after)));
    }

    private static ActivityLogWriteEntry Entry(
        string actor, string action, string? productId, string? productName, ActivityLogWriteDetail detail) =>
        Entry(actor, action, productId, productName, [detail]);

    private static ActivityLogWriteEntry Entry(
        string actor, string action, string? productId, string? productName, IReadOnlyList<ActivityLogWriteDetail> details) =>
        new(actor, action, productId, productName, details);

    private static string UserLabel(UserSummary user) => string.IsNullOrWhiteSpace(user.Name) ? user.Phone : user.Name;
    private static string Display(bool? value) => value == true ? "Hiển thị" : "Ẩn";
    private static string Allowed(bool value) => value ? "Cho phép" : "Không";
    private static string DefaultZero(string? value) => string.IsNullOrEmpty(value) ? "0" : value;
    private static bool Equal(object? left, object? right) => string.Equals(Text(left), Text(right), StringComparison.Ordinal);
    private static string Text(object? value) => value switch
    {
        null => "",
        bool boolean => boolean ? "True" : "False",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "",
        _ => value.ToString() ?? "",
    };
}
