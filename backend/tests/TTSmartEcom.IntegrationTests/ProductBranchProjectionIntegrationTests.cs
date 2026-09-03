using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Cart;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Application.Security;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Domain.Cart;
using TTSmartEcom.Domain.Orders;
using TTSmartEcom.Domain.Products;
using TTSmartEcom.Domain.Security;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Cart;
using TTSmartEcom.Infrastructure.SqlServer.Orders;
using TTSmartEcom.Infrastructure.SqlServer.Products;
using Xunit.Sdk;
using TtsApplicationException = TTSmartEcom.Application.Common.Errors.ApplicationException;

namespace TTSmartEcom.IntegrationTests;

public sealed class ProductBranchProjectionIntegrationTests
{
    private const string ProductA = "507f191e810c19729de86101";
    private const string ProductB = "507f191e810c19729de86102";
    private const string VariantA = "507f191e810c19729de86201";
    private const string VariantB = "507f191e810c19729de86202";
    private const string UserId = "507f191e810c19729de86301";
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MainBranchId = Guid.Parse("22222222-2222-2222-2222-222222222221");
    private static readonly Guid HnBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SgBranchId = Guid.Parse("22222222-2222-2222-2222-222222222223");
    private static readonly Guid OtherCompanyId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherBranchId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task DistributionProjection_EnforcesCompanyAndBranchBoundaries()
    {
        string? configured = Environment.GetEnvironmentVariable("TTSMART_SQL_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configured))
            throw SkipException.ForSkip("Cần TTSMART_SQL_INTEGRATION_CONNECTION trỏ SQL Server test cô lập.");

        string suffix = Guid.NewGuid().ToString("N");
        string controlName = $"TTSmartEcomV2ControlPlaneIntegration_{suffix}";
        string companyName = $"TTSmartEcomV2CompanyIntegration_{suffix}";
        string mainName = $"TTSmartEcomV2_MAIN_{suffix}_online";
        string hnName = $"TTSmartEcomV2_HN_{suffix}_online";
        string sgName = $"TTSmartEcomV2_SG_{suffix}_online";
        SqlConnectionStringBuilder master = new(configured) { InitialCatalog = "master" };
        string[] databaseNames = [controlName, companyName, mainName, hnName, sgName];
        try
        {
            foreach (string database in databaseNames) await ExecuteAsync(master.ConnectionString, $"CREATE DATABASE [{database}];");
            string controlConnection = Connection(configured, controlName);
            string companyConnection = Connection(configured, companyName);
            string mainConnection = Connection(configured, mainName);
            string hnConnection = Connection(configured, hnName);
            string sgConnection = Connection(configured, sgName);
            await ExecuteAsync(controlConnection, ControlSchema);
            await ExecuteAsync(companyConnection, CompanySchema);
            await ExecuteAsync(mainConnection, BranchSchema(CompanyId, MainBranchId));
            await ExecuteAsync(hnConnection, BranchSchema(CompanyId, HnBranchId));
            await ExecuteAsync(sgConnection, BranchSchema(CompanyId, SgBranchId));
            await SeedControlAsync(controlConnection);
            await SeedCompanyAsync(companyConnection);

            string migration = ReadRepositoryFile("database", "sqlserver", "split", "005_CreateProductBranchAssignments.sql");
            string checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(migration)));
            string executableMigration = migration
                .Replace("$(ExpectedDatabaseName)", companyName, StringComparison.Ordinal)
                .Replace("$(CompanyId)", CompanyId.ToString(), StringComparison.Ordinal)
                .Replace("$(BranchId)", MainBranchId.ToString(), StringComparison.Ordinal)
                .Replace("$(CompanyCode)", "TTSmart", StringComparison.Ordinal)
                .Replace("$(BranchCode)", "MAIN", StringComparison.Ordinal)
                .Replace("$(ScriptChecksum)", checksum, StringComparison.Ordinal);
            await ExecuteAsync(companyConnection, executableMigration);
            await ExecuteAsync(companyConnection, executableMigration);
            Assert.Equal(2, await ScalarAsync(companyConnection, "SELECT COUNT(*) FROM dbo.ProductBranchAssignments WHERE BranchId='22222222-2222-2222-2222-222222222221' AND IsActive=1;"));

            FixedCompanyFactory companyFactory = new(companyConnection);
            ProductPage companyCatalog = await Catalog(companyFactory, mainConnection).ListAsync(Query(null), CancellationToken.None);
            Assert.Equal(2, companyCatalog.Total);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                Catalog(companyFactory, mainConnection).ListAsync(
                    Query(null) with { CompanyId = OtherCompanyId },
                    CancellationToken.None));
            ProductPage mainCatalog = await Catalog(companyFactory, mainConnection).ListAsync(Query(MainBranchId), CancellationToken.None);
            Assert.Equal(2, mainCatalog.Total);
            Assert.Empty((await Catalog(companyFactory, hnConnection).ListAsync(Query(HnBranchId), CancellationToken.None)).Products);
            Assert.Empty((await Catalog(companyFactory, sgConnection).ListAsync(Query(SgBranchId), CancellationToken.None)).Products);

