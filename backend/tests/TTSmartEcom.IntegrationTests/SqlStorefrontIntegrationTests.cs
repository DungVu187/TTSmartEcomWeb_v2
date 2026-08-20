using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Storefront;
using TTSmartEcom.Domain.Storefront;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Storefront;
using Xunit.Sdk;

namespace TTSmartEcom.IntegrationTests;

public sealed class SqlStorefrontIntegrationTests
{
    [Fact]
    public async Task StorefrontUpsert_SectionAndImageRemoval_PersistJsonConfiguration()
    {
        string? configuredConnection = Environment.GetEnvironmentVariable("TTSMART_SQL_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configuredConnection)) throw SkipException.ForSkip("Cần TTSMART_SQL_INTEGRATION_CONNECTION cho test SQL cô lập.");
        string databaseName = $"TTSmartEcomV2StorefrontIntegration_{Guid.NewGuid():N}";
        SqlConnectionStringBuilder master = new(configuredConnection) { InitialCatalog = "master" };
        SqlConnectionStringBuilder test = new(configuredConnection) { InitialCatalog = databaseName };
        try
        {
            await ExecuteAsync(master.ConnectionString, $"CREATE DATABASE [{databaseName}];");
            await ExecuteAsync(test.ConnectionString, "CREATE TABLE dbo.StorefrontSettings(StorefrontSettingsId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,ConfigurationJson nvarchar(max) NOT NULL,Version bigint NOT NULL,SourceUpdatedAtUtc datetime2(7) NULL);");
            var repository = new SqlStorefrontRepository(new TestConnectionFactory(test.ConnectionString));

            StorefrontContent saved = await repository.UpsertAsync(new StorefrontPatch(Introduction: "Giới thiệu", Partners: ["/images/partner.png"], TopPurchaseUrl: "/images/top.png"), CancellationToken.None);
            Assert.Equal("Giới thiệu", saved.Introduction);
            Assert.Contains("/images/partner.png", saved.Partners);

            StorefrontContent section = await repository.UpdateSectionAsync("section1", new StorefrontSectionPatch("Mục 1", new LocalizedText("VI", null, null), ["507f191e810c19729de860ea"], true, "/images/section.png", null), CancellationToken.None);
            Assert.Equal("Mục 1", section.Sections["section1"]!.Name);
            Assert.True(await repository.ContainsImageAsync("/images/section.png", CancellationToken.None));
            Assert.True(await repository.RemoveImageAsync("/images/partner.png", CancellationToken.None));

            StorefrontContent reloaded = Assert.IsType<StorefrontContent>(await repository.GetAsync(CancellationToken.None));
            Assert.Empty(reloaded.Partners);
            Assert.Equal("/images/section.png", reloaded.Sections["section1"]!.Image);
        }
        finally
        {
            await ExecuteAsync(master.ConnectionString, $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END");
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); await command.ExecuteNonQueryAsync();
    }

    private sealed class TestConnectionFactory(string connectionString) : ISqlConnectionFactory
    {
        public SqlConnection Create() => new(connectionString);
    }
}
