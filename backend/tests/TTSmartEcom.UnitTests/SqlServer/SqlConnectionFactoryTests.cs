using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TTSmartEcom.Infrastructure.SqlServer;

namespace TTSmartEcom.UnitTests.SqlServer;

public sealed class SqlConnectionFactoryTests
{
    [Fact]
    public void SplitTopologyFactories_UseTheirAssignedDatabase()
    {
        SqlServerOptions options = new()
        {
            ConnectionString = Connection("TTSmart_MAIN_online"),
            OperationalConnectionString = Connection("TTSmart_MAIN_online"),
            CompanyConnectionString = Connection("TTSmart"),
            ControlConnectionString = Connection("ttsmart.com.vn"),
        };
        IOptions<SqlServerOptions> configured = Options.Create(options);

        using SqlConnection operational = new OperationalDbConnectionFactory(configured).Create();
        using SqlConnection company = new CompanyDbConnectionFactory(configured).Create();
        using SqlConnection control = new ControlDbConnectionFactory(configured).Create();

        AssertConnection(operational, "TTSmart_MAIN_online");
        AssertConnection(company, "TTSmart");
        AssertConnection(control, "ttsmart.com.vn");
    }

    [Fact]
    public void CompanyConnection_FallsBackToLegacyConnectionForIsolatedTests()
    {
        SqlServerOptions options = new() { ConnectionString = Connection("TTSmartEcomV2Integration_Test") };

        Assert.Equal(options.ConnectionString, options.GetCompanyConnectionString());
    }

    private static string Connection(string database) =>
        $"Server=(local);Initial Catalog={database};Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

    private static void AssertConnection(SqlConnection connection, string expectedDatabase)
    {
        SqlConnectionStringBuilder builder = new(connection.ConnectionString);
        Assert.Equal(expectedDatabase, builder.InitialCatalog);
        Assert.True(builder.MultipleActiveResultSets);
    }
}
