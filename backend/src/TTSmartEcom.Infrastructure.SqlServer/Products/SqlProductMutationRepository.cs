using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.Infrastructure.SqlServer.Products;

#pragma warning disable CA1725

/// <summary>SQL implementation for Company Product Master and current Branch variant state.</summary>
public sealed class SqlProductMutationRepository(
    ICompanyDbConnectionFactory factory,
    IOperationalDbConnectionFactory operationalFactory,
    SqlProductCatalogRepository reads,
    SqlBranchProductReader branchProducts,
    IOrderStockPort stock) : IProductCatalogWriteRepository, IProductMediaRepository
{
    public async Task<ProductRecord?> FindEquivalentCodeAsync(string code, string? exclude, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("""
            SELECT PublicId FROM dbo.Products
            WHERE UPPER(REPLACE(REPLACE(Code,N' ',N''),N'-',N''))=@code
              AND IsDeleted=0 AND (@exclude IS NULL OR PublicId<>@exclude);
            """, connection);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@exclude", (object?)exclude ?? DBNull.Value);
        object? value = await command.ExecuteScalarAsync(ct);
        return value is string id ? await reads.FindByIdAsync(id, true, ct) : null;
    }

    public async Task<ProductMutationResult> CreateAsync(ProductMutation product, CancellationToken ct)
    {
        string id = SqlPublicIds.New();
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await using (SqlCommand command = new("""
                INSERT dbo.Products
                    (ProductId,PublicId,Name,NameUnsigned,Code,BrandName,TypeName,CategoryName,CategoryValue,
                     Description,VatRaw,Display,Adjusted,DetailsJson,DocumentsJson,Version)
                VALUES
                    (NEWID(),@id,@name,@unsigned,@code,@brand,@type,@section,@value,
                     @description,@vat,@display,@adjusted,@details,@docs,0);
                """, connection, transaction))
            {
                ProductParams(command, id, product);
                await command.ExecuteNonQueryAsync(ct);
            }
            foreach ((ProductVariantMutation variant, int index) in (product.Variants ?? []).Select((value, index) => (value, index)))
                await AddVariantAsync(connection, transaction, id, index, variant, ct);
            await transaction.CommitAsync(ct);
            return new(ProductMutationStatus.Success, await reads.FindByIdAsync(id, true, ct));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(ct);
            return new(ProductMutationStatus.Conflict, Message: "Mã sản phẩm đã tồn tại");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ProductMutationResult> UpdateAsync(string id, ProductMutation product, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("""
            UPDATE dbo.Products SET
                Name=COALESCE(@name,Name),Code=COALESCE(@code,Code),BrandName=COALESCE(@brand,BrandName),
                TypeName=COALESCE(@type,TypeName),CategoryName=COALESCE(@section,CategoryName),
                CategoryValue=COALESCE(@value,CategoryValue),Description=COALESCE(@description,Description),
                VatRaw=COALESCE(@vat,VatRaw),Display=COALESCE(@display,Display),
                Adjusted=COALESCE(@adjusted,Adjusted),Version=Version+1,SourceUpdatedAtUtc=SYSUTCDATETIME()
            WHERE PublicId=@id AND IsDeleted=0;
            """, connection);
        ProductParams(command, id, product);
        return await command.ExecuteNonQueryAsync(ct) == 0
            ? new(ProductMutationStatus.NotFound, Message: "Product not found")
            : new(ProductMutationStatus.Success, await reads.FindByIdAsync(id, true, ct));
    }

    public async Task<ProductMutationResult> DeleteAsync(string id, CancellationToken ct)
    {
        ProductRecord? before = await reads.FindByIdAsync(id, true, ct);
        if (before is null) return new(ProductMutationStatus.NotFound, Message: "Product not found");
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("UPDATE dbo.Products SET IsDeleted=1,Version=Version+1 WHERE PublicId=@id;", connection);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(ct);
        return new(ProductMutationStatus.Success, before);
    }

    public async Task<ProductMutationResult> ToggleDisplayAsync(string id, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("UPDATE dbo.Products SET Display=CASE WHEN Display=1 THEN 0 ELSE 1 END,Version=Version+1 WHERE PublicId=@id AND IsDeleted=0;", connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(ct) == 0
            ? new(ProductMutationStatus.NotFound)
            : new(ProductMutationStatus.Success, await reads.FindByIdAsync(id, true, ct));
    }

    public async Task<ProductMutationResult> DeleteManyAsync(IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new(ProductMutationStatus.Success, AffectedCount: 0);
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = $"UPDATE dbo.Products SET IsDeleted=1,Version=Version+1 WHERE PublicId IN ({string.Join(',', ids.Select((_, index) => "@id" + index))});";
        for (int index = 0; index < ids.Count; index++) command.Parameters.AddWithValue("@id" + index, ids.ElementAt(index));
        return new(ProductMutationStatus.Success, AffectedCount: await command.ExecuteNonQueryAsync(ct));
    }

    public async Task<ProductMutationResult> AddVariantAsync(string id, ProductVariantMutation variant, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await using SqlCommand sequence = new("SELECT COALESCE(MAX(v.SortOrder),-1)+1 FROM dbo.ProductVariants v JOIN dbo.Products p ON p.ProductId=v.ProductId WHERE p.PublicId=@id;", connection, transaction);
            sequence.Parameters.AddWithValue("@id", id);
            int next = Convert.ToInt32(await sequence.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
            await AddVariantAsync(connection, transaction, id, next, variant, ct);
            await transaction.CommitAsync(ct);
            return new(ProductMutationStatus.Success, await reads.FindByIdAsync(id, true, ct));
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ProductMutationResult> UpdateVariantAsync(string id, int index, ProductVariantMutation variant, CancellationToken ct)
    {
        Guid? branchId = null;
        if (HasBranchState(variant))
        {
            SqlBranchProductSnapshot? branchProduct = await branchProducts.FindVariantAsync(id, index, true, ct);
            if (branchProduct is null)
                return new(ProductMutationStatus.Forbidden, Message: "Sản phẩm chưa được phân phối cho chi nhánh hiện tại");
            await UpdateBranchVariantAsync(branchProduct, variant, ct);
            branchId = (await branchProducts.GetScopeAsync(ct)).BranchId;
        }

        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("""
            UPDATE v SET DetailsJson=COALESCE(@details,DetailsJson),Version=Version+1
            FROM dbo.ProductVariants v INNER JOIN dbo.Products p ON p.ProductId=v.ProductId
            WHERE p.PublicId=@id AND v.SortOrder=@index;
            """, connection);
        VariantMasterParams(command, id, index, variant);
        if (await command.ExecuteNonQueryAsync(ct) == 0) return new(ProductMutationStatus.NotFound);
        return new(ProductMutationStatus.Success, await reads.FindByIdAsync(id, true, branchId, ct));
    }

    public async Task<ProductMutationResult> DeleteVariantAsync(string id, int index, CancellationToken ct)
    {
        SqlBranchProductSnapshot? product = await branchProducts.FindVariantAsync(id, index, false, ct);
        if (product is null) return new(ProductMutationStatus.NotFound);
        if (product.QuantityForSale != 0 || product.QuantityInStorage != 0)
            return new(ProductMutationStatus.Invalid, Message: "Không thể xóa phiên bản còn tồn kho tại chi nhánh");
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("DELETE v FROM dbo.ProductVariants v JOIN dbo.Products p ON p.ProductId=v.ProductId WHERE p.PublicId=@id AND v.SortOrder=@index;", connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@index", index);
        return await command.ExecuteNonQueryAsync(ct) == 0
            ? new(ProductMutationStatus.Invalid, Message: "Không thể xóa phiên bản")
            : new(ProductMutationStatus.Success, await reads.FindByIdAsync(id, true, ct));
    }

    public Task<ProductMutationResult> UpdateVariantEarnAsync(string id, int index, double earn, CancellationToken ct) =>
        UpdateVariantAsync(id, index, new ProductVariantMutation(null, null, null, earn, null, null, null, null, null, null, null, null), ct);

    public Task<ProductMutationResult> UpdateVariantImportPriceAsync(string id, int index, string price, CancellationToken ct) =>
        UpdateVariantAsync(id, index, new ProductVariantMutation(null, null, price, null, null, null, null, null, null, null, null, null), ct);

    public async Task<ProductMutationResult> AdjustPurchaseCountAsync(string id, long delta, CancellationToken ct)
    {
        SqlBranchProductSnapshot? product = await branchProducts.FindVariantAsync(id, 0, true, ct);
        if (product is null) return new(ProductMutationStatus.Forbidden, Message: "Sản phẩm chưa được phân phối cho chi nhánh hiện tại");
        try
        {
            await stock.AdjustAsync([new StockAdjustment(id, 0, 0, 0, delta, product.ProductVariantPublicId)], ct);
        }
        catch (TTSmartEcom.Application.Common.Errors.ApplicationException)
        {
            return new(ProductMutationStatus.Invalid, Message: "Số lượng đã mua không đủ để giảm");
        }
        Guid branchId = (await branchProducts.GetScopeAsync(ct)).BranchId;
        return new(ProductMutationStatus.Success, await reads.FindByIdAsync(id, true, branchId, ct));
    }

    public async Task<long> BackfillDisplayAsync(CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("UPDATE dbo.Products SET Display=1 WHERE Display IS NULL;", connection);
        return await command.ExecuteNonQueryAsync(ct);
    }

    public Task<IReadOnlyList<ProductRecord>> FindByCodesAsync(IReadOnlyCollection<string> codes, bool include, CancellationToken ct) =>
        reads.FindByIdsAsync(codes, include, ct);

    public async Task<ProductTypeMutationResult> CreateTypeAsync(string name, string icon, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        string id = SqlPublicIds.New();
        try
        {
            await using SqlCommand command = new("INSERT dbo.ProductTypes(ProductTypeId,PublicId,Name,Icon,Version) VALUES(NEWID(),@id,@name,@icon,0);", connection);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@icon", icon);
            await command.ExecuteNonQueryAsync(ct);
            return new(ProductMutationStatus.Success, new ProductTypeRecord(id, name, icon, null, null));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return new(ProductMutationStatus.Conflict, Message: "Loại sản phẩm đã tồn tại");
        }
    }

    public async Task<ProductTypeMutationResult> UpdateTypeAsync(string id, string name, string icon, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("UPDATE dbo.ProductTypes SET Name=@name,Icon=@icon,Version=Version+1 WHERE PublicId=@id;", connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@icon", icon);
        return await command.ExecuteNonQueryAsync(ct) == 0
            ? new(ProductMutationStatus.NotFound)
            : new(ProductMutationStatus.Success, new ProductTypeRecord(id, name, icon, null, null));
    }

    public async Task<ProductTypeMutationResult> DeleteTypeAsync(string id, bool unused, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("DELETE FROM dbo.ProductTypes OUTPUT deleted.PublicId,deleted.Name,deleted.Icon WHERE PublicId=@id;", connection);
        command.Parameters.AddWithValue("@id", id);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new(ProductMutationStatus.Success, new ProductTypeRecord(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), null, null))
            : new(ProductMutationStatus.NotFound);
    }

    public async Task<IReadOnlyList<ProductReview>?> GetReviewsAsync(string id, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("SELECT PublicId,ReviewerName,Content,Rating FROM dbo.ProductReviews WHERE ProductId=(SELECT ProductId FROM dbo.Products WHERE PublicId=@id) ORDER BY SortOrder;", connection);
        command.Parameters.AddWithValue("@id", id);
        List<ProductReview> reviews = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) reviews.Add(new(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : (double)reader.GetDecimal(3), null));
        return reviews;
    }

    public async Task<ProductReviewMutationResult> CreateReviewAsync(string id, string email, string? comment, double rating, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        string reviewId = SqlPublicIds.New();
        await using SqlCommand command = new("INSERT dbo.ProductReviews(ProductReviewId,PublicId,ProductId,SortOrder,Rating,Content,ReviewerName,Version) SELECT NEWID(),@review,ProductId,(SELECT COUNT(*) FROM dbo.ProductReviews WHERE ProductId=p.ProductId),@rating,@content,@email,0 FROM dbo.Products p WHERE PublicId=@id AND IsDeleted=0;", connection);
        command.Parameters.AddWithValue("@review", reviewId);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@rating", rating);
        command.Parameters.AddWithValue("@content", (object?)comment ?? DBNull.Value);
        command.Parameters.AddWithValue("@email", email);
        return await command.ExecuteNonQueryAsync(ct) == 0
            ? new(ProductMutationStatus.NotFound)
            : new(ProductMutationStatus.Success, new ProductReview(reviewId, email, comment, rating, DateTimeOffset.UtcNow));
    }

    public async Task<ProductReviewMutationResult> UpdateReviewAsync(string id, string review, string? comment, double? rating, string actor, bool moderator, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("UPDATE r SET Content=COALESCE(@content,Content),Rating=COALESCE(@rating,Rating),Version=Version+1 OUTPUT inserted.PublicId,inserted.ReviewerName,inserted.Content,inserted.Rating FROM dbo.ProductReviews r JOIN dbo.Products p ON p.ProductId=r.ProductId WHERE p.PublicId=@id AND r.PublicId=@review AND (@moderator=1 OR r.ReviewerName=@actor);", connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@review", review);
        command.Parameters.AddWithValue("@content", (object?)comment ?? DBNull.Value);
        command.Parameters.AddWithValue("@rating", (object?)rating ?? DBNull.Value);
        command.Parameters.AddWithValue("@actor", actor);
        command.Parameters.AddWithValue("@moderator", moderator);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new(ProductMutationStatus.Success, new ProductReview(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : (double)reader.GetDecimal(3), null))
            : new(ProductMutationStatus.NotFound);
    }

    public async Task<ProductReviewMutationResult> DeleteReviewAsync(string id, string review, string actor, bool moderator, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("DELETE r FROM dbo.ProductReviews r JOIN dbo.Products p ON p.ProductId=r.ProductId WHERE p.PublicId=@id AND r.PublicId=@review AND (@moderator=1 OR r.ReviewerName=@actor);", connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@review", review);
        command.Parameters.AddWithValue("@actor", actor);
        command.Parameters.AddWithValue("@moderator", moderator);
        return await command.ExecuteNonQueryAsync(ct) == 0 ? new(ProductMutationStatus.NotFound) : new(ProductMutationStatus.Success);
    }

    public async Task<ProductStockMutationResult> AdjustStockAsync(string id, int index, ProductStockMutation mutation, CancellationToken ct)
    {
        SqlBranchProductSnapshot? product = await branchProducts.FindVariantAsync(id, index, true, ct);
        if (product is null) return new(ProductMutationStatus.Forbidden, Message: "Sản phẩm chưa được phân phối cho chi nhánh hiện tại");
        try
        {
            await stock.AdjustAsync([new StockAdjustment(id, index, mutation.Quantity, mutation.Quantity, ExpectedVariantId: product.ProductVariantPublicId)], ct);
        }
        catch (TTSmartEcom.Application.Common.Errors.ApplicationException)
        {
            return new(ProductMutationStatus.Invalid, Message: "Insufficient stock");
        }
        Guid branchId = (await branchProducts.GetScopeAsync(ct)).BranchId;
        return new(ProductMutationStatus.Success, await reads.FindByIdAsync(id, true, branchId, ct));
    }

    public async Task<ProductVariantImageReference?> GetVariantImageReferenceAsync(string id, int index, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("SELECT DetailsJson FROM dbo.ProductVariants WHERE ProductId=(SELECT ProductId FROM dbo.Products WHERE PublicId=@id) AND SortOrder=@index;", connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@index", index);
        object? value = await command.ExecuteScalarAsync(ct);
        if (value is null) return await reads.FindByIdAsync(id, true, ct) is null ? null : new ProductVariantImageReference(false, null);
        using JsonDocument details = JsonDocument.Parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "{}");
        JsonElement root = details.RootElement;
        string? url = root.TryGetProperty("imgUrl", out JsonElement image) && image.ValueKind == JsonValueKind.String
            ? image.GetString()
            : root.TryGetProperty("imageUrl", out JsonElement alternate) && alternate.ValueKind == JsonValueKind.String ? alternate.GetString() : null;
        return new ProductVariantImageReference(true, url);
    }

    public async Task<bool> IsProductImageReferencedElsewhereAsync(string id, int index, string filename, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("SELECT COUNT(*) FROM dbo.ProductVariants v JOIN dbo.Products p ON p.ProductId=v.ProductId WHERE (p.PublicId<>@id OR v.SortOrder<>@index) AND v.DetailsJson LIKE @file;", connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@index", index);
        command.Parameters.AddWithValue("@file", "%" + filename + "%");
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) > 0;
    }

    public async Task<ProductRecord?> ClearVariantImageAsync(string id, int index, string expected, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("UPDATE v SET DetailsJson=JSON_MODIFY(JSON_MODIFY(DetailsJson,'$.imgUrl',NULL),'$.imageUrl',NULL),Version=Version+1 FROM dbo.ProductVariants v JOIN dbo.Products p ON p.ProductId=v.ProductId WHERE p.PublicId=@id AND v.SortOrder=@index AND v.DetailsJson LIKE @expected;", connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@index", index);
        command.Parameters.AddWithValue("@expected", "%" + expected + "%");
        return await command.ExecuteNonQueryAsync(ct) == 1 ? await reads.FindByIdAsync(id, true, ct) : null;
    }

    public async Task<bool> IsInvoiceImageReferencedAsync(string filename, CancellationToken ct)
    {
        await using SqlConnection connection = operationalFactory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.SalesOrders WHERE ImagesJson LIKE @file) OR EXISTS(SELECT 1 FROM dbo.InventoryOrders WHERE ImagesJson LIKE @file) THEN 1 ELSE 0 END;", connection);
        command.Parameters.AddWithValue("@file", "%" + filename + "%");
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) == 1;
    }

    private static async Task AddVariantAsync(SqlConnection connection, SqlTransaction transaction, string id, int index, ProductVariantMutation variant, CancellationToken ct)
    {
        await using SqlCommand command = new("""
            INSERT dbo.ProductVariants
                (ProductVariantId,PublicId,ProductId,SortOrder,PriceRaw,ImportPriceRaw,QuantityForSale,QuantityInStorage,DetailsJson,Version)
            VALUES
                (NEWID(),@variant,(SELECT ProductId FROM dbo.Products WHERE PublicId=@id),@index,@price,NULL,NULL,NULL,@details,0);
            """, connection, transaction);
        VariantMasterParams(command, id, index, variant);
        command.Parameters.AddWithValue("@variant", SqlPublicIds.New());
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task UpdateBranchVariantAsync(SqlBranchProductSnapshot product, ProductVariantMutation mutation, CancellationToken ct)
    {
        await using SqlConnection connection = operationalFactory.Create();
        await connection.OpenAsync(ct);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await using (SqlCommand command = new("""
                UPDATE dbo.BranchProductVariants WITH(UPDLOCK,HOLDLOCK)
                SET Price=COALESCE(@priceValue,Price),PriceRaw=COALESCE(@price,PriceRaw),
                    ImportPrice=COALESCE(@importValue,ImportPrice),ImportPriceRaw=COALESCE(@import,ImportPriceRaw),
                    IsActive=1,UpdatedAtUtc=SYSUTCDATETIME()
                WHERE ProductVariantId=@variant;
                IF @@ROWCOUNT=0
                    INSERT dbo.BranchProductVariants
                        (BranchProductVariantId,ProductId,ProductVariantId,Price,PriceRaw,ImportPrice,ImportPriceRaw,IsActive)
                    VALUES (NEWID(),@product,@variant,@priceValue,@price,@importValue,@import,1);
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("@product", product.ProductId);
                command.Parameters.AddWithValue("@variant", product.ProductVariantId);
                command.Parameters.AddWithValue("@price", (object?)mutation.Price ?? DBNull.Value);
                command.Parameters.AddWithValue("@priceValue", (object?)ParseMoney(mutation.Price) ?? DBNull.Value);
                command.Parameters.AddWithValue("@import", (object?)mutation.ImportPrice ?? DBNull.Value);
                command.Parameters.AddWithValue("@importValue", (object?)ParseMoney(mutation.ImportPrice) ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(ct);
            }

            if (mutation.QuantityForSale.HasValue || mutation.QuantityInStorage.HasValue)
            {
                await using SqlCommand command = new("""
                    UPDATE dbo.BranchStockBalances
                    SET QuantityForSale=COALESCE(@sale,QuantityForSale),
                        QuantityInStorage=COALESCE(@storage,QuantityInStorage),UpdatedAtUtc=SYSUTCDATETIME()
                    WHERE ProductVariantId=@variant;
                    IF @@ROWCOUNT=0
                        INSERT dbo.BranchStockBalances
                            (ProductVariantId,ProductId,ProductPublicId,ProductVariantPublicId,VariantPosition,
                             QuantityForSale,QuantityInStorage,ProductCodeSnapshot,ProductNameSnapshot,
                             VariantNameSnapshot,SourceVersion)
                        VALUES
                            (@variant,@product,@productPublic,@variantPublic,@position,COALESCE(@sale,0),COALESCE(@storage,0),
                             @code,@name,@variantName,0);
                    """, connection, transaction);
                command.Parameters.AddWithValue("@variant", product.ProductVariantId);
                command.Parameters.AddWithValue("@product", product.ProductId);
                command.Parameters.AddWithValue("@productPublic", product.ProductPublicId);
                command.Parameters.AddWithValue("@variantPublic", product.ProductVariantPublicId);
                command.Parameters.AddWithValue("@position", product.VariantIndex);
                command.Parameters.AddWithValue("@sale", (object?)mutation.QuantityForSale ?? DBNull.Value);
                command.Parameters.AddWithValue("@storage", (object?)mutation.QuantityInStorage ?? DBNull.Value);
                command.Parameters.AddWithValue("@code", (object?)product.Code ?? DBNull.Value);
                command.Parameters.AddWithValue("@name", (object?)product.ProductName ?? DBNull.Value);
                command.Parameters.AddWithValue("@variantName", (object?)product.VariantName ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static bool HasBranchState(ProductVariantMutation variant) =>
        variant.Price is not null || variant.ImportPrice is not null || variant.QuantityForSale.HasValue || variant.QuantityInStorage.HasValue;

    private static decimal? ParseMoney(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal number) ? number : null;

    private static void ProductParams(SqlCommand command, string id, ProductMutation product)
    {
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", (object?)product.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("@unsigned", (object?)product.Name?.ToLowerInvariant() ?? DBNull.Value);
        command.Parameters.AddWithValue("@code", (object?)product.Code ?? DBNull.Value);
        command.Parameters.AddWithValue("@brand", (object?)product.Brand ?? DBNull.Value);
        command.Parameters.AddWithValue("@type", (object?)product.Type ?? DBNull.Value);
        command.Parameters.AddWithValue("@section", (object?)product.Section ?? DBNull.Value);
        command.Parameters.AddWithValue("@value", (object?)product.Value ?? DBNull.Value);
        command.Parameters.AddWithValue("@description", (object?)product.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@vat", (object?)product.Vat ?? DBNull.Value);
        command.Parameters.AddWithValue("@display", (object?)product.Display ?? DBNull.Value);
        command.Parameters.AddWithValue("@adjusted", (object?)product.Adjusted ?? DBNull.Value);
        command.Parameters.AddWithValue("@details", JsonSerializer.Serialize(product));
        command.Parameters.AddWithValue("@docs", JsonSerializer.Serialize(product.Documents ?? []));
    }

    private static void VariantMasterParams(SqlCommand command, string id, int index, ProductVariantMutation variant)
    {
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@index", index);
        command.Parameters.AddWithValue("@price", (object?)variant.Price ?? DBNull.Value);
        command.Parameters.AddWithValue("@details", JsonSerializer.Serialize(new
        {
            earn = variant.Earn,
            imgUrl = variant.ImageUrl,
            variant.Color,
            variant.Shape,
            variant.ButtonCount,
            variant.Frame,
            variant.Note,
        }));
    }
}
