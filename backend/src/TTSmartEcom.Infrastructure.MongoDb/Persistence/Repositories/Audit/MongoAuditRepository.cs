using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Domain.Audit;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Audit;

public sealed class MongoAuditRepository(IMongoDatabaseProvider databaseProvider) : IAuditRepository, IActivityLogWriter
{
    /// <summary>
    /// Legacy Mongoose owns a 90-day TTL index on createdAt (7,776,000 seconds).
    /// This repository deliberately does not create or modify indexes at startup;
    /// index reconciliation must remain an explicit, separately approved operation.
    /// </summary>
    internal static readonly TimeSpan LegacyRetention = TimeSpan.FromDays(90);

    internal static readonly IReadOnlyDictionary<string, string> LegacyActionLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["create_product"] = "Tạo sản phẩm", ["update_product"] = "Sửa sản phẩm", ["delete_product"] = "Xóa sản phẩm",
            ["update_variant"] = "Sửa biến thể", ["update_earn"] = "Sửa % lợi nhuận", ["update_import_price"] = "Sửa giá nhập",
            ["toggle_display"] = "Ẩn/Hiện sản phẩm", ["add_variant"] = "Thêm biến thể", ["delete_variant"] = "Xóa biến thể",
            ["create_user"] = "Tạo tài khoản", ["update_user"] = "Sửa tài khoản", ["delete_user"] = "Xóa tài khoản",
            ["update_user_permissions"] = "Sửa quyền tài khoản", ["assign_user_stations"] = "Phân trạm cho tài khoản",
            ["rotate_autologin_token"] = "Xoay mã đăng nhập tự động",
            ["create_station"] = "Tạo trạm trộn", ["update_station"] = "Sửa trạm trộn", ["update_station_products"] = "Cập nhật sản phẩm trạm", ["delete_station"] = "Xóa trạm trộn",
            ["add_chip_attr"] = "Thêm thuộc tính sản phẩm", ["remove_chip_attr"] = "Xóa thuộc tính sản phẩm",
            ["create_brand"] = "Thêm thương hiệu", ["delete_brand"] = "Xóa thương hiệu",
            ["create_type"] = "Thêm loại sản phẩm", ["update_type"] = "Cập nhật loại sản phẩm", ["delete_type"] = "Xóa loại sản phẩm",
            ["create_section"] = "Thêm phân loại", ["update_section"] = "Sửa phân loại", ["delete_section"] = "Xóa phân loại",
            ["create_section_value"] = "Thêm giá trị phân loại", ["update_section_value"] = "Sửa giá trị phân loại", ["delete_section_value"] = "Xóa giá trị phân loại",
            ["update_settings"] = "Cập nhật cấu hình chung", ["update_introduction"] = "Sửa trang giới thiệu", ["update_policy"] = "Sửa trang chính sách",
            ["update_policies"] = "Cập nhật trang chính sách", ["update_homepage_section"] = "Sửa phần trang chủ", ["update_home_categories"] = "Cập nhật danh mục trang chủ",
            ["update_zalo_settings"] = "Cập nhật cấu hình Zalo OA",
            ["update_telegram_settings"] = "Cập nhật cấu hình Telegram", ["create_telegram_recipient"] = "Thêm người/nhóm nhận Telegram",
            ["update_telegram_recipient"] = "Sửa người/nhóm nhận Telegram", ["delete_telegram_recipient"] = "Xóa người/nhóm nhận Telegram",
            ["create_voice_vocab"] = "Thêm từ vựng tìm kiếm giọng nói", ["update_voice_vocab"] = "Sửa từ vựng tìm kiếm giọng nói",
            ["delete_voice_vocab"] = "Xóa từ vựng tìm kiếm giọng nói",
        };

    private readonly IMongoCollection<BsonDocument> logs = databaseProvider.Database.GetCollection<BsonDocument>(ActivityLogDocument.CollectionName);
    private readonly IMongoCollection<ActivityLogDocument> logWriter = databaseProvider.Database.GetCollection<ActivityLogDocument>(ActivityLogDocument.CollectionName);

    public Task AppendAsync(ActivityLogWriteEntry entry, CancellationToken cancellationToken) =>
        logWriter.InsertOneAsync(ToDocument(entry, DateTime.UtcNow), cancellationToken: cancellationToken);

    public async Task AppendManyAsync(
        IReadOnlyCollection<ActivityLogWriteEntry> entries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        ActivityLogDocument[] documents = entries.Select(entry => ToDocument(entry, now)).ToArray();
        await logWriter.InsertManyAsync(
            documents,
            new InsertManyOptions { IsOrdered = true },
            cancellationToken);
    }

    public async Task<ActivityLogPage> QueryAsync(ActivityLogQuery query, CancellationToken cancellationToken)
    {
        FilterDefinitionBuilder<BsonDocument> builder = Builders<BsonDocument>.Filter;
        List<FilterDefinition<BsonDocument>> clauses = [];
        if (query.StartDate.HasValue || query.EndDate.HasValue)
        {
            if (query.StartDate.HasValue) clauses.Add(builder.Gte("createdAt", query.StartDate.Value.UtcDateTime));
            if (query.EndDate.HasValue) clauses.Add(builder.Lte("createdAt", query.EndDate.Value.UtcDateTime));
        }
        AddRegex(clauses, builder, "userName", query.UserName);
        AddRegex(clauses, builder, "productName", query.ProductName);
        if (!string.IsNullOrWhiteSpace(query.Action)) clauses.Add(builder.Eq("action", query.Action));
        FilterDefinition<BsonDocument> filter = clauses.Count == 0 ? builder.Empty : builder.And(clauses);
        long total = await logs.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        List<BsonDocument> values = await logs.Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("createdAt")).Skip((query.Page - 1) * query.Limit).Limit(query.Limit).ToListAsync(cancellationToken);
        ActivityLog[] mapped = values.Select(Map).ToArray();
        ActivityLogReferences references = await BuildReferencesAsync(mapped, cancellationToken);
        return new ActivityLogPage(true, query.Page, query.Limit, total, (int)Math.Ceiling(total / (double)query.Limit), mapped, LegacyActionLabels, references);
    }

    private async Task<ActivityLogReferences> BuildReferencesAsync(
        IReadOnlyList<ActivityLog> values,
        CancellationToken cancellationToken)
    {
        HashSet<string> productIds = [];
        HashSet<string> stationIds = [];
        foreach (ActivityLog log in values)
        {
            foreach (ActivityLogDetail detail in log.Details)
            {
                string field = detail.Field?.Trim() ?? string.Empty;
                if (!field.Equals("productId", StringComparison.OrdinalIgnoreCase) &&
                    !field.Equals("station", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (string id in ExtractObjectIds(detail.OldValue).Concat(ExtractObjectIds(detail.NewValue)))
                {
                    if (field.Equals("productId", StringComparison.OrdinalIgnoreCase)) productIds.Add(id);
                    else stationIds.Add(id);
                }
            }
        }

        Dictionary<string, string> products = await ResolveReferenceLabelsAsync(
            ProductDocument.CollectionName, productIds, "code", "name", BuildProductReferenceLabel, cancellationToken);
        Dictionary<string, string> stations = await ResolveReferenceLabelsAsync(
            StationDocument.CollectionName, stationIds, "stationCode", "stationName", BuildStationReferenceLabel, cancellationToken);
        return new ActivityLogReferences(products, stations);
    }

    private async Task<Dictionary<string, string>> ResolveReferenceLabelsAsync(
        string collectionName,
        IEnumerable<string> ids,
        string primaryField,
        string secondaryField,
        Func<BsonDocument, string> labelFactory,
        CancellationToken cancellationToken)
    {
        string[] values = ids.Distinct(StringComparer.OrdinalIgnoreCase).Take(200).ToArray();
        if (values.Length == 0) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        IMongoCollection<BsonDocument> collection = databaseProvider.Database.GetCollection<BsonDocument>(collectionName);
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Or(values.Select(BuildReferenceIdFilter));
        List<BsonDocument> documents = await collection.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include("_id").Include(primaryField).Include(secondaryField))
            .ToListAsync(cancellationToken);
        Dictionary<string, string> labels = new(StringComparer.OrdinalIgnoreCase);
        foreach (BsonDocument document in documents)
        {
            labels[ReadId(document)] = labelFactory(document);
        }
        return labels;
    }

    internal static string BuildProductReferenceLabel(BsonDocument document) =>
        ReadString(document, "code") ?? ReadString(document, "name") ?? ReadId(document);

    internal static string BuildStationReferenceLabel(BsonDocument document)
    {
        string id = ReadId(document);
        string code = ReadString(document, "stationCode")?.Trim() ?? string.Empty;
        string name = ReadString(document, "stationName")?.Trim() ?? string.Empty;
        return code.Length > 0 && name.Length > 0
            ? $"{code} - {name}"
            : code.Length > 0 ? code : name.Length > 0 ? name : id;
    }

    private static IEnumerable<string> ExtractObjectIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(value, @"\b[0-9a-fA-F]{24}\b"))
            yield return match.Value.ToLowerInvariant();
    }

    private static FilterDefinition<BsonDocument> BuildReferenceIdFilter(string id)
    {
        FilterDefinitionBuilder<BsonDocument> builder = Builders<BsonDocument>.Filter;
        return ObjectId.TryParse(id, out ObjectId objectId)
            ? builder.Or(builder.Eq("_id", objectId), builder.Eq("_id", id))
            : builder.Eq("_id", id);
    }

    private static void AddRegex(List<FilterDefinition<BsonDocument>> clauses, FilterDefinitionBuilder<BsonDocument> builder, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        string escaped = System.Text.RegularExpressions.Regex.Escape(value.Trim()[..Math.Min(value.Trim().Length, 100)]);
        clauses.Add(builder.Regex(field, new BsonRegularExpression(escaped, "i")));
    }

    private static ActivityLog Map(BsonDocument d)
    {
        string? productId = d.TryGetValue("productId", out BsonValue pid) && !pid.IsBsonNull ? pid.ToString() : null;
        return new ActivityLog(ReadId(d), ReadString(d, "userName"), ReadString(d, "action"), productId, ReadString(d, "productName"),
            ReadArray(d, "details").Where(static x => x.IsBsonDocument).Select(static x => new ActivityLogDetail(ReadString(x.AsBsonDocument, "field"), ReadString(x.AsBsonDocument, "oldValue") ?? string.Empty, ReadString(x.AsBsonDocument, "newValue") ?? string.Empty)).ToArray(),
            ReadDate(d, "createdAt"), ReadDate(d, "updatedAt"));
    }
    private static string ReadId(BsonDocument d) => d.TryGetValue("_id", out BsonValue value) && !value.IsBsonNull ? value.ToString() ?? string.Empty : string.Empty;
    private static string? ReadString(BsonDocument d, string field) => d.TryGetValue(field, out BsonValue value) && !value.IsBsonNull ? value.IsString ? value.AsString : value.ToString() : null;
    private static DateTimeOffset? ReadDate(BsonDocument d, string field) => d.TryGetValue(field, out BsonValue value) && value.IsValidDateTime ? new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero) : null;
    private static BsonArray ReadArray(BsonDocument d, string field) => d.TryGetValue(field, out BsonValue value) && value.IsBsonArray ? value.AsBsonArray : [];

    internal static ActivityLogDocument ToDocument(ActivityLogWriteEntry entry, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.UserName))
        {
            throw new ArgumentException("Activity-log userName is required.", nameof(entry));
        }
        if (string.IsNullOrWhiteSpace(entry.Action))
        {
            throw new ArgumentException("Activity-log action is required.", nameof(entry));
        }

        ObjectId? productId = null;
        if (entry.ProductId is not null)
        {
            if (!ObjectId.TryParse(entry.ProductId, out ObjectId parsed))
            {
                throw new ArgumentException("Activity-log productId must be a MongoDB ObjectId.", nameof(entry));
            }
            productId = parsed;
        }

        DateTime utcNow = now.Kind == DateTimeKind.Utc ? now : now.ToUniversalTime();
        return new ActivityLogDocument
        {
            Id = ObjectId.GenerateNewId(),
            Version = 0,
            UserName = entry.UserName,
            Action = entry.Action,
            ProductId = productId,
            ProductName = entry.ProductName,
            Details = entry.Details.Select(static detail => new ActivityLogDocument.ActivityLogDetailDocument
            {
                Id = ObjectId.GenerateNewId(),
                Field = detail.Field,
                OldValue = detail.OldValue ?? string.Empty,
                NewValue = detail.NewValue ?? string.Empty,
            }).ToList(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }
}
