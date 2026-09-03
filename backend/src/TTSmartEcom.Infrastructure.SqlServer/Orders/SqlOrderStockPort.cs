using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Infrastructure.SqlServer.Products;

namespace TTSmartEcom.Infrastructure.SqlServer.Orders;

#pragma warning disable CA1725

public sealed class SqlOrderStockPort(
    IOperationalDbConnectionFactory factory,
    SqlBranchProductReader branchProducts) : IOrderStockPort
{
    public async Task<ProductOrderSnapshot?> GetProductAsync(
        string productId,
        int variantIndex,
        CancellationToken cancellationToken)
    {
        SqlBranchProductSnapshot? product = await branchProducts.FindVariantAsync(
            productId, variantIndex, requireActiveAssignment: false, cancellationToken);
        if (product is null) return null;
        using JsonDocument details = JsonDocument.Parse(product.DetailsJson);
        JsonElement root = details.RootElement;
        return new ProductOrderSnapshot(
            product.ProductPublicId,
            product.VariantIndex,
            product.ProductVariantPublicId,
            product.ProductName,
            product.BrandName,
            product.Code,
            product.PriceRaw,
            Text(root, "imgUrl"),
            Text(root, "color"),
            Text(root, "shape"),
            product.QuantityForSale,
            product.QuantityInStorage,
            Number(root, "earn", 25),
            product.Display,
            product.ImportPriceRaw,
            product.IsAssigned,
            product.ProductId,
            product.ProductVariantId,
            product.VariantName);
    }

    public async Task<IReadOnlyList<StockAdjustment>> AdjustAsync(
        IReadOnlyList<StockAdjustment> adjustments,
        CancellationToken cancellationToken)
    {
        if (adjustments.Count == 0) return [];
        Dictionary<(string ProductId, int VariantIndex), SqlBranchProductSnapshot> products = [];
        foreach (StockAdjustment adjustment in adjustments)
        {
            SqlBranchProductSnapshot? product = await branchProducts.FindVariantAsync(
                adjustment.ProductId,
                adjustment.VariantIndex,
                requireActiveAssignment: false,
                cancellationToken);
            if (product is null || adjustment.ExpectedVariantId is not null &&
                !adjustment.ExpectedVariantId.Equals(product.ProductVariantPublicId, StringComparison.OrdinalIgnoreCase))
                throw Fail(404, "Không tìm thấy Product/Variant trong Company DB.");
            if (adjustment.RequireActiveAssignment && !product.IsAssigned)
                throw Fail(403, "Sản phẩm chưa được phân phối cho chi nhánh hiện tại.");
            products[(adjustment.ProductId, adjustment.VariantIndex)] = product;
        }

        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (StockAdjustment adjustment in adjustments)
                await ApplyAsync(connection, transaction, adjustment, products[(adjustment.ProductId, adjustment.VariantIndex)], cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return adjustments;
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw exception as TTSmartEcom.Application.Common.Errors.ApplicationException
                ?? Fail(409, "Tồn kho vừa được thay đổi hoặc không đủ số lượng.", exception);
        }
    }

    public Task RollbackAsync(IReadOnlyList<StockAdjustment> adjustments, CancellationToken cancellationToken) =>
        AdjustAsync(adjustments.Reverse().Select(adjustment => adjustment with
        {
            QuantityForSaleDelta = -adjustment.QuantityForSaleDelta,
            QuantityInStorageDelta = -adjustment.QuantityInStorageDelta,
            PurchaseCountDelta = -adjustment.PurchaseCountDelta,
            RequireActiveAssignment = false,
        }).ToArray(), cancellationToken);

    private static async Task ApplyAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        StockAdjustment adjustment,
        SqlBranchProductSnapshot product,
        CancellationToken cancellationToken)
    {
        await using (SqlCommand command = new("""
            UPDATE dbo.BranchStockBalances WITH (UPDLOCK,HOLDLOCK)
            SET QuantityForSale=COALESCE(QuantityForSale,0)+@sale,
                QuantityInStorage=COALESCE(QuantityInStorage,0)+@storage,
                ProductCodeSnapshot=@code,
                ProductNameSnapshot=@name,
                VariantNameSnapshot=@variantName,
                UpdatedAtUtc=SYSUTCDATETIME()
            WHERE ProductVariantId=@variantId
              AND (@sale>=0 OR COALESCE(QuantityForSale,0)>=-@sale)
              AND (@storage>=0 OR COALESCE(QuantityInStorage,0)>=-@storage);
            SELECT @@ROWCOUNT;
            """, connection, transaction))
        {
            AddStockParameters(command, adjustment, product);
            int updated = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
            if (updated == 0)
            {
                if (adjustment.QuantityForSaleDelta < 0 || adjustment.QuantityInStorageDelta < 0)
                    throw Fail(409, "Không đủ tồn kho tại chi nhánh hiện tại.");
                await InsertBalanceAsync(connection, transaction, adjustment, product, cancellationToken);
            }
        }

        if (adjustment.PurchaseCountDelta != 0)
            await UpdatePurchaseCountAsync(connection, transaction, adjustment, product.ProductId, cancellationToken);
    }

    private static async Task InsertBalanceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        StockAdjustment adjustment,
        SqlBranchProductSnapshot product,
        CancellationToken cancellationToken)
    {
        try
        {
            await using SqlCommand command = new("""
                INSERT dbo.BranchStockBalances
                    (ProductVariantId,ProductId,ProductPublicId,ProductVariantPublicId,VariantPosition,
                     QuantityForSale,QuantityInStorage,ProductCodeSnapshot,ProductNameSnapshot,
                     VariantNameSnapshot,SourceVersion)
                VALUES
                    (@variantId,@productId,@productPublicId,@variantPublicId,@position,
                     @sale,@storage,@code,@name,@variantName,0);
                """, connection, transaction);
            AddStockParameters(command, adjustment, product);
            command.Parameters.AddWithValue("@productId", product.ProductId);
            command.Parameters.AddWithValue("@productPublicId", product.ProductPublicId);
            command.Parameters.AddWithValue("@variantPublicId", product.ProductVariantPublicId);
            command.Parameters.AddWithValue("@position", product.VariantIndex);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw Fail(409, "Tồn kho vừa được khởi tạo bởi yêu cầu khác.", exception);
        }
    }

    private static async Task UpdatePurchaseCountAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        StockAdjustment adjustment,
        Guid productId,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = new("""
            UPDATE dbo.BranchProductStatistics WITH (UPDLOCK,HOLDLOCK)
            SET PurchaseCount=PurchaseCount+@delta,UpdatedAtUtc=SYSUTCDATETIME()
            WHERE ProductId=@productId AND (@delta>=0 OR PurchaseCount>=-@delta);
            IF @@ROWCOUNT=0 AND @delta>=0
                INSERT dbo.BranchProductStatistics(ProductId,PurchaseCount) VALUES(@productId,@delta);
            SELECT PurchaseCount FROM dbo.BranchProductStatistics WHERE ProductId=@productId;
            """, connection, transaction);
        command.Parameters.AddWithValue("@productId", productId);
        SqlParameter delta = command.Parameters.Add("@delta", SqlDbType.BigInt);
        delta.Value = checked((long)adjustment.PurchaseCountDelta);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null || value is DBNull) throw Fail(409, "Số lượt mua tại chi nhánh không đủ để giảm.");
    }

    private static void AddStockParameters(SqlCommand command, StockAdjustment adjustment, SqlBranchProductSnapshot product)
    {
        command.Parameters.AddWithValue("@variantId", product.ProductVariantId);
        command.Parameters.AddWithValue("@sale", (decimal)adjustment.QuantityForSaleDelta);
        command.Parameters.AddWithValue("@storage", (decimal)adjustment.QuantityInStorageDelta);
        command.Parameters.AddWithValue("@code", (object?)product.Code ?? DBNull.Value);
        command.Parameters.AddWithValue("@name", (object?)product.ProductName ?? DBNull.Value);
        command.Parameters.AddWithValue("@variantName", (object?)product.VariantName ?? DBNull.Value);
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static double Number(JsonElement root, string name, double fallback) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetDouble(out double result) ? result : fallback;
    private static TTSmartEcom.Application.Common.Errors.ApplicationException Fail(int status, string message, Exception? inner = null) =>
        new(new ApplicationError($"TTS-STOCK-{status}", 4400 + status, status, message), inner);
}
