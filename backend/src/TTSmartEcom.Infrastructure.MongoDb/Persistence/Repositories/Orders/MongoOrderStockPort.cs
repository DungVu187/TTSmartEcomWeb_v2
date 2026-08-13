using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Orders;

public sealed class MongoOrderStockPort(IMongoDatabaseProvider databaseProvider) : IOrderStockPort
{
    private readonly IMongoCollection<ProductDocument> products = databaseProvider.Database.GetCollection<ProductDocument>(ProductDocument.CollectionName);

    public async Task<ProductOrderSnapshot?> GetProductAsync(string productId, int variantIndex, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(productId, out ObjectId id) || variantIndex < 0) return null;
        ProductDocument? product = await products.Find(Builders<ProductDocument>.Filter.Eq(x => x.Id, id)).Limit(1).FirstOrDefaultAsync(cancellationToken);
        ProductVariantDocument? variant = product?.Variants is not null && variantIndex < product.Variants.Count ? product.Variants[variantIndex] : null;
        if (product is null || variant is null) return null;
        return new ProductOrderSnapshot(productId, variantIndex, variant.Id?.ToString(), product.Name, product.Brand, product.Code, variant.Price,
            variant.ImageUrl, variant.Color, variant.Shape, variant.QuantityForSale ?? 0, variant.QuantityInStorage ?? 0, variant.Earn ?? 25, product.Display ?? true,
            variant.ImportPrice);
    }

    public async Task<IReadOnlyList<StockAdjustment>> AdjustAsync(IReadOnlyList<StockAdjustment> adjustments, CancellationToken cancellationToken)
    {
        List<StockAdjustment> applied = [];
        try
        {
            foreach (StockAdjustment requested in adjustments)
            {
                StockAdjustment normalized = await ApplyOneAsync(requested, cancellationToken);
                applied.Add(normalized);
            }
            return applied;
        }
        catch (Exception error)
        {
            try { await RollbackAsync(applied, cancellationToken); }
            catch (Exception rollbackError) { throw Failure(500, "Không thể hoàn tác đầy đủ thay đổi tồn kho.", rollbackError); }
            throw FailureFrom(error);
        }
    }

    public async Task RollbackAsync(IReadOnlyList<StockAdjustment> adjustments, CancellationToken cancellationToken)
    {
        foreach (StockAdjustment adjustment in adjustments.Reverse())
        {
            await ApplyOneAsync(adjustment with
            {
                QuantityForSaleDelta = -adjustment.QuantityForSaleDelta,
                QuantityInStorageDelta = -adjustment.QuantityInStorageDelta,
                PurchaseCountDelta = -adjustment.PurchaseCountDelta,
            }, cancellationToken);
        }
    }

    private async Task<StockAdjustment> ApplyOneAsync(StockAdjustment adjustment, CancellationToken ct)
    {
        if (!ObjectId.TryParse(adjustment.ProductId, out ObjectId productId) || adjustment.VariantIndex < 0) throw Failure(400, "Mã sản phẩm hoặc phiên bản không hợp lệ.");
        ProductOrderSnapshot product = await GetProductAsync(adjustment.ProductId, adjustment.VariantIndex, ct) ?? throw Failure(404, "Không tìm thấy sản phẩm hoặc phiên bản sản phẩm.");
        string variantIdText = adjustment.ExpectedVariantId ?? product.VariantId ?? string.Empty;
        if (!ObjectId.TryParse(variantIdText, out ObjectId variantId)) throw Failure(409, "Phiên bản sản phẩm đã thay đổi, vui lòng tải lại dữ liệu.");

        FilterDefinitionBuilder<ProductDocument> b = Builders<ProductDocument>.Filter;
        FilterDefinition<ProductDocument> filter = b.Eq(x => x.Id, productId) &
            b.Eq($"variant.{adjustment.VariantIndex}._id", variantId);
        if (adjustment.QuantityForSaleDelta < 0) filter &= b.Gte($"variant.{adjustment.VariantIndex}.quantityForSale", Math.Abs(adjustment.QuantityForSaleDelta));
        if (adjustment.QuantityInStorageDelta < 0) filter &= b.Gte($"variant.{adjustment.VariantIndex}.quantityInStorage", Math.Abs(adjustment.QuantityInStorageDelta));
        if (adjustment.PurchaseCountDelta < 0) filter &= b.Gte(x => x.PurchaseCount, Convert.ToInt64(Math.Ceiling(Math.Abs(adjustment.PurchaseCountDelta))));

        List<UpdateDefinition<ProductDocument>> updates = [];
        if (adjustment.QuantityForSaleDelta != 0) updates.Add(Builders<ProductDocument>.Update.Inc($"variant.{adjustment.VariantIndex}.quantityForSale", adjustment.QuantityForSaleDelta));
        if (adjustment.QuantityInStorageDelta != 0) updates.Add(Builders<ProductDocument>.Update.Inc($"variant.{adjustment.VariantIndex}.quantityInStorage", adjustment.QuantityInStorageDelta));
        if (adjustment.PurchaseCountDelta != 0) updates.Add(Builders<ProductDocument>.Update.Inc(x => x.PurchaseCount, Convert.ToInt64(adjustment.PurchaseCountDelta)));
        if (updates.Count == 0) return adjustment with { ExpectedVariantId = variantIdText };
        UpdateResult result = await products.UpdateOneAsync(filter, Builders<ProductDocument>.Update.Combine(updates), cancellationToken: ct);
        if (result.ModifiedCount != 1) throw Failure(409, "Tồn kho vừa được thay đổi bởi thao tác khác hoặc không đủ số lượng.");
        return adjustment with { ExpectedVariantId = variantIdText };
    }

    private static TTSmartEcom.Application.Common.Errors.ApplicationException FailureFrom(Exception error) => error as TTSmartEcom.Application.Common.Errors.ApplicationException ?? Failure(500, "Lỗi khi cập nhật tồn kho", error);
    private static TTSmartEcom.Application.Common.Errors.ApplicationException Failure(int status, string message, Exception? inner = null) => new(new ApplicationError($"TTS-STOCK-{status}", 4400 + status, status, message), inner);
}
