using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Domain.Stations;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Stations;
using TTSmartEcom.Infrastructure.SqlServer.Products;
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
            await ExecuteAsync(test.ConnectionString, """
                INSERT dbo.Products(ProductId,PublicId,Name,Display,IsDeleted)
                VALUES(NEWID(),N'507f191e810c19729de860ea',N'A',1,0),(NEWID(),N'507f191e810c19729de860eb',N'B',1,0);
                INSERT dbo.ProductVariants(ProductVariantId,PublicId,ProductId,SortOrder,DetailsJson)
                SELECT NEWID(),CASE p.PublicId WHEN N'507f191e810c19729de860ea' THEN N'507f191e810c19729de860ec' ELSE N'507f191e810c19729de860ed' END,p.ProductId,0,N'{}' FROM dbo.Products p;
                INSERT dbo.ProductBranchAssignments(ProductBranchAssignmentId,ProductId,BranchId,IsActive,AssignedAtUtc)
                SELECT NEWID(),ProductId,'22222222-2222-2222-2222-222222222222',1,SYSUTCDATETIME() FROM dbo.Products;
                """);
            var factory = new TestConnectionFactory(test.ConnectionString);
            var repository = new SqlStationRepository(factory, new SqlBranchProductReader(factory, factory));

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
        CREATE TABLE dbo.Products(ProductId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,Name nvarchar(500) NULL,BrandName nvarchar(300) NULL,Code nvarchar(200) NULL,Display bit NULL,IsDeleted bit NOT NULL);
        CREATE TABLE dbo.ProductVariants(ProductVariantId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,ProductId uniqueidentifier NOT NULL,SortOrder int NOT NULL,Name nvarchar(500) NULL,PriceRaw nvarchar(200) NULL,DetailsJson nvarchar(max) NULL);
        CREATE TABLE dbo.CompanyDatabaseInfo(CompanyDatabaseInfoId uniqueidentifier NOT NULL PRIMARY KEY,SingletonKey tinyint NOT NULL,CompanyId uniqueidentifier NOT NULL,DatabaseKind nvarchar(40) NOT NULL);
        INSERT dbo.CompanyDatabaseInfo VALUES(NEWID(),1,'11111111-1111-1111-1111-111111111111',N'CompanyShared');
        CREATE TABLE dbo.BranchDatabaseInfo(BranchDatabaseInfoId uniqueidentifier NOT NULL PRIMARY KEY,SingletonKey tinyint NOT NULL,CompanyId uniqueidentifier NOT NULL,BranchId uniqueidentifier NOT NULL,DatabaseKind nvarchar(40) NOT NULL);
        INSERT dbo.BranchDatabaseInfo VALUES(NEWID(),1,'11111111-1111-1111-1111-111111111111','22222222-2222-2222-2222-222222222222',N'BranchOperational');
        CREATE TABLE dbo.ProductBranchAssignments(ProductBranchAssignmentId uniqueidentifier NOT NULL PRIMARY KEY,ProductId uniqueidentifier NOT NULL,BranchId uniqueidentifier NOT NULL,IsActive bit NOT NULL,AssignedAtUtc datetime2(7) NOT NULL);
        CREATE TABLE dbo.BranchStockBalances(ProductVariantId uniqueidentifier NOT NULL PRIMARY KEY,ProductId uniqueidentifier NOT NULL,ProductPublicId char(24) NOT NULL,ProductVariantPublicId char(24) NOT NULL,VariantPosition int NOT NULL,QuantityForSale decimal(19,6) NULL,QuantityInStorage decimal(19,6) NULL);
        CREATE TABLE dbo.BranchProductVariants(BranchProductVariantId uniqueidentifier NOT NULL PRIMARY KEY,ProductId uniqueidentifier NOT NULL,ProductVariantId uniqueidentifier NOT NULL UNIQUE,PriceRaw nvarchar(100) NULL,ImportPriceRaw nvarchar(100) NULL,IsActive bit NOT NULL);
        CREATE TABLE dbo.BranchProductStatistics(ProductId uniqueidentifier NOT NULL PRIMARY KEY,PurchaseCount bigint NOT NULL);
        CREATE TABLE dbo.Stations(StationId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,Name nvarchar(300) NULL,Code nvarchar(100) NULL,DetailsJson nvarchar(max) NULL,Version bigint NOT NULL);
        CREATE TABLE dbo.StationProducts(StationProductId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,StationId uniqueidentifier NOT NULL,ProductId uniqueidentifier NULL,SourceProductId char(24) NULL,SortOrder int NOT NULL,DetailsJson nvarchar(max) NULL,Version bigint NOT NULL);
        """;

    private static async Task ExecuteAsync(string connectionString, string sql) { await using var c=new SqlConnection(connectionString);await c.OpenAsync();await using var q=new SqlCommand(sql,c);await q.ExecuteNonQueryAsync(); }
    private static async Task<long> CountAsync(string connectionString,string sql) { await using var c=new SqlConnection(connectionString);await c.OpenAsync();await using var q=new SqlCommand(sql,c);return Convert.ToInt64(await q.ExecuteScalarAsync(),System.Globalization.CultureInfo.InvariantCulture); }
    private sealed class TestConnectionFactory(string connectionString) : ISqlConnectionFactory, IOperationalDbConnectionFactory, ICompanyDbConnectionFactory { public SqlConnection Create()=>new(new SqlConnectionStringBuilder(connectionString){MultipleActiveResultSets=true}.ConnectionString); }
}
