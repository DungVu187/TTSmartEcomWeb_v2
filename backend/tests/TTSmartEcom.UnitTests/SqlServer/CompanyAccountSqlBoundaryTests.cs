namespace TTSmartEcom.UnitTests.SqlServer;

public sealed class CompanyAccountSqlBoundaryTests
{
    [Fact]
    public void MembershipMutation_UsesSerializableTransactionLocksAndControlPlaneAudit()
    {
        string source = Read(
            "backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Security",
            "SqlCompanyAccountAdministrationRepository.cs");

        Assert.Contains("IsolationLevel.Serializable", source, StringComparison.Ordinal);
        Assert.Contains("WITH (UPDLOCK,HOLDLOCK)", source, StringComparison.Ordinal);
        Assert.Contains("UPDATE dbo.CompanyUsers", source, StringComparison.Ordinal);
        Assert.Contains("UPDATE dbo.UserRoles", source, StringComparison.Ordinal);
        Assert.Contains("INSERT dbo.AuditLogs", source, StringComparison.Ordinal);
        Assert.Contains("company.account.assign", source, StringComparison.Ordinal);
        Assert.Contains("company.account.update", source, StringComparison.Ordinal);
        Assert.Contains("company.account.revoke", source, StringComparison.Ordinal);
        Assert.Contains("transaction.CommitAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MembershipMutation_ValidatesCompanyRoleScopeAndNeverUsesLegacyPermissionJson()
    {
        string source = Read(
            "backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Security",
            "SqlCompanyAccountAdministrationRepository.cs");

        Assert.Contains("role.ScopeType != ControlPlaneScopeType.Company", source, StringComparison.Ordinal);
        Assert.Contains("role.CompanyId.Value != command.CompanyId", source, StringComparison.Ordinal);
        Assert.Contains("command.ActorCompanyPermissions.Contains", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CONGTY", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PermissionJson", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([RepositoryRoot(), .. segments]));

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "backend"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
