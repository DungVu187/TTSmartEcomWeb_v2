using System.Globalization;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Domain.Products;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Products;

public sealed class MongoProductCatalogWriteRepository(IMongoDatabaseProvider databaseProvider)
    : IProductCatalogWriteRepository, IProductMediaRepository
{
    private readonly IMongoCollection<BsonDocument> products =
        databaseProvider.Database.GetCollection<BsonDocument>(ProductDocument.CollectionName);
    private readonly IMongoCollection<BsonDocument> types =
        databaseProvider.Database.GetCollection<BsonDocument>(ProductTypeDocument.CollectionName);
    private readonly IMongoCollection<BsonDocument> manages =
        databaseProvider.Database.GetCollection<BsonDocument>(ManageDocument.CollectionName);
    private readonly IMongoCollection<BsonDocument> storageHistories =
        databaseProvider.Database.GetCollection<BsonDocument>(StorageHistoryDocument.CollectionName);
    private readonly IMongoCollection<BsonDocument> orders =
        databaseProvider.Database.GetCollection<BsonDocument>(OrderDocument.CollectionName);
    private readonly IMongoCollection<BsonDocument> importOrders =
        databaseProvider.Database.GetCollection<BsonDocument>(IpOrderDocument.CollectionName);
    private readonly IMongoCollection<BsonDocument> exportOrders =
        databaseProvider.Database.GetCollection<BsonDocument>(EpOrderDocument.CollectionName);

    public async Task<ProductVariantImageReference?> GetVariantImageReferenceAsync(
        string productId,
        int variantIndex,
        CancellationToken cancellationToken)
    {
        BsonDocument? document = await products.Find(BuildIdFilter(productId))
            .Project(Builders<BsonDocument>.Projection.Include("variant"))
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null) return null;

        BsonArray variants = ReadArray(document, "variant");
        if (variantIndex < 0 || variantIndex >= variants.Count || !variants[variantIndex].IsBsonDocument)
            return new ProductVariantImageReference(false, null);

        return new ProductVariantImageReference(true, ReadString(variants[variantIndex].AsBsonDocument, "imgUrl"));
    }

    public async Task<bool> IsProductImageReferencedElsewhereAsync(
        string productId,
        int variantIndex,
        string filename,
        CancellationToken cancellationToken)
    {
        List<BsonDocument> candidates = await products.Find(Builders<BsonDocument>.Filter.Exists("variant.imgUrl", true))
            .Project(Builders<BsonDocument>.Projection.Include("_id").Include("variant.imgUrl"))
            .ToListAsync(cancellationToken);
        foreach (BsonDocument candidate in candidates)
        {
            string candidateId = ReadId(candidate);
            BsonArray variants = ReadArray(candidate, "variant");
            for (int index = 0; index < variants.Count; index++)
            {
                if (string.Equals(candidateId, productId, StringComparison.OrdinalIgnoreCase) && index == variantIndex)
                    continue;
                if (variants[index].IsBsonDocument && ReferencesMediaFilename(
                        ReadString(variants[index].AsBsonDocument, "imgUrl"), "images", filename))
                    return true;
            }
        }
        return false;
    }

    public async Task<ProductRecord?> ClearVariantImageAsync(
        string productId,
        int variantIndex,
        string expectedImageUrl,
        CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = BuildIdFilter(productId) &
            Builders<BsonDocument>.Filter.Eq($"variant.{variantIndex}.imgUrl", expectedImageUrl);
        BsonDocument? document = await products.FindOneAndUpdateAsync(filter,
            Builders<BsonDocument>.Update.Set($"variant.{variantIndex}.imgUrl", string.Empty).Set("updatedAt", DateTime.UtcNow),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return document is null ? null : MapProduct(document);
    }

    public async Task<bool> IsInvoiceImageReferencedAsync(string filename, CancellationToken cancellationToken)
    {
        foreach (IMongoCollection<BsonDocument> collection in new[] { orders, importOrders, exportOrders })
        {
            List<BsonDocument> candidates = await collection.Find(Builders<BsonDocument>.Filter.Exists("images", true))
                .Project(Builders<BsonDocument>.Projection.Include("images"))
                .ToListAsync(cancellationToken);
            if (candidates.Any(candidate => ReadArray(candidate, "images")
                    .Any(value => value.IsString && ReferencesMediaFilename(value.AsString, "invoice-images", filename))))
                return true;
        }
        return false;
    }

    public async Task<ProductRecord?> FindEquivalentCodeAsync(
        string normalizedCode, string? excludeId, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Exists("code", true),
            Builders<BsonDocument>.Filter.Nin("code", new BsonArray { BsonNull.Value, string.Empty }));
        List<BsonDocument> candidates = await products.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include("_id").Include("name").Include("code"))
            .Limit(20_000)
            .ToListAsync(cancellationToken);
        BsonDocument? match = candidates.FirstOrDefault(candidate =>
            !string.Equals(ReadId(candidate), excludeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeCode(ReadString(candidate, "code")), normalizedCode, StringComparison.Ordinal));
        return match is null ? null : MinimalProduct(match);
    }

    public async Task<ProductMutationResult> CreateAsync(ProductMutation product, CancellationToken cancellationToken)
    {
        BsonDocument document = ToCreateDocument(product);
        try
        {
            await products.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return Conflict("Mã sản phẩm đã tồn tại");
        }
        return Success(document);
    }

    public async Task<ProductMutationResult> UpdateAsync(string id, ProductMutation product, CancellationToken cancellationToken)
    {
        // Legacy product PUT updates product metadata and (optionally) variant metadata.
        // It never replaces the variant array: order/inventory documents keep references to
        // the existing variant ids, and quantity is owned by stock/completion endpoints.
        BsonDocument setDocument = ToUpdateDocument(product, includeVariants: false);
        if (setDocument.ElementCount == 0 && product.Variants is not { Count: > 0 })
        {
            BsonDocument? current = await FindByIdDocumentAsync(id, cancellationToken);
            return current is null ? NotFound("Product not found") : Success(current);
        }
        BsonDocument? document;
        if (setDocument.ElementCount == 0)
        {
            document = await FindByIdDocumentAsync(id, cancellationToken);
        }
        else
        {
            setDocument["updatedAt"] = DateTime.UtcNow;
            try
            {
                document = await products.FindOneAndUpdateAsync(BuildIdFilter(id),
                    new BsonDocument("$set", setDocument),
                    new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
            }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return Conflict("Mã sản phẩm đã tồn tại");
            }
        }
        if (document is null) return NotFound("Product not found");
        if (product.Variants is { Count: > 0 })
        {
            document = await UpdateVariantMetadataAsync(id, product.Variants, cancellationToken);
            if (document is null) return Conflict("Product or variant changed");
        }
        return Success(document);
    }

    public async Task<ProductMutationResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        BsonDocument? document = await products.FindOneAndDeleteAsync(BuildIdFilter(id), cancellationToken: cancellationToken);
        return document is null ? NotFound("Product not found") : Success(document);
    }

    public async Task<ProductMutationResult> ToggleDisplayAsync(string id, CancellationToken cancellationToken)
    {
        BsonDocument? current = await FindByIdDocumentAsync(id, cancellationToken);
        if (current is null) return NotFound("Product not found");
        bool next = !ReadBool(current, "display", true);
        BsonDocument? document = await products.FindOneAndUpdateAsync(BuildIdFilter(id),
            Builders<BsonDocument>.Update.Set("display", next).Set("updatedAt", DateTime.UtcNow),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return document is null ? Conflict("Product changed") : Success(document);
    }

    public async Task<ProductMutationResult> DeleteManyAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Or(ids.Select(BuildIdFilter));
        DeleteResult result = await products.DeleteManyAsync(filter, cancellationToken);
        return new ProductMutationResult(ProductMutationStatus.Success, AffectedCount: result.DeletedCount);
    }

    public async Task<ProductMutationResult> AddVariantAsync(
        string productId, ProductVariantMutation variant, CancellationToken cancellationToken)
    {
        BsonDocument value = ToVariantDocument(variant, includeInventory: true);
        value["_id"] = ObjectId.GenerateNewId();
        BsonDocument? document = await products.FindOneAndUpdateAsync(BuildIdFilter(productId),
            Builders<BsonDocument>.Update.Push("variant", value).Set("updatedAt", DateTime.UtcNow),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return document is null ? NotFound("Product not found") : Success(document);
    }

    public async Task<ProductMutationResult> UpdateVariantAsync(
        string productId, int variantIndex, ProductVariantMutation variant, CancellationToken cancellationToken)
    {
        BsonDocument? current = await FindByIdDocumentAsync(productId, cancellationToken);
        if (current is null) return NotFound("Product not found");
        BsonArray variants = ReadArray(current, "variant");
        if (variantIndex >= variants.Count) return NotFound("Variant not found");
        BsonDocument set = new();
        foreach (BsonElement element in ToVariantDocument(variant, includeInventory: false))
            set[$"variant.{variantIndex}.{element.Name}"] = element.Value;
        if (set.ElementCount == 0) return Success(current);
        set["updatedAt"] = DateTime.UtcNow;
        BsonDocument? document = await products.FindOneAndUpdateAsync(BuildIdFilter(productId),
            new BsonDocument("$set", set),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return document is null ? Conflict("Variant changed") : Success(document);
    }

    public async Task<ProductMutationResult> DeleteVariantAsync(string productId, int variantIndex, CancellationToken cancellationToken)
    {
        BsonDocument? current = await FindByIdDocumentAsync(productId, cancellationToken);
        if (current is null) return NotFound("Product not found");
        BsonArray variants = ReadArray(current, "variant");
        if (variantIndex >= variants.Count || !variants[variantIndex].IsBsonDocument) return NotFound("Variant not found");
        if (variants.Count == 1) return Invalid("Sản phẩm phải còn ít nhất một phiên bản.");
        if (variantIndex != variants.Count - 1) return Invalid("Chỉ được xóa phiên bản cuối cùng để không làm lệch phiên bản trong các đơn hàng cũ.");
        BsonDocument target = variants[variantIndex].AsBsonDocument;
        if (ReadDouble(target, "quantityForSale") != 0 || ReadDouble(target, "quantityInStorage") != 0)
            return Invalid("Không thể xóa phiên bản vẫn còn tồn kho hoặc tồn khả dụng.");
        variants.RemoveAt(variantIndex);
        BsonDocument? document = await products.FindOneAndUpdateAsync(BuildIdFilter(productId),
            Builders<BsonDocument>.Update.Set("variant", variants).Set("updatedAt", DateTime.UtcNow),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return document is null ? Conflict("Variant changed") : Success(document);
    }

    public Task<ProductMutationResult> UpdateVariantEarnAsync(
        string productId, int variantIndex, double earn, CancellationToken cancellationToken) =>
        UpdatePricingAsync(productId, variantIndex, earn, null, cancellationToken);

    public Task<ProductMutationResult> UpdateVariantImportPriceAsync(
        string productId, int variantIndex, string importPrice, CancellationToken cancellationToken) =>
        UpdatePricingAsync(productId, variantIndex, null, importPrice, cancellationToken);

    public async Task<ProductMutationResult> AdjustPurchaseCountAsync(
        string productId, long delta, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = BuildIdFilter(productId);
        if (delta < 0) filter &= Builders<BsonDocument>.Filter.Gte("purchaseCount", -delta);
        BsonDocument? document = await products.FindOneAndUpdateAsync(filter,
            Builders<BsonDocument>.Update.Inc("purchaseCount", delta).Set("updatedAt", DateTime.UtcNow),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        if (document is not null) return Success(document);
        return await products.CountDocumentsAsync(BuildIdFilter(productId), cancellationToken: cancellationToken) == 0
            ? NotFound("Sản phẩm không tồn tại") : Invalid("Số lượng đã mua không đủ để giảm");
    }

    public async Task<long> BackfillDisplayAsync(CancellationToken cancellationToken)
    {
        UpdateResult result = await products.UpdateManyAsync(Builders<BsonDocument>.Filter.Exists("display", false),
            Builders<BsonDocument>.Update.Set("display", true), cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    public async Task<IReadOnlyList<ProductRecord>> FindByCodesAsync(
        IReadOnlyCollection<string> codes, bool includePrivate, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.In("code", codes);
        if (!includePrivate) filter &= Builders<BsonDocument>.Filter.Eq("display", true);
        List<BsonDocument> documents = await products.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include("_id").Include("code").Include("name")).ToListAsync(cancellationToken);
        return documents.Select(MinimalProduct).ToArray();
    }

    public async Task<ProductTypeMutationResult> CreateTypeAsync(string name, string icon, CancellationToken cancellationToken)
    {
        List<BsonDocument> existing = await types.Find(FilterDefinition<BsonDocument>.Empty)
            .Project(Builders<BsonDocument>.Projection.Include("_id").Include("Type")).ToListAsync(cancellationToken);
        BsonDocument? duplicate = existing.FirstOrDefault(item => NormalizeLabel(ReadString(item, "Type")) == NormalizeLabel(name));
        if (duplicate is not null)
            return new ProductTypeMutationResult(ProductMutationStatus.Conflict, Message: "Loại sản phẩm đã tồn tại. Hãy chọn loại đó để cập nhật.");
        BsonDocument document = new()
        {
            ["_id"] = ObjectId.GenerateNewId(), ["Type"] = name, ["icon"] = icon,
            ["createdAt"] = DateTime.UtcNow, ["updatedAt"] = DateTime.UtcNow,
        };
        await types.InsertOneAsync(document, cancellationToken: cancellationToken);
        return new ProductTypeMutationResult(ProductMutationStatus.Success, MapType(document));
    }

    public async Task<ProductTypeMutationResult> UpdateTypeAsync(
        string id, string name, string icon, CancellationToken cancellationToken)
    {
        BsonDocument? current = await types.Find(BuildIdFilter(id)).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (current is null) return TypeNotFound();
        string oldName = ReadString(current, "Type") ?? string.Empty;
        List<BsonDocument> existing = await types.Find(Builders<BsonDocument>.Filter.Ne("_id", current["_id"]))
            .Project(Builders<BsonDocument>.Projection.Include("Type")).ToListAsync(cancellationToken);
        if (existing.Any(item => NormalizeLabel(ReadString(item, "Type")) == NormalizeLabel(name)))
            return new ProductTypeMutationResult(ProductMutationStatus.Conflict, Message: "Tên loại sản phẩm đã tồn tại");
        await types.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", current["_id"]),
            Builders<BsonDocument>.Update.Set("Type", name).Set("icon", icon).Set("updatedAt", DateTime.UtcNow),
            cancellationToken: cancellationToken);
        long updatedProducts = oldName == name ? 0 : (await products.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("type", oldName), Builders<BsonDocument>.Update.Set("type", name),
            cancellationToken: cancellationToken)).ModifiedCount;
        long updatedCategories = await UpdateHomeCategoryTypesAsync(oldName, name, icon, cancellationToken);
        current["Type"] = name; current["icon"] = icon; current["updatedAt"] = DateTime.UtcNow;
        return new ProductTypeMutationResult(ProductMutationStatus.Success, MapType(current), updatedProducts, updatedCategories);
    }

    public async Task<ProductTypeMutationResult> DeleteTypeAsync(string id, bool requireUnused, CancellationToken cancellationToken)
    {
        BsonDocument? current = await types.Find(BuildIdFilter(id)).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (current is null) return TypeNotFound();
        string name = ReadString(current, "Type") ?? string.Empty;
        if (requireUnused)
        {
            long count = await products.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("type", name), cancellationToken: cancellationToken);
            if (count > 0) return new ProductTypeMutationResult(ProductMutationStatus.Conflict,
                Message: $"Không thể xóa vì đang có {count} sản phẩm thuộc loại này");
        }
        await types.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", current["_id"]), cancellationToken);
        return new ProductTypeMutationResult(ProductMutationStatus.Success, MapType(current));
    }

    public async Task<IReadOnlyList<ProductReview>?> GetReviewsAsync(string productId, CancellationToken cancellationToken)
    {
        BsonDocument? product = await products.Find(BuildIdFilter(productId))
            .Project(Builders<BsonDocument>.Projection.Include("reviews")).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return product is null ? null : ReadDocuments(product, "reviews").Select(MapReview).ToArray();
    }

    public async Task<ProductReviewMutationResult> CreateReviewAsync(
        string productId, string email, string? comment, double rating, CancellationToken cancellationToken)
    {
        BsonDocument? product = await FindByIdDocumentAsync(productId, cancellationToken);
        if (product is null) return ReviewNotFound("Product not found");
        BsonDocument review = new()
        {
            ["_id"] = ObjectId.GenerateNewId(), ["email"] = email, ["comment"] = comment ?? string.Empty,
            ["rating"] = rating, ["createdAt"] = DateTime.UtcNow,
        };
        EnsureReviews(product).Add(review);
        RecalculateReviewAggregates(product);
        ProductReviewMutationResult? persisted = await ReplaceReviewsAsync(product, cancellationToken);
        return persisted ?? new ProductReviewMutationResult(ProductMutationStatus.Success, MapReview(review));
    }

    public async Task<ProductReviewMutationResult> UpdateReviewAsync(
        string productId, string reviewId, string? comment, double? rating,
        string actorEmail, bool isModerator, CancellationToken cancellationToken)
    {
        BsonDocument? product = await FindByIdDocumentAsync(productId, cancellationToken);
        if (product is null) return ReviewNotFound("Product not found");
        BsonDocument? review = FindReview(product, reviewId);
        if (review is null) return ReviewNotFound("Review not found");
        if (!isModerator && !string.Equals(ReadString(review, "email"), actorEmail, StringComparison.OrdinalIgnoreCase))
            return new ProductReviewMutationResult(ProductMutationStatus.Forbidden, Message: "Bạn không có quyền chỉnh sửa đánh giá này.");
        if (!string.IsNullOrEmpty(comment)) review["comment"] = comment;
        if (rating.HasValue) review["rating"] = rating.Value;
        RecalculateReviewAggregates(product);
        ProductReviewMutationResult? persisted = await ReplaceReviewsAsync(product, cancellationToken);
        return persisted ?? new ProductReviewMutationResult(ProductMutationStatus.Success, MapReview(review));
    }

    public async Task<ProductReviewMutationResult> DeleteReviewAsync(
        string productId, string reviewId, string actorEmail, bool isModerator, CancellationToken cancellationToken)
    {
        BsonDocument? product = await FindByIdDocumentAsync(productId, cancellationToken);
        if (product is null) return ReviewNotFound("Product not found");
        BsonDocument? review = FindReview(product, reviewId);
        if (review is null) return ReviewNotFound("Review not found");
        if (!isModerator && !string.Equals(ReadString(review, "email"), actorEmail, StringComparison.OrdinalIgnoreCase))
            return new ProductReviewMutationResult(ProductMutationStatus.Forbidden, Message: "Bạn không có quyền chỉnh sửa đánh giá này.");
        EnsureReviews(product).Remove(review);
        RecalculateReviewAggregates(product);
        ProductReviewMutationResult? persisted = await ReplaceReviewsAsync(product, cancellationToken);
        return persisted ?? new ProductReviewMutationResult(ProductMutationStatus.Success, Product: MapProduct(product));
    }

    public async Task<ProductStockMutationResult> AdjustStockAsync(
        string productId, int variantIndex, ProductStockMutation mutation, CancellationToken cancellationToken)
    {
        BsonDocument? product = await FindByIdDocumentAsync(productId, cancellationToken);
        if (product is null) return new ProductStockMutationResult(ProductMutationStatus.NotFound, Message: "Product not found");
        BsonArray variants = ReadArray(product, "variant");
        if (variantIndex >= variants.Count || !variants[variantIndex].IsBsonDocument)
            return new ProductStockMutationResult(ProductMutationStatus.Invalid, Message: "Invalid variant index");
        BsonDocument variant = variants[variantIndex].AsBsonDocument;
        double nextSale = ReadDouble(variant, "quantityForSale") + mutation.Quantity;
        double nextStorage = ReadDouble(variant, "quantityInStorage") + mutation.Quantity;
        if (nextSale < 0 || nextStorage < 0)
            return new ProductStockMutationResult(ProductMutationStatus.Invalid, Message: "Insufficient stock");
        variant["quantityForSale"] = nextSale; variant["quantityInStorage"] = nextStorage; product["updatedAt"] = DateTime.UtcNow;
        ReplaceOneResult productWrite = await products.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", product["_id"]), product,
            cancellationToken: cancellationToken);
        if (productWrite.MatchedCount == 0) return new ProductStockMutationResult(ProductMutationStatus.Conflict, Message: "Product changed");
        BsonDocument history = new()
        {
            ["_id"] = ObjectId.GenerateNewId(), ["productId"] = product["_id"],
            ["productName"] = ReadString(product, "name") ?? string.Empty, ["quantity"] = mutation.Quantity,
            ["userName"] = mutation.UserName ?? string.Empty, ["orderId"] = mutation.OrderId ?? string.Empty,
            ["orderName"] = mutation.OrderName ?? string.Empty, ["isAIScan"] = mutation.IsAiScan,
            ["source"] = "product_manual", ["createdAt"] = DateTime.UtcNow, ["updatedAt"] = DateTime.UtcNow,
        };
        try { await storageHistories.InsertOneAsync(history, cancellationToken: cancellationToken); }
        catch
        {
            variant["quantityForSale"] = nextSale - mutation.Quantity;
            variant["quantityInStorage"] = nextStorage - mutation.Quantity;
            await products.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", product["_id"]), product, cancellationToken: cancellationToken);
            throw;
        }
        return new ProductStockMutationResult(ProductMutationStatus.Success, MapProduct(product), ReadId(history));
    }

    private async Task<ProductMutationResult> UpdatePricingAsync(
        string productId, int index, double? earn, string? importPrice, CancellationToken cancellationToken)
    {
        BsonDocument? current = await FindByIdDocumentAsync(productId, cancellationToken);
        if (current is null) return NotFound("Product not found");
        BsonArray variants = ReadArray(current, "variant");
        if (index >= variants.Count || !variants[index].IsBsonDocument) return Invalid("Invalid variant index");
        BsonDocument variant = variants[index].AsBsonDocument;
        double nextEarn = earn ?? ReadDouble(variant, "earn", 25);
        string nextImport = importPrice ?? ReadString(variant, "importPrice") ?? "0";
        double priceAmount = ParsePrice(nextImport);
        string nextPrice = (Math.Ceiling(priceAmount * (1 + nextEarn / 100) / 1000) * 1000)
            .ToString(CultureInfo.InvariantCulture);
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set($"variant.{index}.earn", nextEarn).Set($"variant.{index}.importPrice", nextImport)
            .Set($"variant.{index}.price", nextPrice).Set("adjusted", true).Set("updatedAt", DateTime.UtcNow);
        BsonDocument? document = await products.FindOneAndUpdateAsync(BuildIdFilter(productId), update,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        if (document is null) return Conflict("Variant changed");
        ProductRecord mapped = MapProduct(document);
        return new ProductMutationResult(ProductMutationStatus.Success, mapped, mapped.Variants[index]);
    }

    private async Task<long> UpdateHomeCategoryTypesAsync(
        string oldName, string newName, string icon, CancellationToken cancellationToken)
    {
        BsonDocument? manage = await manages.Find(FilterDefinition<BsonDocument>.Empty).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (manage is null || !manage.TryGetValue("homeCategoryConfig", out BsonValue homeValue) || !homeValue.IsBsonDocument ||
            !homeValue.AsBsonDocument.TryGetValue("items", out BsonValue itemsValue) || !itemsValue.IsBsonArray) return 0;
        long count = 0;
        foreach (BsonValue itemValue in itemsValue.AsBsonArray.Where(item => item.IsBsonDocument))
        {
            BsonDocument item = itemValue.AsBsonDocument;
            if (!string.Equals(ReadString(item, "type"), oldName, StringComparison.Ordinal)) continue;
            item["type"] = newName;
            if (string.Equals(ReadString(item, "label"), oldName, StringComparison.Ordinal)) item["label"] = newName;
            item["icon"] = icon;
            count++;
        }
        if (count > 0)
            await manages.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", manage["_id"]), manage, cancellationToken: cancellationToken);
        return count;
    }

    private async Task<BsonDocument?> FindByIdDocumentAsync(string id, CancellationToken cancellationToken) =>
        await products.Find(BuildIdFilter(id)).Limit(1).FirstOrDefaultAsync(cancellationToken);

    private async Task<ProductReviewMutationResult?> ReplaceReviewsAsync(BsonDocument product, CancellationToken cancellationToken)
    {
        product["updatedAt"] = DateTime.UtcNow;
        ReplaceOneResult result = await products.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", product["_id"]), product,
            cancellationToken: cancellationToken);
        return result.MatchedCount == 0
            ? new ProductReviewMutationResult(ProductMutationStatus.Conflict, Message: "Review changed") : null;
    }

    private static BsonArray EnsureReviews(BsonDocument product)
    {
        if (!product.TryGetValue("reviews", out BsonValue value) || !value.IsBsonArray) product["reviews"] = new BsonArray();
        return product["reviews"].AsBsonArray;
    }

    private static BsonDocument? FindReview(BsonDocument product, string reviewId) => EnsureReviews(product)
        .Where(value => value.IsBsonDocument).Select(value => value.AsBsonDocument)
        .FirstOrDefault(review => string.Equals(ReadId(review), reviewId, StringComparison.OrdinalIgnoreCase));

    private static void RecalculateReviewAggregates(BsonDocument product)
    {
        BsonArray reviews = EnsureReviews(product);
        double total = reviews.Where(value => value.IsBsonDocument).Sum(value => ReadDouble(value.AsBsonDocument, "rating"));
        product["reviewCount"] = reviews.Count; product["totalRating"] = total;
        product["averageReviews"] = reviews.Count == 0 ? 0D : total / reviews.Count;
    }

    private static ProductReviewMutationResult ReviewNotFound(string message) =>
        new(ProductMutationStatus.NotFound, Message: message);

    private async Task<BsonDocument?> UpdateVariantMetadataAsync(
        string productId,
        IReadOnlyList<ProductVariantMutation> incomingVariants,
        CancellationToken cancellationToken)
    {
        BsonDocument? current = await FindByIdDocumentAsync(productId, cancellationToken);
        if (current is null) return null;

        BsonArray existingVariants = ReadArray(current, "variant");
        for (int incomingIndex = 0; incomingIndex < incomingVariants.Count; incomingIndex++)
        {
            ProductVariantMutation incoming = incomingVariants[incomingIndex];
            int variantIndex = ResolveVariantIndex(existingVariants, incoming.Id, incomingIndex);
            if (variantIndex < 0 || variantIndex >= existingVariants.Count || !existingVariants[variantIndex].IsBsonDocument)
                continue;

            BsonDocument metadata = ToVariantDocument(incoming, includeInventory: false);
            metadata.Remove("_id");
            if (metadata.ElementCount == 0) continue;

            BsonDocument existing = existingVariants[variantIndex].AsBsonDocument;
            FilterDefinition<BsonDocument> filter = BuildIdFilter(productId);
            if (existing.TryGetValue("_id", out BsonValue existingId) && !existingId.IsBsonNull)
                filter &= Builders<BsonDocument>.Filter.Eq($"variant.{variantIndex}._id", existingId);

            List<UpdateDefinition<BsonDocument>> updates = metadata.Elements
                .Select(element => Builders<BsonDocument>.Update.Set($"variant.{variantIndex}.{element.Name}", element.Value))
                .Append(Builders<BsonDocument>.Update.Set("updatedAt", DateTime.UtcNow))
                .ToList();
            current = await products.FindOneAndUpdateAsync(
                filter,
                Builders<BsonDocument>.Update.Combine(updates),
                new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
            if (current is null) return null;
            existingVariants = ReadArray(current, "variant");
        }

        return current;
    }

    private static int ResolveVariantIndex(BsonArray variants, string? incomingId, int fallbackIndex)
    {
        if (!string.IsNullOrWhiteSpace(incomingId))
        {
            for (int index = 0; index < variants.Count; index++)
            {
                if (variants[index].IsBsonDocument &&
                    string.Equals(ReadId(variants[index].AsBsonDocument), incomingId, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
            return -1;
        }

        return fallbackIndex;
    }

    private static BsonDocument ToCreateDocument(ProductMutation value)
    {
        BsonDocument document = ToUpdateDocument(value, includeVariants: true);
        document["_id"] = ObjectId.GenerateNewId();
        document["nameUnsigned"] = RemoveTones(value.Name ?? string.Empty);
        document["display"] = value.Display ?? true;
        document["adjusted"] = value.Adjusted ?? true;
        document["purchaseCount"] = 0L; document["reviews"] = new BsonArray();
        document["totalRating"] = 0D; document["reviewCount"] = 0L; document["averageReviews"] = 0D;
        document["createdAt"] = DateTime.UtcNow; document["updatedAt"] = DateTime.UtcNow;
        if (!document.Contains("variant")) document["variant"] = new BsonArray { ToVariantDocument(new ProductVariantMutation(
            null, string.Empty, string.Empty, 25, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, string.Empty), true) };
        return document;
    }

    private static BsonDocument ToUpdateDocument(ProductMutation value, bool includeVariants)
    {
        BsonDocument document = new();
        Put(document, "type", value.Type); Put(document, "name", value.Name); Put(document, "code", value.Code);
        Put(document, "brand", value.Brand); Put(document, "section", value.Section); Put(document, "value", value.Value);
        Put(document, "warranty", value.Warranty); Put(document, "vat", value.Vat); Put(document, "adjusted", value.Adjusted);
        Put(document, "display", value.Display); Put(document, "solution", value.Solution); Put(document, "description", value.Description);
        Put(document, "features", value.Features); Put(document, "operatingMethod", value.OperatingMethod);
        Put(document, "advantages", value.Advantages); Put(document, "specifications", value.Specifications);
        if (value.Name is not null) document["nameUnsigned"] = RemoveTones(value.Name);
        if (value.InfoDoc is not null) document["infoDoc"] = new BsonDocument
        {
            ["manual"] = value.InfoDoc.Manual ?? string.Empty, ["dataSheet"] = value.InfoDoc.DataSheet ?? string.Empty,
            ["catalog"] = value.InfoDoc.Catalog ?? string.Empty, ["others"] = value.InfoDoc.Others ?? string.Empty,
        };
        if (value.Documents is not null)
            document["documents"] = new BsonArray(value.Documents.Select(ToProductLinkDocument));
        if (includeVariants && value.Variants is not null) document["variant"] = new BsonArray(value.Variants.Select(item =>
        {
            BsonDocument variant = ToVariantDocument(item, true); variant["_id"] = ObjectId.GenerateNewId(); return variant;
        }));
        return document;
    }

    private static BsonDocument ToVariantDocument(ProductVariantMutation value, bool includeInventory)
    {
        BsonDocument document = new();
        Put(document, "price", value.Price); Put(document, "importPrice", value.ImportPrice); Put(document, "earn", value.Earn);
        Put(document, "imgUrl", value.ImageUrl); Put(document, "color", value.Color); Put(document, "shape", value.Shape);
        Put(document, "buttonCount", value.ButtonCount); Put(document, "frame", value.Frame); Put(document, "note", value.Note);
        if (includeInventory) { Put(document, "quantityForSale", value.QuantityForSale); Put(document, "quantityInStorage", value.QuantityInStorage); }
        return document;
    }

    private static BsonDocument ToProductLinkDocument(ProductLinkMutation value)
    {
        ObjectId id = value.Id is null
            ? ObjectId.GenerateNewId()
            : ObjectId.Parse(value.Id);
        return new BsonDocument
        {
            ["_id"] = id,
            ["label"] = value.Label ?? string.Empty,
            ["url"] = value.Url ?? string.Empty,
            ["sourceType"] = value.SourceType ?? string.Empty,
        };
    }

    private static void Put(BsonDocument document, string field, string? value) { if (value is not null) document[field] = value; }
    private static void Put(BsonDocument document, string field, bool? value) { if (value.HasValue) document[field] = value.Value; }
    private static void Put(BsonDocument document, string field, double? value) { if (value.HasValue) document[field] = value.Value; }

    private static ProductMutationResult Success(BsonDocument document)
    {
        ProductRecord mapped = MapProduct(document);
        return new ProductMutationResult(ProductMutationStatus.Success, mapped);
    }
    private static ProductMutationResult NotFound(string message) => new(ProductMutationStatus.NotFound, Message: message);
    private static ProductMutationResult Conflict(string message) => new(ProductMutationStatus.Conflict, Message: message);
    private static ProductMutationResult Invalid(string message) => new(ProductMutationStatus.Invalid, Message: message);
    private static ProductTypeMutationResult TypeNotFound() => new(ProductMutationStatus.NotFound, Message: "Không tìm thấy loại sản phẩm");

    private static FilterDefinition<BsonDocument> BuildIdFilter(string id)
    {
        var builder = Builders<BsonDocument>.Filter;
        return ObjectId.TryParse(id, out ObjectId value)
            ? builder.Or(builder.Eq("_id", value), builder.Eq("_id", id)) : builder.Eq("_id", id);
    }

    private static ProductTypeRecord MapType(BsonDocument document) => new(ReadId(document), ReadString(document, "Type"),
        ReadString(document, "icon"), ReadDate(document, "createdAt"), ReadDate(document, "updatedAt"));

    private static ProductRecord MinimalProduct(BsonDocument document) => new(ReadId(document), null, ReadString(document, "name"), null,
        null, ReadString(document, "code"), null, null, null, null, null, [], null, [], 0, [], 0, 0, 0, null,
        null, null, null, null, null, null, null, null, false);

    private static ProductRecord MapProduct(BsonDocument document)
    {
        string? type = ReadString(document, "type"); string? brand = ReadString(document, "brand"); string? section = ReadString(document, "section");
        return new ProductRecord(ReadId(document), type, ReadString(document, "name"), ReadString(document, "nameUnsigned"),
            ReadBoolNullable(document, "display"), ReadString(document, "code"), ReadString(document, "vat"), ReadBoolNullable(document, "adjusted"),
            brand, section, ReadString(document, "value"), ReadDocuments(document, "variant").Select(MapVariant).ToArray(),
            MapInfo(document), ReadDocuments(document, "documents").Select(item => new ProductLink(ReadId(item), ReadString(item, "label"), ReadString(item, "url"), ReadString(item, "sourceType"))).ToArray(),
            ReadLong(document, "purchaseCount"), ReadDocuments(document, "reviews").Select(item => new ProductReview(ReadId(item), ReadString(item, "email"), ReadString(item, "comment"), ReadNullableDouble(item, "rating"), ReadDate(item, "createdAt"))).ToArray(),
            ReadDouble(document, "totalRating"), ReadLong(document, "reviewCount"), ReadDouble(document, "averageReviews"), ReadString(document, "warranty"),
            ReadString(document, "solution"), ReadString(document, "description"), ReadString(document, "features"), ReadString(document, "operatingMethod"),
            ReadString(document, "advantages"), ReadString(document, "specifications"), ReadDate(document, "createdAt"), ReadDate(document, "updatedAt"),
            !string.IsNullOrWhiteSpace(type) && !string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(section));
    }

    private static ProductVariant MapVariant(BsonDocument item) => new(ReadId(item), ReadString(item, "price"), ReadString(item, "importPrice"),
        ReadNullableDouble(item, "earn"), ReadString(item, "imgUrl"), ReadString(item, "color"), ReadString(item, "shape"), ReadString(item, "buttonCount"),
        ReadString(item, "frame"), ReadNullableDouble(item, "quantityForSale"), ReadNullableDouble(item, "quantityInStorage"), ReadString(item, "note"));
    private static ProductReview MapReview(BsonDocument item) => new(ReadId(item), ReadString(item, "email"), ReadString(item, "comment"),
        ReadNullableDouble(item, "rating"), ReadDate(item, "createdAt"));
    private static ProductInfo? MapInfo(BsonDocument document) => document.TryGetValue("infoDoc", out BsonValue value) && value.IsBsonDocument
        ? new ProductInfo(ReadString(value.AsBsonDocument, "manual"), ReadString(value.AsBsonDocument, "dataSheet"), ReadString(value.AsBsonDocument, "catalog"), ReadString(value.AsBsonDocument, "others")) : null;
    private static List<BsonDocument> ReadDocuments(BsonDocument document, string field) => ReadArray(document, field).Where(item => item.IsBsonDocument).Select(item => item.AsBsonDocument).ToList();
    private static BsonArray ReadArray(BsonDocument document, string field) => document.TryGetValue(field, out BsonValue value) && value.IsBsonArray ? value.AsBsonArray : [];
    private static string ReadId(BsonDocument document) => document.TryGetValue("_id", out BsonValue value) && !value.IsBsonNull ? value.ToString() ?? string.Empty : string.Empty;
    private static string? ReadString(BsonDocument document, string field) => document.TryGetValue(field, out BsonValue value) && !value.IsBsonNull ? value.IsString ? value.AsString : value.ToString() : null;
    private static bool ReadBool(BsonDocument document, string field, bool fallback) => document.TryGetValue(field, out BsonValue value) && value.IsBoolean ? value.AsBoolean : fallback;
    private static bool? ReadBoolNullable(BsonDocument document, string field) => document.TryGetValue(field, out BsonValue value) && value.IsBoolean ? value.AsBoolean : null;
    private static long ReadLong(BsonDocument document, string field) => document.TryGetValue(field, out BsonValue value) && value.IsNumeric ? value.ToInt64() : 0;
    private static double ReadDouble(BsonDocument document, string field, double fallback = 0) => document.TryGetValue(field, out BsonValue value) && value.IsNumeric ? value.ToDouble() : fallback;
    private static double? ReadNullableDouble(BsonDocument document, string field) => document.TryGetValue(field, out BsonValue value) && value.IsNumeric ? value.ToDouble() : null;
    private static DateTimeOffset? ReadDate(BsonDocument document, string field) => document.TryGetValue(field, out BsonValue value) && value.IsValidDateTime ? new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero) : null;
    private static string NormalizeCode(string? value) => new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    private static string NormalizeLabel(string? value) => string.Join(' ', RemoveTones(value ?? string.Empty).ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string RemoveTones(string value) { string normalized = value.Normalize(NormalizationForm.FormD); return new string(normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).Select(ch => ch is 'đ' ? 'd' : ch is 'Đ' ? 'D' : ch).ToArray()).Normalize(NormalizationForm.FormC); }
    private static double ParsePrice(string value) { string normalized = value.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.'); return double.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out double parsed) ? parsed : 0; }

    private static bool ReferencesMediaFilename(string? mediaUrl, string route, string filename)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl) || mediaUrl.Length > 2_048 || mediaUrl.Contains('\0')) return false;
        if (!Uri.TryCreate(mediaUrl, UriKind.RelativeOrAbsolute, out Uri? uri)) return false;
        string path = uri.IsAbsoluteUri ? uri.AbsolutePath : mediaUrl.Split('?', '#')[0];
        string escapedRoute = Uri.EscapeDataString(route);
        string escapedFilename = Uri.EscapeDataString(filename);
        string normalized = path.Replace('\\', '/');
        return string.Equals(normalized, $"/{escapedRoute}/{escapedFilename}", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, $"/api/{escapedRoute}/{escapedFilename}", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, $"/{route}/{filename}", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, $"/api/{route}/{filename}", StringComparison.OrdinalIgnoreCase);
    }
}
