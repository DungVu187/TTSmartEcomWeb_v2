using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Domain.Stations;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Stations;
using Xunit.Sdk;

namespace TTSmartEcom.IntegrationTests;

public sealed class SqlStationIntegrationTests
{
    [Fact]
    public async Task StationCrudAndProductAssignments_PersistToSql()
    {
        string? configuredConnection = Environment.GetEnvironmentVariable("TTSMART_SQL_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configuredConnection)) throw SkipException.ForSkip("Cần TTSMART_SQL_INTEGRATION_CONNECTION cho test SQL cô lập.");
        string databaseName = $"TTSmartEcomV2StationIntegration_{Guid.NewGuid():N}";
        SqlConnectionStringBuilder master = new(configuredConnection) { InitialCatalog = "master" };
        SqlConnectionStringBuilder test = new(configuredConnection) { InitialCatalog = databaseName };
        try
        {
            await ExecuteAsync(master.ConnectionString, $"CREATE DATABASE [{databaseName}];");
            await ExecuteAsync(test.ConnectionString, Schema);
            await ExecuteAsync(test.ConnectionString, "INSERT dbo.Products(ProductId,PublicId) VALUES(NEWID(),N'507f191e810c19729de860ea'),(NEWID(),N'507f191e810c19729de860eb');");
            var repository = new SqlStationRepository(new TestConnectionFactory(test.ConnectionString));

            Station created = Assert.IsType<Station>(await repository.CreateAsync(new NewStationData("Trạm A", "A01", "Hà Nội", true), CancellationToken.None));
            Station updated = Assert.IsType<Station>(await repository.UpdateAsync(created.Id, new UpdateStationData("Trạm B", null, "Hồ Chí Minh", false), CancellationToken.None));
            Assert.Equal("Trạm B", updated.StationName);
            Assert.False(updated.AllowPublicSignup);

            Station withImage = Assert.IsType<Station>(await repository.UpdateImageAsync(created.Id, "/station/a.png", CancellationToken.None));
            Assert.Equal("/station/a.png", withImage.ImageUrl);
            Station withProducts = Assert.IsType<Station>(await repository.UpdateProductsAsync(created.Id, ["507f191e810c19729de860ea", "507f191e810c19729de860eb"], CancellationToken.None));
            Assert.Equal(2, withProducts.ProductIds.Count);
            Assert.Equal(2, await CountAsync(test.ConnectionString, "SELECT COUNT(*) FROM dbo.StationProducts;"));

            Assert.True(await repository.DeleteAsync(created.Id, CancellationToken.None));
            Assert.Null(await repository.FindByIdAsync(created.Id, CancellationToken.None));
            Assert.Equal(0, await CountAsync(test.ConnectionString, "SELECT COUNT(*) FROM dbo.StationProducts;"));
        }
        finally
        {
            await ExecuteAsync(master.ConnectionString, $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END");
        }
    }

    private const string Schema = """
        CREATE TABLE dbo.Products(ProductId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE);
        CREATE TABLE dbo.Stations(StationId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,Name nvarchar(300) NULL,Code nvarchar(100) NULL,DetailsJson nvarchar(max) NULL,Version bigint NOT NULL);
        CREATE TABLE dbo.StationProducts(StationProductId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,StationId uniqueidentifier NOT NULL,ProductId uniqueidentifier NULL,SourceProductId char(24) NULL,SortOrder int NOT NULL,DetailsJson nvarchar(max) NULL,Version bigint NOT NULL);
        """;

    private static async Task ExecuteAsync(string connectionString, string sql) { await using var c=new SqlConnection(connectionString);await c.OpenAsync();await using var q=new SqlCommand(sql,c);await q.ExecuteNonQueryAsync(); }
    private static async Task<long> CountAsync(string connectionString,string sql) { await using var c=new SqlConnection(connectionString);await c.OpenAsync();await using var q=new SqlCommand(sql,c);return Convert.ToInt64(await q.ExecuteScalarAsync(),System.Globalization.CultureInfo.InvariantCulture); }
    private sealed class TestConnectionFactory(string connectionString) : ISqlConnectionFactory { public SqlConnection Create()=>new(new SqlConnectionStringBuilder(connectionString){MultipleActiveResultSets=true}.ConnectionString); }
}