            ProductBranchDistributionService distribution = new(
                new SqlProductBranchAssignmentRepository(companyFactory),
                new SqlCompanyBranchDirectory(new FixedControlFactory(controlConnection)),
                new AccessScopeService());
            CurrentUserContext actor = CompanyAdmin();
            ProductBranchAssignmentChange assignedHn = await distribution.AssignAsync([ProductA, ProductB], [HnBranchId], actor, CancellationToken.None);
            Assert.Equal(2, assignedHn.ChangedCount);
            ProductPage hnCatalog = await Catalog(companyFactory, hnConnection).ListAsync(Query(HnBranchId), CancellationToken.None);
            Assert.Equal(2, hnCatalog.Total);
            Assert.Empty((await Catalog(companyFactory, sgConnection).ListAsync(Query(SgBranchId), CancellationToken.None)).Products);
            ProductRecord productB = Assert.Single(hnCatalog.Products, product => product.Id == ProductB);
            Assert.Equal(0, Assert.Single(productB.Variants).QuantityForSale);

            await distribution.AssignAsync([ProductA], [SgBranchId], actor, CancellationToken.None);
            await SeedBalanceAsync(hnConnection, companyConnection, ProductA, 5);
            await SeedBalanceAsync(sgConnection, companyConnection, ProductA, 9);
            ProductRecord productAHn = Assert.Single((await Catalog(companyFactory, hnConnection).ListAsync(Query(HnBranchId), CancellationToken.None)).Products, product => product.Id == ProductA);
            ProductRecord productASg = Assert.Single((await Catalog(companyFactory, sgConnection).ListAsync(Query(SgBranchId), CancellationToken.None)).Products, product => product.Id == ProductA);
            Assert.Equal(5, Assert.Single(productAHn.Variants).QuantityForSale);
            Assert.Equal(9, Assert.Single(productASg.Variants).QuantityForSale);

            FixedOperationalFactory hnFactory = new(hnConnection);
            SqlBranchProductReader hnReader = new(companyFactory, hnFactory);
            SqlOrderStockPort hnStock = new(hnFactory, hnReader);
            await hnStock.AdjustAsync([new StockAdjustment(ProductA, 0, -2, -1, ExpectedVariantId: VariantA)], CancellationToken.None);
            Assert.Equal(3, await ScalarAsync(hnConnection, "SELECT CONVERT(int,QuantityForSale) FROM dbo.BranchStockBalances;"));
            Assert.Equal(9, await ScalarAsync(sgConnection, "SELECT CONVERT(int,QuantityForSale) FROM dbo.BranchStockBalances;"));

