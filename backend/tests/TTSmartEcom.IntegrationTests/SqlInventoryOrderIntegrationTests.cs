using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Inventory;
using TTSmartEcom.Domain.Inventory;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Audit;
using TTSmartEcom.Infrastructure.SqlServer.Inventory;
using TTSmartEcom.Infrastructure.SqlServer.Orders;
using Xunit.Sdk;

namespace TTSmartEcom.IntegrationTests;

public sealed class SqlInventoryOrderIntegrationTests
{
    private const string ProductId = "507f191e810c19729de860ea";

    [Fact]
    public async Task EmptyImportOrder_CanAddAndCompleteLine_WithStockHistory()
    {
        string? configuredConnection = Environment.GetEnvironmentVariable("TTSMART_SQL_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            throw SkipException.ForSkip("Cần TTSMART_SQL_INTEGRATION_CONNECTION trỏ SQL Server local dành cho test cô lập.");
        }

        string databaseName = $"TTSmartEcomV2InventoryIntegration_{Guid.NewGuid():N}";
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
                SELECT NEWID(),N'507f191e810c19729de860eb',ProductId,0,N'10000',N'5000',10,10,NULL FROM dbo.Products WHERE PublicId=N'{ProductId}';
                """);

            var factory = new TestConnectionFactory(test.ConnectionString);
            var service = new InventoryOrderService(
                new SqlInventoryOrderRepository(factory),
                new SqlOrderStockPort(factory),
                new SqlStorageHistoryRepository(factory));

            InventoryOrder empty = await service.CreateAsync(InventoryOrderKind.Import, "Người kiểm thử", "Phiếu nhập", null, [], CancellationToken.None);
            Assert.Empty(empty.ProductList);
            InventoryOrder withLine = await service.AddLineAsync(InventoryOrderKind.Import, empty.Id,
                new InventoryOrderLineInput(ProductId, "10000", null, null, "cái", 5, 0, null, null), CancellationToken.None);
            InventoryOrderLine pending = Assert.Single(withLine.ProductList);
            Assert.False(pending.Status);
            Assert.Equal(0, pending.ProgressQuantity);

            InventoryOrder completed = await service.CompleteLineAsync(InventoryOrderKind.Import, withLine.Id, 0, true, "Người kiểm thử", CancellationToken.None);
            InventoryOrderLine completedLine = Assert.Single(completed.ProductList);
            Assert.True(completedLine.Status);
            Assert.Equal(5, completedLine.ProgressQuantity);
            Assert.Equal(5, completedLine.StockAppliedQuantity);

            (decimal sale, decimal storage, long operationCount, long movementCount) = await StateAsync(test.ConnectionString);
            Assert.Equal(15m, sale);
            Assert.Equal(15m, storage);
            Assert.Equal(1, operationCount);
            Assert.Equal(1, movementCount);
        }
        finally
        {
            await ExecuteAsync(master.ConnectionString, $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END");
        }
    }

    [Fact]
    public async Task EmptyExportOrder_CanAddAndCompleteLine_DecreasingBothStocks()
    {
        string? configuredConnection = Environment.GetEnvironmentVariable("TTSMART_SQL_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            throw SkipException.ForSkip("Cần TTSMART_SQL_INTEGRATION_CONNECTION trỏ SQL Server local dành cho test cô lập.");
        }

        string databaseName = $"TTSmartEcomV2InventoryIntegration_{Guid.NewGuid():N}";
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
                SELECT NEWID(),N'507f191e810c19729de860eb',ProductId,0,N'10000',N'5000',10,10,NULL FROM dbo.Products WHERE PublicId=N'{ProductId}';
                """);

            var factory = new TestConnectionFactory(test.ConnectionString);
            var service = new InventoryOrderService(new SqlInventoryOrderRepository(factory), new SqlOrderStockPort(factory), new SqlStorageHistoryRepository(factory));
            InventoryOrder empty = await service.CreateAsync(InventoryOrderKind.Export, "Người kiểm thử", "Phiếu xuất", null, [], CancellationToken.None);
            InventoryOrder withLine = await service.AddLineAsync(InventoryOrderKind.Export, empty.Id,
                new InventoryOrderLineInput(ProductId, null, "5000", 20, "cái", 5, 0, null, "0"), CancellationToken.None);
            Assert.False(Assert.Single(withLine.ProductList).Status);

