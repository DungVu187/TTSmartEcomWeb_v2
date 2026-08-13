namespace TTSmartEcom.Domain.Security;

public static class SystemRoles
{
    public const string SuperAdmin = "superadmin";
    public const string Admin = "admin";
    public const string Staff = "staff";
    public const string Customer = "customer";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        SuperAdmin,
        Admin,
        Staff,
        Customer,
    };
}