            SqlOrderRepository orders = new(hnFactory, hnReader);
            SalesOrder order = await orders.InsertAsync(new SalesOrder(
                string.Empty, "SO-SNAPSHOT", "0900000000", "Test", [new SalesOrderItem(ProductA, 0, 1)],
                10_000, "Processing", false, "Processing", null, [], DateTimeOffset.UtcNow, null, 0), CancellationToken.None);
            await ExecuteAsync(companyConnection, $"UPDATE dbo.Products SET Name=N'Tên mới' WHERE PublicId=N'{ProductA}';");
            Assert.Equal("Sản phẩm A", await StringScalarAsync(hnConnection, "SELECT JSON_VALUE(DetailsJson,'$.productNameSnapshot') FROM dbo.SalesOrderItems;"));
            SalesOrder loadedBeforeMetadataUpdate = (await orders.FindAsync(order.Id, CancellationToken.None))!;
            SalesOrder? metadataUpdated = await orders.UpdateAsync(
                loadedBeforeMetadataUpdate with { Payment = true },
                loadedBeforeMetadataUpdate.Version,
                CancellationToken.None);
            Assert.NotNull(metadataUpdated);
            Assert.Equal("Sản phẩm A", Assert.Single(metadataUpdated.CartItems).ProductNameSnapshot);
            Assert.Equal("Sản phẩm A", await StringScalarAsync(hnConnection, "SELECT JSON_VALUE(DetailsJson,'$.productNameSnapshot') FROM dbo.SalesOrderItems;"));