            InventoryOrder completed = await service.CompleteLineAsync(InventoryOrderKind.Export, withLine.Id, 0, true, "Người kiểm thử", CancellationToken.None);
            Assert.True(Assert.Single(completed.ProductList).Status);
            (decimal sale, decimal storage, long operationCount, long movementCount) = await StateAsync(test.ConnectionString);
            Assert.Equal(5m, sale);
            Assert.Equal(5m, storage);
            Assert.Equal(1, operationCount);
            Assert.Equal(1, movementCount);
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
            Display bit NULL, IsDeleted bit NOT NULL, PurchaseCount bigint NOT NULL DEFAULT 0,
            Version bigint NOT NULL DEFAULT 0
        );
        CREATE TABLE dbo.ProductVariants (
            ProductVariantId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            ProductId uniqueidentifier NOT NULL, SortOrder int NOT NULL, PriceRaw nvarchar(200) NULL,
            ImportPriceRaw nvarchar(200) NULL, QuantityForSale decimal(19,6) NULL,
            QuantityInStorage decimal(19,6) NULL, DetailsJson nvarchar(max) NULL
        );
        CREATE TABLE dbo.InventoryOrders (
            InventoryOrderId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            Direction nvarchar(20) NOT NULL, OrderName nvarchar(200) NULL, Note nvarchar(2000) NULL,
            UserName nvarchar(160) NULL, Total decimal(19,4) NULL, TotalRaw nvarchar(200) NULL,
            Status bit NULL, TransactionDateUtc datetime2(7) NULL, CompletedAtUtc datetime2(7) NULL, ImagesJson nvarchar(max) NULL,
            SourceCreatedAtUtc datetime2(7) NULL, SourceUpdatedAtUtc datetime2(7) NULL, Version bigint NOT NULL
        );
        CREATE TABLE dbo.InventoryOrderItems (
            InventoryOrderItemId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            InventoryOrderId uniqueidentifier NOT NULL, ProductId uniqueidentifier NULL, ProductVariantId uniqueidentifier NULL,
            SourceProductId char(24) NULL, Price decimal(19,4) NULL, PriceRaw nvarchar(200) NULL,
            Vat decimal(19,4) NULL, VatRaw nvarchar(200) NULL, Quantity decimal(19,6) NULL,
            ProgressQuantity decimal(19,6) NULL, StockAppliedQuantity decimal(19,6) NULL,
            Unit nvarchar(100) NULL, Note nvarchar(2000) NULL, DetailsJson nvarchar(max) NULL,
            SortOrder int NOT NULL, Version bigint NOT NULL
        );
        CREATE TABLE dbo.StockOperations (
            StockOperationId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            OperationType nvarchar(100) NULL, SourceReference nvarchar(200) NULL, OccurredAtUtc datetime2(7) NULL, TransactionDateUtc datetime2(7) NULL,
            DetailsJson nvarchar(max) NULL, Version bigint NOT NULL
        );
        CREATE TABLE dbo.StockMovementLines (
            StockMovementLineId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            StockOperationId uniqueidentifier NOT NULL, ProductId uniqueidentifier NULL, SourceProductId char(24) NULL,
            Quantity decimal(19,6) NULL, DetailsJson nvarchar(max) NULL, SortOrder int NOT NULL, Version bigint NOT NULL
        );
        """;

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(decimal Sale, decimal Storage, long Operations, long Movements)> StateAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT (SELECT QuantityForSale FROM dbo.ProductVariants WHERE PublicId=N'507f191e810c19729de860eb'),(SELECT QuantityInStorage FROM dbo.ProductVariants WHERE PublicId=N'507f191e810c19729de860eb'),(SELECT COUNT(*) FROM dbo.StockOperations),(SELECT COUNT(*) FROM dbo.StockMovementLines);", connection);
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetDecimal(0), reader.GetDecimal(1), Convert.ToInt64(reader.GetValue(2), System.Globalization.CultureInfo.InvariantCulture), Convert.ToInt64(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture));
    }

    private sealed class TestConnectionFactory(string connectionString) : ISqlConnectionFactory
    {
        public SqlConnection Create() => new(new SqlConnectionStringBuilder(connectionString)
        {
            MultipleActiveResultSets = true,
        }.ConnectionString);
    }
}
