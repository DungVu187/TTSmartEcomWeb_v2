namespace TTSmartEcom.UnitTests.SqlServer;

public sealed class ProductBranchSqlBoundaryTests
{
    [Fact]
    public void AssignmentMigration_IsVersionedIdempotentAndNonDestructive()
    {
        string sql = Read("database", "sqlserver", "split", "005_CreateProductBranchAssignments.sql");

        Assert.Contains("CREATE TABLE dbo.ProductBranchAssignments", sql, StringComparison.Ordinal);
        Assert.Contains("UQ_ProductBranchAssignments_Product_Branch UNIQUE (ProductId, BranchId)", sql, StringComparison.Ordinal);
        Assert.Contains("FK_ProductBranchAssignments_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId)", sql, StringComparison.Ordinal);
        Assert.Contains("MigrationNumber = 10004", sql, StringComparison.Ordinal);
        Assert.Contains("ScriptChecksum", sql, StringComparison.Ordinal);
        Assert.Contains("@ActiveBefore", sql, StringComparison.Ordinal);
        Assert.Contains("@ActiveAfter", sql, StringComparison.Ordinal);
        Assert.True(
            sql.IndexOf("ScriptChecksum = N'$(ScriptChecksum)'", StringComparison.Ordinal) <
            sql.IndexOf("BEGIN TRANSACTION", StringComparison.Ordinal),
            "Migration phải dừng trước khi backfill nếu checksum đã được áp dụng.");
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM dbo.Products", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FOREIGN KEY (BranchId)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OperationalRepositories_DoNotQueryProductMasterTables()
    {
        string[] paths =
        [
            Path.Combine("backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Cart", "SqlCartRepository.cs"),
            Path.Combine("backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Orders", "SqlOrderRepository.cs"),
            Path.Combine("backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Orders", "SqlOrderStockPort.cs"),
            Path.Combine("backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Inventory", "SqlInventoryOrderRepository.cs"),
            Path.Combine("backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Stations", "SqlStationRepository.cs"),
            Path.Combine("backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Audit", "SqlStorageHistoryRepository.cs"),
        ];

        foreach (string path in paths)
        {
            string source = Read(path.Split(Path.DirectorySeparatorChar));
            Assert.DoesNotContain("dbo.Products", source, StringComparison.Ordinal);
            Assert.DoesNotContain("dbo.ProductVariants", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProductMutation_DoesNotUpdateCompanyVariantQuantities()
    {
        string source = Read("backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Products", "SqlProductMutationRepository.cs");

        Assert.DoesNotContain("UPDATE v SET QuantityForSale", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET QuantityForSale=COALESCE(QuantityForSale", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UPDATE dbo.BranchStockBalances", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BranchScopedProductCreate_AssignsProductInsideCompanyTransaction()
    {
        string source = Read("backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Products", "SqlProductMutationRepository.cs");

        int assignment = source.IndexOf("AddCreationAssignmentAsync", StringComparison.Ordinal);
        int commit = source.IndexOf("transaction.CommitAsync", assignment, StringComparison.Ordinal);
        Assert.True(assignment >= 0 && commit > assignment);
        Assert.Contains("INSERT dbo.ProductBranchAssignments", source, StringComparison.Ordinal);
        Assert.Contains("source = \"branch_product_create\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_LoadsVariantsAndBranchStateInBatches()
    {
        string source = Read("backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Products", "SqlProductCatalogRepository.cs");

        Assert.Equal(1, Count(source, "FROM dbo.ProductVariants"));
        Assert.Contains("LoadStatesAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (ProductRow", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BranchDocuments_PersistProductAndVariantSnapshots()
    {
        string sales = Read("backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Orders", "SqlOrderRepository.cs");
        string inventory = Read("backend", "src", "TTSmartEcom.Infrastructure.SqlServer", "Inventory", "SqlInventoryOrderRepository.cs");

        foreach (string source in new[] { sales, inventory })
        {
            Assert.Contains("productCodeSnapshot", source, StringComparison.Ordinal);
            Assert.Contains("productNameSnapshot", source, StringComparison.Ordinal);
            Assert.Contains("variantNameSnapshot", source, StringComparison.Ordinal);
            Assert.Contains("variantPublicIdSnapshot", source, StringComparison.Ordinal);
            Assert.Contains("unitPriceSnapshot", source, StringComparison.Ordinal);
        }


        Assert.Contains("x.ProductNameSnapshot??product.ProductName", sales, StringComparison.Ordinal);
        Assert.Contains("x.Name??product?.ProductName", inventory, StringComparison.Ordinal);
        Assert.Contains("x.VariantPublicIdSnapshot??product?.ProductVariantPublicId", inventory, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectionBootstrap_DoesNotOverwriteBranchSpecificPrices()
    {
        string script = Read("database", "sqlserver", "split", "Run-DataSplitMigrations.ps1");

        Assert.Contains("IF NOT EXISTS", script, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE dbo.BranchProductVariants", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ValidateSet('TTSmart_MAIN_online')", script, StringComparison.Ordinal);
        Assert.Contains("Assert-DataDatabaseMetadata", script, StringComparison.Ordinal);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([RepositoryRoot(), .. segments]));

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "backend")) &&
                Directory.Exists(Path.Combine(directory.FullName, "database")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Không tìm thấy repository root cho SQL boundary tests.");
    }
}
