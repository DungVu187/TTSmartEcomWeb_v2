using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Inventory;
using TTSmartEcom.Domain.Inventory;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Audit;
using TTSmartEcom.Infrastructure.SqlServer.Inventory;
using TTSmartEcom.Infrastructure.SqlServer.Orders;
using TTSmartEcom.Infrastructure.SqlServer.Products;
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
                INSERT dbo.ProductBranchAssignments(ProductBranchAssignmentId,ProductId,BranchId,IsActive,AssignedAtUtc)
                SELECT NEWID(),ProductId,'22222222-2222-2222-2222-222222222222',1,SYSUTCDATETIME() FROM dbo.Products;
                INSERT dbo.BranchStockBalances(ProductVariantId,ProductId,ProductPublicId,ProductVariantPublicId,VariantPosition,QuantityForSale,QuantityInStorage,SourceVersion)
                SELECT v.ProductVariantId,v.ProductId,p.PublicId,v.PublicId,v.SortOrder,10,10,0 FROM dbo.ProductVariants v JOIN dbo.Products p ON p.ProductId=v.ProductId;
                INSERT dbo.BranchProductVariants(BranchProductVariantId,ProductId,ProductVariantId,PriceRaw,ImportPriceRaw,IsActive)
                SELECT NEWID(),ProductId,ProductVariantId,PriceRaw,ImportPriceRaw,1 FROM dbo.ProductVariants;
                """);

            var factory = new TestConnectionFactory(test.ConnectionString);
            var reader = new SqlBranchProductReader(factory, factory);
            var service = new InventoryOrderService(
                new SqlInventoryOrderRepository(factory, reader),
                new SqlOrderStockPort(factory, reader),
                new SqlStorageHistoryRepository(factory, reader));

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
                INSERT dbo.ProductBranchAssignments(ProductBranchAssignmentId,ProductId,BranchId,IsActive,AssignedAtUtc)
                SELECT NEWID(),ProductId,'22222222-2222-2222-2222-222222222222',1,SYSUTCDATETIME() FROM dbo.Products;
                INSERT dbo.BranchStockBalances(ProductVariantId,ProductId,ProductPublicId,ProductVariantPublicId,VariantPosition,QuantityForSale,QuantityInStorage,SourceVersion)
                SELECT v.ProductVariantId,v.ProductId,p.PublicId,v.PublicId,v.SortOrder,10,10,0 FROM dbo.ProductVariants v JOIN dbo.Products p ON p.ProductId=v.ProductId;
                INSERT dbo.BranchProductVariants(BranchProductVariantId,ProductId,ProductVariantId,PriceRaw,ImportPriceRaw,IsActive)
                SELECT NEWID(),ProductId,ProductVariantId,PriceRaw,ImportPriceRaw,1 FROM dbo.ProductVariants;
                """);

            var factory = new TestConnectionFactory(test.ConnectionString);
            var reader = new SqlBranchProductReader(factory, factory);
            var service = new InventoryOrderService(new SqlInventoryOrderRepository(factory, reader), new SqlOrderStockPort(factory, reader), new SqlStorageHistoryRepository(factory, reader));
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
        await using var command = new SqlCommand("SELECT (SELECT QuantityForSale FROM dbo.BranchStockBalances WHERE ProductVariantPublicId=N'507f191e810c19729de860eb'),(SELECT QuantityInStorage FROM dbo.BranchStockBalances WHERE ProductVariantPublicId=N'507f191e810c19729de860eb'),(SELECT COUNT(*) FROM dbo.StockOperations),(SELECT COUNT(*) FROM dbo.StockMovementLines);", connection);
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetDecimal(0), reader.GetDecimal(1), Convert.ToInt64(reader.GetValue(2), System.Globalization.CultureInfo.InvariantCulture), Convert.ToInt64(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture));
    }

    private sealed class TestConnectionFactory(string connectionString) : ISqlConnectionFactory, IOperationalDbConnectionFactory, ICompanyDbConnectionFactory
    {
        public SqlConnection Create() => new(new SqlConnectionStringBuilder(connectionString)
        {
            MultipleActiveResultSets = true,
        }.ConnectionString);
    }
}
