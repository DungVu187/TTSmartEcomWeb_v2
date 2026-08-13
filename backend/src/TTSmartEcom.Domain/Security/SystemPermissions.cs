namespace TTSmartEcom.Domain.Security;

public static class SystemPermissions
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "product.view", "product.create", "product.edit", "product.delete",
        "order.view", "order.create", "order.edit", "order.delete", "order.excel", "order.scan_ai",
        "iporder.view", "iporder.create", "iporder.edit", "iporder.delete", "iporder.excel", "iporder.scan_ai",
        "eporder.view", "eporder.create", "eporder.edit", "eporder.delete", "eporder.excel", "eporder.scan_ai",
        "station.view", "station.create", "station.edit", "station.delete",
        "customer.view", "customer.create", "customer.edit", "customer.delete", "customer.assign_station",
        "storefront.manage", "voice.manage", "account.manage", "zalo.manage",
        "history_import.view", "history_export.view", "activitylog.view",
    };
}
