using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Cart;
using TTSmartEcom.Domain.Cart;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Cart;
using TTSmartEcom.Infrastructure.SqlServer.Products;
using Xunit.Sdk;

namespace TTSmartEcom.IntegrationTests;

public sealed class SqlCartIntegrationTests
{
    private const string UserId = "507f191e810c19729de860ea";
    private const string ProductId = "507f191e810c19729de860eb";

    [Fact]
    public async Task CartMutations_PersistAndFailedReplacementRollsBack()
    {
        string? configuredConnection = Environment.GetEnvironmentVariable("TTSMART_SQL_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            throw SkipException.ForSkip("Cần TTSMART_SQL_INTEGRATION_CONNECTION trỏ SQL Server local dành cho test cô lập.");
        }

        string databaseName = $"TTSmartEcomV2CartIntegration_{Guid.NewGuid():N}";
        SqlConnectionStringBuilder master = new(configuredConnection) { InitialCatalog = "master" };
        SqlConnectionStringBuilder test = new(configuredConnection) { InitialCatalog = databaseName };
        try
        {
            await ExecuteAsync(master.ConnectionString, $"CREATE DATABASE [{databaseName}];");
            await ExecuteAsync(test.ConnectionString, Schema);
            await ExecuteAsync(test.ConnectionString, $"""
                INSERT dbo.Users(UserId,PublicId,Phone,Name,Role,StationIdsJson,Version,IsDeleted)
                VALUES(NEWID(),N'{UserId}',N'0900000000',N'Test',N'customer',N'[]',0,0);
                INSERT dbo.Products(ProductId,PublicId,Name,BrandName,Code,Display,IsDeleted)
                VALUES(NEWID(),N'{ProductId}',N'Sản phẩm test',N'Brand',N'SP-TEST',1,0);
                INSERT dbo.ProductVariants(ProductVariantId,PublicId,ProductId,SortOrder,PriceRaw,QuantityForSale,QuantityInStorage,DetailsJson)
                SELECT NEWID(),N'507f191e810c19729de860ec',ProductId,0,N'10000',10,20,NULL FROM dbo.Products WHERE PublicId=N'{ProductId}';
                INSERT dbo.ProductBranchAssignments(ProductBranchAssignmentId,ProductId,BranchId,IsActive,AssignedAtUtc)
                SELECT NEWID(),ProductId,'22222222-2222-2222-2222-222222222222',1,SYSUTCDATETIME() FROM dbo.Products;
                INSERT dbo.BranchStockBalances(ProductVariantId,ProductId,ProductPublicId,ProductVariantPublicId,VariantPosition,QuantityForSale,QuantityInStorage)
                SELECT v.ProductVariantId,v.ProductId,p.PublicId,v.PublicId,v.SortOrder,10,20 FROM dbo.ProductVariants v JOIN dbo.Products p ON p.ProductId=v.ProductId;
                INSERT dbo.BranchProductVariants(BranchProductVariantId,ProductId,ProductVariantId,PriceRaw,IsActive)
                SELECT NEWID(),ProductId,ProductVariantId,PriceRaw,1 FROM dbo.ProductVariants;
                """);

            var factory = new TestConnectionFactory(test.ConnectionString);
            var repository = new SqlCartRepository(factory, new SqlBranchProductReader(factory, factory));
            var service = new CartService(repository, repository);
            IReadOnlyList<CartItem> added = await service.AddAsync(UserId, new CartChange(ProductId, 0, 2), CancellationToken.None);
            Assert.Single(added);
            Assert.Equal(2, added[0].Quantity);

            IReadOnlyList<CartItem> updated = await service.UpdateItemAsync(UserId, new CartChange(ProductId, 0, 3), CancellationToken.None);
            Assert.Equal(3, Assert.Single(updated).Quantity);
            IReadOnlyList<CartItem> statusUpdated = await service.UpdateStatusAsync(UserId, new CartChange(ProductId, 0, Status: true), CancellationToken.None);
            Assert.True(Assert.Single(statusUpdated).Status);

            CartOwner owner = Assert.IsType<CartOwner>(await repository.FindOwnerAsync(UserId, CancellationToken.None));
            CartItem original = Assert.Single(owner.Items);
            await Assert.ThrowsAsync<SqlException>(() => repository.ReplaceAsync(UserId,
            [
                original with { Id = "507f191e810c19729de860ed" },
                original with { Id = "507f191e810c19729de860ed" },
            ], owner.Version, CancellationToken.None));
            CartOwner afterRollback = Assert.IsType<CartOwner>(await repository.FindOwnerAsync(UserId, CancellationToken.None));
            Assert.Equal(original.Id, Assert.Single(afterRollback.Items).Id);

            IReadOnlyList<CartItem> cleared = await service.ClearAsync(UserId, CancellationToken.None);
            Assert.Empty(cleared);
            Assert.Empty(Assert.IsType<CartOwner>(await repository.FindOwnerAsync(UserId, CancellationToken.None)).Items);
        }
        finally
        {
            await ExecuteAsync(master.ConnectionString, $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END");
        }
    }

    private const string Schema = """
        CREATE TABLE dbo.Users (
            UserId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            Phone nvarchar(50) NULL, Name nvarchar(200) NULL, Role nvarchar(80) NULL,
            StationIdsJson nvarchar(max) NULL, Version bigint NOT NULL, IsDeleted bit NOT NULL
        );
        CREATE TABLE dbo.Products (
            ProductId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            Name nvarchar(500) NULL, BrandName nvarchar(300) NULL, Code nvarchar(200) NULL,
            Display bit NULL, IsDeleted bit NOT NULL
        );
        CREATE TABLE dbo.ProductVariants (
            ProductVariantId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            ProductId uniqueidentifier NOT NULL, SortOrder int NOT NULL, Name nvarchar(500) NULL, PriceRaw nvarchar(200) NULL,
            QuantityForSale decimal(19,6) NULL, QuantityInStorage decimal(19,6) NULL, DetailsJson nvarchar(max) NULL
        );
        CREATE TABLE dbo.CompanyDatabaseInfo(CompanyDatabaseInfoId uniqueidentifier NOT NULL PRIMARY KEY,SingletonKey tinyint NOT NULL,CompanyId uniqueidentifier NOT NULL,DatabaseKind nvarchar(40) NOT NULL);
        INSERT dbo.CompanyDatabaseInfo VALUES(NEWID(),1,'11111111-1111-1111-1111-111111111111',N'CompanyShared');
        CREATE TABLE dbo.BranchDatabaseInfo(BranchDatabaseInfoId uniqueidentifier NOT NULL PRIMARY KEY,SingletonKey tinyint NOT NULL,CompanyId uniqueidentifier NOT NULL,BranchId uniqueidentifier NOT NULL,DatabaseKind nvarchar(40) NOT NULL);
        INSERT dbo.BranchDatabaseInfo VALUES(NEWID(),1,'11111111-1111-1111-1111-111111111111','22222222-2222-2222-2222-222222222222',N'BranchOperational');
        CREATE TABLE dbo.ProductBranchAssignments(ProductBranchAssignmentId uniqueidentifier NOT NULL PRIMARY KEY,ProductId uniqueidentifier NOT NULL,BranchId uniqueidentifier NOT NULL,IsActive bit NOT NULL,AssignedAtUtc datetime2(7) NOT NULL);
        CREATE TABLE dbo.BranchStockBalances(ProductVariantId uniqueidentifier NOT NULL PRIMARY KEY,ProductId uniqueidentifier NOT NULL,ProductPublicId char(24) NOT NULL,ProductVariantPublicId char(24) NOT NULL,VariantPosition int NOT NULL,QuantityForSale decimal(19,6) NULL,QuantityInStorage decimal(19,6) NULL);
        CREATE TABLE dbo.BranchProductVariants(BranchProductVariantId uniqueidentifier NOT NULL PRIMARY KEY,ProductId uniqueidentifier NOT NULL,ProductVariantId uniqueidentifier NOT NULL UNIQUE,PriceRaw nvarchar(100) NULL,ImportPriceRaw nvarchar(100) NULL,IsActive bit NOT NULL);
        CREATE TABLE dbo.BranchProductStatistics(ProductId uniqueidentifier NOT NULL PRIMARY KEY,PurchaseCount bigint NOT NULL);
        CREATE TABLE dbo.CartItems (
            CartItemId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            UserId uniqueidentifier NOT NULL, ProductId uniqueidentifier NULL, ProductVariantId uniqueidentifier NULL,
            SourceProductId char(24) NULL, VariantIndex int NULL, Quantity decimal(19,6) NULL,
            Status bit NOT NULL, SortOrder int NOT NULL, Version bigint NOT NULL
        );
        """;

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TestConnectionFactory(string connectionString) : ISqlConnectionFactory, IOperationalDbConnectionFactory, ICompanyDbConnectionFactory
    {
        public SqlConnection Create() => new(new SqlConnectionStringBuilder(connectionString)
        {
            MultipleActiveResultSets = true,
        }.ConnectionString);
    }
}
