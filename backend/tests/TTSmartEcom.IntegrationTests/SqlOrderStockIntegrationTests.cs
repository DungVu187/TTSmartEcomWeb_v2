using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Domain.Orders;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Orders;
using TTSmartEcom.Infrastructure.SqlServer.Products;
using Xunit.Sdk;

namespace TTSmartEcom.IntegrationTests;

public sealed class SqlOrderStockIntegrationTests
{
    private const string ProductId = "507f191e810c19729de860ea";
    private const string VariantId = "507f191e810c19729de860eb";

    [Fact]
    public async Task EmptyOrder_CanReceiveLine_AndFailedStockBatchRollsBack()
    {
        string? configuredConnection = Environment.GetEnvironmentVariable("TTSMART_SQL_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            throw SkipException.ForSkip("Cần TTSMART_SQL_INTEGRATION_CONNECTION trỏ SQL Server local dành cho test cô lập.");
        }

        string databaseName = $"TTSmartEcomV2OrdersIntegration_{Guid.NewGuid():N}";
        SqlConnectionStringBuilder master = new(configuredConnection) { InitialCatalog = "master" };
        SqlConnectionStringBuilder test = new(configuredConnection) { InitialCatalog = databaseName };
        try
        {
            await ExecuteAsync(master.ConnectionString, $"CREATE DATABASE [{databaseName}];");
            await ExecuteAsync(test.ConnectionString, Schema);
            await ExecuteAsync(test.ConnectionString, $"""
                INSERT dbo.Products(ProductId,PublicId,Name,BrandName,Code,Display,IsDeleted)
                VALUES(NEWID(),N'{ProductId}',N'Sản phẩm test',N'Brand',N'SP-TEST',1,0);
                INSERT dbo.ProductVariants(ProductVariantId,PublicId,ProductId,SortOrder,PriceRaw,ImportPriceRaw,QuantityForSale,QuantityInStorage,DetailsJson)
                SELECT NEWID(),N'{VariantId}',ProductId,0,N'10000',N'5000',10,20,NULL FROM dbo.Products WHERE PublicId=N'{ProductId}';
                INSERT dbo.ProductBranchAssignments(ProductBranchAssignmentId,ProductId,BranchId,IsActive,AssignedAtUtc)
                SELECT NEWID(),ProductId,'22222222-2222-2222-2222-222222222222',1,SYSUTCDATETIME() FROM dbo.Products;
                INSERT dbo.BranchStockBalances(ProductVariantId,ProductId,ProductPublicId,ProductVariantPublicId,VariantPosition,QuantityForSale,QuantityInStorage,SourceVersion)
                SELECT v.ProductVariantId,v.ProductId,p.PublicId,v.PublicId,v.SortOrder,10,20,0 FROM dbo.ProductVariants v JOIN dbo.Products p ON p.ProductId=v.ProductId;
                INSERT dbo.BranchProductVariants(BranchProductVariantId,ProductId,ProductVariantId,PriceRaw,ImportPriceRaw,IsActive)
                SELECT NEWID(),ProductId,ProductVariantId,PriceRaw,ImportPriceRaw,1 FROM dbo.ProductVariants;
                """);

            var factory = new TestConnectionFactory(test.ConnectionString);
            var reader = new SqlBranchProductReader(factory, factory);
            var orders = new SqlOrderRepository(factory, reader);
            var stock = new SqlOrderStockPort(factory, reader);
            SalesOrder draft = new(string.Empty, "TTS-01", string.Empty, string.Empty, [], 0, "Processing", false, "Processing", null, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0);

            SalesOrder created = await orders.InsertAsync(draft, CancellationToken.None);
            Assert.Empty(created.CartItems);
            SalesOrder persistedDraft = Assert.IsType<SalesOrder>(await orders.FindAsync(created.Id, CancellationToken.None));
            Assert.Empty(persistedDraft.CartItems);

            SalesOrder? updated = await orders.UpdateAsync(
                persistedDraft with { CartItems = [new SalesOrderItem(ProductId, 0, 2)], Total = 20_000 },
                persistedDraft.Version,
                CancellationToken.None);
            SalesOrder orderWithLine = Assert.IsType<SalesOrder>(updated);
            SalesOrder reloaded = Assert.IsType<SalesOrder>(await orders.FindAsync(orderWithLine.Id, CancellationToken.None));
            SalesOrderItem line = Assert.Single(reloaded.CartItems);
            Assert.Equal(ProductId, line.ProductId);
            Assert.Equal(2, line.Quantity);

            await stock.AdjustAsync([new StockAdjustment(ProductId, 0, -1, -2, 3, VariantId)], CancellationToken.None);
            (decimal saleAfterPurchase, decimal storageAfterPurchase, decimal purchaseCountAfterPurchase) = await QuantitiesAsync(test.ConnectionString);
            Assert.Equal(9m, saleAfterPurchase);
            Assert.Equal(18m, storageAfterPurchase);
            Assert.Equal(3m, purchaseCountAfterPurchase);

            await Assert.ThrowsAsync<TTSmartEcom.Application.Common.Errors.ApplicationException>(() => stock.AdjustAsync(
            [
                new StockAdjustment(ProductId, 0, -2, 0, ExpectedVariantId: VariantId),
                new StockAdjustment("507f191e810c19729de860ec", 0, -1, 0),
            ], CancellationToken.None));

            (decimal sale, decimal storage, decimal purchaseCount) = await QuantitiesAsync(test.ConnectionString);
            Assert.Equal(9m, sale);
            Assert.Equal(18m, storage);
            Assert.Equal(3m, purchaseCount);
        }
        finally
        {
            await ExecuteAsync(master.ConnectionString, $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END");
        }
    }

    private const string Schema = """
        CREATE TABLE dbo.Products (
            ProductId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            Name nvarchar(500) NULL, BrandName nvarchar(300) NULL, Code nvarchar(200) NULL,
            Display bit NULL, IsDeleted bit NOT NULL DEFAULT 0, PurchaseCount bigint NOT NULL DEFAULT 0,
            Version bigint NOT NULL DEFAULT 0
        );
        CREATE TABLE dbo.ProductVariants (
            ProductVariantId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            ProductId uniqueidentifier NOT NULL, SortOrder int NOT NULL, Name nvarchar(500) NULL, PriceRaw nvarchar(200) NULL,
            ImportPriceRaw nvarchar(200) NULL, QuantityForSale decimal(19,6) NULL,
            QuantityInStorage decimal(19,6) NULL, DetailsJson nvarchar(max) NULL
        );
        CREATE TABLE dbo.CompanyDatabaseInfo(CompanyDatabaseInfoId uniqueidentifier NOT NULL PRIMARY KEY,SingletonKey tinyint NOT NULL,CompanyId uniqueidentifier NOT NULL,DatabaseKind nvarchar(40) NOT NULL);
        INSERT dbo.CompanyDatabaseInfo VALUES(NEWID(),1,'11111111-1111-1111-1111-111111111111',N'CompanyShared');
        CREATE TABLE dbo.BranchDatabaseInfo(BranchDatabaseInfoId uniqueidentifier NOT NULL PRIMARY KEY,SingletonKey tinyint NOT NULL,CompanyId uniqueidentifier NOT NULL,BranchId uniqueidentifier NOT NULL,DatabaseKind nvarchar(40) NOT NULL);
        INSERT dbo.BranchDatabaseInfo VALUES(NEWID(),1,'11111111-1111-1111-1111-111111111111','22222222-2222-2222-2222-222222222222',N'BranchOperational');
        CREATE TABLE dbo.ProductBranchAssignments(ProductBranchAssignmentId uniqueidentifier NOT NULL PRIMARY KEY,ProductId uniqueidentifier NOT NULL,BranchId uniqueidentifier NOT NULL,IsActive bit NOT NULL,AssignedAtUtc datetime2(7) NOT NULL);
        CREATE TABLE dbo.BranchStockBalances(ProductVariantId uniqueidentifier NOT NULL PRIMARY KEY,ProductId uniqueidentifier NOT NULL,ProductPublicId char(24) NOT NULL,ProductVariantPublicId char(24) NOT NULL,VariantPosition int NOT NULL,QuantityForSale decimal(19,6) NULL,QuantityInStorage decimal(19,6) NULL,ProductCodeSnapshot nvarchar(200) NULL,ProductNameSnapshot nvarchar(1000) NULL,VariantNameSnapshot nvarchar(1000) NULL,SourceVersion bigint NOT NULL,CreatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),UpdatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME());
        CREATE TABLE dbo.BranchProductVariants(BranchProductVariantId uniqueidentifier NOT NULL PRIMARY KEY,ProductId uniqueidentifier NOT NULL,ProductVariantId uniqueidentifier NOT NULL UNIQUE,PriceRaw nvarchar(100) NULL,ImportPriceRaw nvarchar(100) NULL,IsActive bit NOT NULL);
        CREATE TABLE dbo.BranchProductStatistics(ProductId uniqueidentifier NOT NULL PRIMARY KEY,PurchaseCount bigint NOT NULL,UpdatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME());
        CREATE TABLE dbo.SalesOrders (
            SalesOrderId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            OrderCode nvarchar(100) NULL, CustomerPhoneSnapshot nvarchar(50) NULL,
            CustomerNameSnapshot nvarchar(200) NULL, Total decimal(19,4) NULL, TotalRaw nvarchar(200) NULL,
            Status nvarchar(100) NULL, Paid bit NULL, State nvarchar(100) NULL, CompletedAtUtc datetime2(7) NULL,
            ImagesJson nvarchar(max) NULL, SourceCreatedAtUtc datetime2(7) NULL,
            SourceUpdatedAtUtc datetime2(7) NULL, Version bigint NOT NULL
        );
        CREATE TABLE dbo.SalesOrderItems (
            SalesOrderItemId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            SalesOrderId uniqueidentifier NOT NULL, ProductId uniqueidentifier NULL, ProductVariantId uniqueidentifier NULL,
            SourceProductId char(24) NULL, VariantIndex int NULL, Quantity decimal(19,6) NULL,
            DetailsJson nvarchar(max) NULL, SortOrder int NOT NULL, Version bigint NOT NULL
        );
        CREATE TABLE dbo.NumberSequences (
            NumberSequenceId uniqueidentifier NOT NULL PRIMARY KEY, SequenceCode nvarchar(100) NOT NULL UNIQUE,
            NextValue bigint NOT NULL, Version bigint NOT NULL, UpdatedAtUtc datetime2(7) NULL
        );
        """;

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(decimal Sale, decimal Storage, decimal PurchaseCount)> QuantitiesAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT b.QuantityForSale,b.QuantityInStorage,CONVERT(decimal(19,6),COALESCE(s.PurchaseCount,0)) FROM dbo.BranchStockBalances b LEFT JOIN dbo.BranchProductStatistics s ON s.ProductId=b.ProductId WHERE b.ProductVariantPublicId=@id;", connection);
        command.Parameters.AddWithValue("@id", VariantId);
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetDecimal(0), reader.GetDecimal(1), reader.GetDecimal(2));
    }

    private sealed class TestConnectionFactory(string connectionString) : ISqlConnectionFactory, IOperationalDbConnectionFactory, ICompanyDbConnectionFactory
    {
        public SqlConnection Create() => new(new SqlConnectionStringBuilder(connectionString)
        {
            MultipleActiveResultSets = true,
        }.ConnectionString);
    }
}