            ProductBranchAssignmentChange revoked = await distribution.RevokeAsync([ProductA], [HnBranchId], actor, CancellationToken.None);
            Assert.Equal(1, revoked.ChangedCount);
            Assert.Equal(3, await ScalarAsync(hnConnection, "SELECT CONVERT(int,QuantityForSale) FROM dbo.BranchStockBalances;"));
            Assert.Equal(1, await ScalarAsync(hnConnection, "SELECT COUNT(*) FROM dbo.SalesOrders;"));
            Assert.DoesNotContain((await Catalog(companyFactory, hnConnection).ListAsync(Query(HnBranchId), CancellationToken.None)).Products, product => product.Id == ProductA);
            await ExecuteAsync(companyConnection, executableMigration);
            Assert.False(await distribution.IsActiveAsync(ProductA, HnBranchId, actor, CancellationToken.None));
            Assert.DoesNotContain((await Catalog(companyFactory, hnConnection).ListAsync(Query(HnBranchId), CancellationToken.None)).Products, product => product.Id == ProductA);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => orders.InsertAsync(new SalesOrder(
                string.Empty, "SO-REVOKED", "0900000000", "Test", [new SalesOrderItem(ProductA, 0, 1)],
                10_000, "Processing", false, "Processing", null, [], DateTimeOffset.UtcNow, null, 0), CancellationToken.None));

            await ExecuteAsync(hnConnection, $"INSERT dbo.Users(UserId,PublicId,Phone,Name,Role,StationIdsJson,Version,IsDeleted) VALUES(NEWID(),N'{UserId}',N'0900000000',N'Test',N'customer',N'[]',0,0);");
            SqlCartRepository cartRepository = new(hnFactory, hnReader);
            CartService cart = new(cartRepository, cartRepository);
            TtsApplicationException cartError = await Assert.ThrowsAsync<TtsApplicationException>(() =>
                cart.AddAsync(UserId, new CartChange(ProductA, 0, 1), CancellationToken.None));
            Assert.Equal(403, cartError.Error.HttpStatus);

            ProductBranchAssignmentChange repeated = await distribution.AssignAsync([ProductB], [HnBranchId], actor, CancellationToken.None);
            Assert.Equal(0, repeated.ChangedCount);
            TtsApplicationException crossCompany = await Assert.ThrowsAsync<TtsApplicationException>(() =>
                distribution.AssignAsync([ProductA], [OtherBranchId], actor, CancellationToken.None));
            Assert.Equal(403, crossCompany.Error.HttpStatus);
            Assert.NotNull(await orders.FindAsync(order.Id, CancellationToken.None));

            SqlProductCatalogRepository hnProductCatalog = Catalog(companyFactory, hnConnection);
            SqlProductMutationRepository productMutations = new(
                companyFactory,
                hnFactory,
                hnProductCatalog,
                hnReader,
                hnStock);
            ProductMutationResult companyOnlyProduct = await productMutations.CreateAsync(
                ProductMutationFor("Sản phẩm tạo tại Company", "COMPANY-ONLY"),
                new ProductCreationAssignment(CompanyId, null, actor.UserId, "Admin"),
                CancellationToken.None);
            Assert.Equal(ProductMutationStatus.Success, companyOnlyProduct.Status);
            string companyOnlyId = companyOnlyProduct.Product!.Id;
            await distribution.AssignAsync([companyOnlyId], [HnBranchId], actor, CancellationToken.None);
            ProductMutationResult newVariant = await productMutations.AddVariantAsync(
                companyOnlyId,
                VariantMutation("22000"),
                CancellationToken.None);
            Assert.Equal(ProductMutationStatus.Success, newVariant.Status);
            ProductRecord assignedWithNewVariant = (await hnProductCatalog.FindByIdAsync(
                companyOnlyId,
                true,
                CompanyId,
                HnBranchId,
                CancellationToken.None))!;
            Assert.Equal(2, assignedWithNewVariant.Variants.Count);
            Assert.All(assignedWithNewVariant.Variants, variant => Assert.Equal(0, variant.QuantityForSale));

            ProductMutationResult branchCreatedProduct = await productMutations.CreateAsync(
                ProductMutationFor("Sản phẩm tạo tại Branch", "BRANCH-CREATE"),
                new ProductCreationAssignment(CompanyId, HnBranchId, actor.UserId, "Admin"),
                CancellationToken.None);
            Assert.Equal(ProductMutationStatus.Success, branchCreatedProduct.Status);
            Assert.True(await distribution.IsActiveAsync(
                branchCreatedProduct.Product!.Id,
                HnBranchId,
                actor,
                CancellationToken.None));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => productMutations.CreateAsync(
                ProductMutationFor("Không được tạo", "WRONG-COMPANY"),
                new ProductCreationAssignment(OtherCompanyId, null, actor.UserId, "Admin"),
                CancellationToken.None));
            Assert.Equal(0, await ScalarAsync(
                companyConnection,
                "SELECT COUNT(*) FROM dbo.Products WHERE Code=N'WRONG-COMPANY';"));
        }
        finally
        {
            foreach (string database in databaseNames.Reverse())
                await ExecuteAsync(master.ConnectionString, $"IF DB_ID(N'{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END");
        }
    }

    private static SqlProductCatalogRepository Catalog(FixedCompanyFactory company, string branchConnection)
    {
        FixedOperationalFactory branch = new(branchConnection);
        return new SqlProductCatalogRepository(company, new SqlBranchProductReader(company, branch));
    }

    private static ProductListQuery Query(Guid? branchId) => new(
        1, 100, null, null, null, null, null, null, "name", "asc", null, true,
        BranchId: branchId,
        CompanyId: CompanyId);

    private static CurrentUserContext CompanyAdmin()
    {
        IReadOnlySet<string> permissions = new HashSet<string>(["product.edit"], StringComparer.Ordinal);
        CompanyMembershipContext membership = new(CompanyId, "TTSmart", "TTSmart", Guid.NewGuid(), 1, ["company_admin"], permissions);
        return new CurrentUserContext(Guid.NewGuid(), true, false, "Admin", "admin@example.test", null,
            [membership], CompanyId, [], null, ["company_admin"], permissions, true, false);
    }

    private static ProductMutation ProductMutationFor(string name, string code) => new(
        "Chưa phân loại",
        name,
        code,
        "Chưa rõ",
        "Chưa phân loại",
        "Chưa rõ",
        null,
        null,
        false,
        true,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        [],
        [VariantMutation("11000")]);

    private static ProductVariantMutation VariantMutation(string price) => new(
        null,
        price,
        null,
        25,
        null,
        null,
        null,
        null,
        null,
        0,
        0,
        null);

    private static async Task SeedControlAsync(string connection) => await ExecuteAsync(connection, $"""
        INSERT dbo.Companies VALUES('{CompanyId}',N'TTSmart',N'TTSMART',1,0),('{OtherCompanyId}',N'OTHER',N'OTHER',1,0);
        INSERT dbo.Branches VALUES('{MainBranchId}','{CompanyId}',N'MAIN',1,0),('{HnBranchId}','{CompanyId}',N'HN',1,0),('{SgBranchId}','{CompanyId}',N'SG',1,0),('{OtherBranchId}','{OtherCompanyId}',N'OTHER',1,0);
        """);

    private static async Task SeedCompanyAsync(string connection) => await ExecuteAsync(connection, $$"""
        INSERT dbo.Products(ProductId,PublicId,Name,NameUnsigned,Code,Display,Adjusted,DetailsJson,DocumentsJson,PurchaseCount,Version,IsDeleted)
        VALUES('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1',N'{{ProductA}}',N'Sản phẩm A',N'san pham a',N'A',1,0,N'{}',N'[]',0,0,0),
              ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2',N'{{ProductB}}',N'Sản phẩm B',N'san pham b',N'B',1,0,N'{}',N'[]',0,0,0);
        INSERT dbo.ProductVariants(ProductVariantId,PublicId,ProductId,SortOrder,Name,Price,PriceRaw,ImportPrice,ImportPriceRaw,DetailsJson,Version)
        VALUES('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1',N'{{VariantA}}','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1',0,N'Mặc định',10000,N'10000',5000,N'5000',N'{"earn":25}',0),
              ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2',N'{{VariantB}}','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2',0,N'Mặc định',20000,N'20000',8000,N'8000',N'{"earn":25}',0);
        """);

    private static async Task SeedBalanceAsync(string branchConnection, string companyConnection, string productPublicId, int quantity)
    {
        await using SqlConnection company = new(companyConnection);
        await company.OpenAsync();
        await using SqlCommand lookup = new("SELECT p.ProductId,v.ProductVariantId,v.PublicId,v.SortOrder,p.Code,p.Name,v.Name,v.PriceRaw,v.ImportPriceRaw FROM dbo.Products p JOIN dbo.ProductVariants v ON v.ProductId=p.ProductId WHERE p.PublicId=@id;", company);
        lookup.Parameters.AddWithValue("@id", productPublicId);
        await using SqlDataReader reader = await lookup.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Guid productId = reader.GetGuid(0);
        Guid variantId = reader.GetGuid(1);
        string variantPublicId = reader.GetString(2);
        int position = reader.GetInt32(3);
        string? code = reader.IsDBNull(4) ? null : reader.GetString(4);
        string? name = reader.IsDBNull(5) ? null : reader.GetString(5);
        string? variantName = reader.IsDBNull(6) ? null : reader.GetString(6);
        string? price = reader.IsDBNull(7) ? null : reader.GetString(7);
        string? importPrice = reader.IsDBNull(8) ? null : reader.GetString(8);
        await reader.CloseAsync();
        await using SqlConnection branch = new(branchConnection);
        await branch.OpenAsync();
        await using SqlCommand command = new("""
            INSERT dbo.BranchStockBalances(ProductVariantId,ProductId,ProductPublicId,ProductVariantPublicId,VariantPosition,QuantityForSale,QuantityInStorage,ProductCodeSnapshot,ProductNameSnapshot,VariantNameSnapshot,SourceVersion)
            VALUES(@variantId,@productId,@productPublicId,@variantPublicId,@position,@quantity,@quantity,@code,@name,@variantName,0);
            INSERT dbo.BranchProductVariants(BranchProductVariantId,ProductId,ProductVariantId,PriceRaw,ImportPriceRaw,IsActive)
            VALUES(NEWID(),@productId,@variantId,@price,@importPrice,1);
            """, branch);
        command.Parameters.AddWithValue("@variantId", variantId);
        command.Parameters.AddWithValue("@productId", productId);
        command.Parameters.AddWithValue("@productPublicId", productPublicId);
        command.Parameters.AddWithValue("@variantPublicId", variantPublicId);
        command.Parameters.AddWithValue("@position", position);
        command.Parameters.AddWithValue("@quantity", quantity);
        command.Parameters.AddWithValue("@code", (object?)code ?? DBNull.Value);
        command.Parameters.AddWithValue("@name", (object?)name ?? DBNull.Value);
        command.Parameters.AddWithValue("@variantName", (object?)variantName ?? DBNull.Value);
        command.Parameters.AddWithValue("@price", (object?)price ?? DBNull.Value);
        command.Parameters.AddWithValue("@importPrice", (object?)importPrice ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private const string ControlSchema = """
        CREATE TABLE dbo.Companies(CompanyId uniqueidentifier NOT NULL PRIMARY KEY,CompanyCode nvarchar(64) NOT NULL,NormalizedCompanyCode nvarchar(64) NOT NULL,Status tinyint NOT NULL,IsDeleted bit NOT NULL);
        CREATE TABLE dbo.Branches(BranchId uniqueidentifier NOT NULL PRIMARY KEY,CompanyId uniqueidentifier NOT NULL,BranchCode nvarchar(64) NOT NULL,Status tinyint NOT NULL,IsDeleted bit NOT NULL);
        """;

    private const string CompanySchema = """
        CREATE TABLE dbo.SchemaVersions(SchemaVersionId uniqueidentifier NOT NULL PRIMARY KEY,MigrationNumber int NOT NULL UNIQUE,MigrationName nvarchar(300) NOT NULL,ScriptChecksum char(64) NULL);
        CREATE TABLE dbo.CompanyDatabaseInfo(CompanyDatabaseInfoId uniqueidentifier NOT NULL PRIMARY KEY,SingletonKey tinyint NOT NULL UNIQUE,CompanyId uniqueidentifier NOT NULL,CompanyCode nvarchar(64) NOT NULL,DatabaseKind nvarchar(40) NOT NULL);
        INSERT dbo.CompanyDatabaseInfo VALUES(NEWID(),1,'11111111-1111-1111-1111-111111111111',N'TTSmart',N'CompanyShared');
        CREATE TABLE dbo.Products(ProductId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,TypeName nvarchar(300) NULL,Name nvarchar(500) NULL,NameUnsigned nvarchar(500) NULL,Display bit NULL,Code nvarchar(200) NULL,VatRaw nvarchar(200) NULL,Adjusted bit NULL,BrandName nvarchar(300) NULL,CategoryName nvarchar(300) NULL,CategoryValue nvarchar(500) NULL,Description nvarchar(max) NULL,DetailsJson nvarchar(max) NULL,DocumentsJson nvarchar(max) NULL,PurchaseCount bigint NOT NULL DEFAULT 0,SourceCreatedAtUtc datetime2(7) NULL,SourceUpdatedAtUtc datetime2(7) NULL,Version bigint NOT NULL,IsDeleted bit NOT NULL DEFAULT 0);
        CREATE TABLE dbo.ProductVariants(ProductVariantId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,ProductId uniqueidentifier NOT NULL,SortOrder int NOT NULL,Name nvarchar(500) NULL,Price decimal(19,4) NULL,PriceRaw nvarchar(200) NULL,ImportPrice decimal(19,4) NULL,ImportPriceRaw nvarchar(200) NULL,QuantityForSale decimal(19,6) NULL,QuantityInStorage decimal(19,6) NULL,DetailsJson nvarchar(max) NULL,Version bigint NOT NULL,CONSTRAINT FK_Test_ProductVariants_Products FOREIGN KEY(ProductId) REFERENCES dbo.Products(ProductId));
        CREATE TABLE dbo.ProductTypes(ProductTypeId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,Name nvarchar(300) NULL,Icon nvarchar(300) NULL);
        CREATE TABLE dbo.ActivityLogs(ActivityLogId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,Action nvarchar(200) NOT NULL,ActorName nvarchar(200) NULL,DetailsJson nvarchar(max) NULL,CreatedAtUtc datetime2(7) NULL,Version bigint NOT NULL);
        """;

    private static string BranchSchema(Guid companyId, Guid branchId) => $"""
        CREATE TABLE dbo.BranchDatabaseInfo(BranchDatabaseInfoId uniqueidentifier NOT NULL PRIMARY KEY,SingletonKey tinyint NOT NULL UNIQUE,CompanyId uniqueidentifier NOT NULL,BranchId uniqueidentifier NOT NULL,DatabaseKind nvarchar(40) NOT NULL);
        INSERT dbo.BranchDatabaseInfo VALUES(NEWID(),1,'{companyId}','{branchId}',N'BranchOperational');
        CREATE TABLE dbo.BranchStockBalances(ProductVariantId uniqueidentifier NOT NULL PRIMARY KEY,ProductId uniqueidentifier NOT NULL,ProductPublicId char(24) NOT NULL,ProductVariantPublicId char(24) NOT NULL,VariantPosition int NOT NULL,QuantityForSale decimal(19,6) NULL,QuantityInStorage decimal(19,6) NULL,ProductCodeSnapshot nvarchar(200) NULL,ProductNameSnapshot nvarchar(1000) NULL,VariantNameSnapshot nvarchar(1000) NULL,SourceVersion bigint NOT NULL,CreatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),UpdatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME());
        CREATE TABLE dbo.BranchProductVariants(BranchProductVariantId uniqueidentifier NOT NULL PRIMARY KEY,ProductId uniqueidentifier NOT NULL,ProductVariantId uniqueidentifier NOT NULL UNIQUE,PriceRaw nvarchar(100) NULL,ImportPriceRaw nvarchar(100) NULL,IsActive bit NOT NULL);
        CREATE TABLE dbo.BranchProductStatistics(ProductId uniqueidentifier NOT NULL PRIMARY KEY,PurchaseCount bigint NOT NULL,UpdatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME());
        CREATE TABLE dbo.SalesOrders(SalesOrderId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,OrderCode nvarchar(200) NULL,CustomerPhoneSnapshot nvarchar(50) NULL,CustomerNameSnapshot nvarchar(300) NULL,Total decimal(19,4) NULL,TotalRaw nvarchar(200) NULL,Status nvarchar(100) NULL,Paid bit NULL,State nvarchar(100) NULL,CompletedAtUtc datetime2(7) NULL,ImagesJson nvarchar(max) NULL,SourceCreatedAtUtc datetime2(7) NULL,SourceUpdatedAtUtc datetime2(7) NULL,Version bigint NOT NULL);
        CREATE TABLE dbo.SalesOrderItems(SalesOrderItemId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,SalesOrderId uniqueidentifier NOT NULL,ProductId uniqueidentifier NULL,ProductVariantId uniqueidentifier NULL,SourceProductId char(24) NULL,VariantIndex int NULL,Quantity decimal(19,6) NULL,DetailsJson nvarchar(max) NULL,SortOrder int NOT NULL,Version bigint NOT NULL);
        CREATE TABLE dbo.Users(UserId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,Phone nvarchar(50) NULL,Name nvarchar(200) NULL,Role nvarchar(80) NULL,StationIdsJson nvarchar(max) NULL,Version bigint NOT NULL,IsDeleted bit NOT NULL);
        CREATE TABLE dbo.CartItems(CartItemId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,UserId uniqueidentifier NOT NULL,ProductId uniqueidentifier NULL,ProductVariantId uniqueidentifier NULL,SourceProductId char(24) NULL,VariantIndex int NULL,Quantity decimal(19,6) NULL,Status bit NOT NULL,SortOrder int NOT NULL,Version bigint NOT NULL);
        """;

    private static string Connection(string configured, string database) => new SqlConnectionStringBuilder(configured)
    {
        InitialCatalog = database,
        MultipleActiveResultSets = true,
    }.ConnectionString;

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ScalarAsync(string connectionString, string sql)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string?> StringScalarAsync(string connectionString, string sql)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new(sql, connection);
        return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. segments]);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Không tìm thấy migration trong repository.");
    }

    private sealed class FixedCompanyFactory(string connectionString) : ICompanyDbConnectionFactory
    {
        public SqlConnection Create() => new(connectionString);
    }

    private sealed class FixedOperationalFactory(string connectionString) : IOperationalDbConnectionFactory
    {
        public SqlConnection Create() => new(connectionString);
    }

    private sealed class FixedControlFactory(string connectionString) : IControlDbConnectionFactory
    {
        public SqlConnection Create() => new(connectionString);
    }
}
