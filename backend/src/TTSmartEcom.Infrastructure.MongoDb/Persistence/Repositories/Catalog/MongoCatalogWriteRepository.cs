using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Abstractions.Catalog;
using TTSmartEcom.Domain.Catalog;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Catalog;

public sealed class MongoCatalogWriteRepository(IMongoDatabaseProvider databaseProvider)
    : ICatalogWriteRepository, ICatalogMediaRepository
{
    private readonly IMongoCollection<BsonDocument> brands = databaseProvider.Database.GetCollection<BsonDocument>(BrandDocument.CollectionName);
    private readonly IMongoCollection<BsonDocument> chips = databaseProvider.Database.GetCollection<BsonDocument>(ChipDocument.CollectionName);
    private readonly IMongoCollection<BsonDocument> sections = databaseProvider.Database.GetCollection<BsonDocument>(SectionDocument.CollectionName);

    public async Task<bool> IsSectionImageReferencedAsync(string filename, CancellationToken cancellationToken)
    {
        List<BsonDocument> documents = await sections.Find(Builders<BsonDocument>.Filter.Exists("Section.imgUrl", true))
            .Project(Builders<BsonDocument>.Projection.Include("Section.imgUrl"))
            .ToListAsync(cancellationToken);
        return documents.SelectMany(ReadItems).Any(section => ReferencesSectionImage(ReadString(section, "imgUrl"), filename));
    }

    public async Task<CatalogMutationResult<BrandRecord>> CreateBrandAsync(string name, CancellationToken cancellationToken)
    {
        List<BsonDocument> values = await brands.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
        BsonDocument? duplicate = values.FirstOrDefault(item => NormalizeKey(ReadString(item, "Brand")) == NormalizeKey(name));
        if (duplicate is not null) return Success(new BrandRecord(ReadId(duplicate), ReadString(duplicate, "Brand")));
        BsonDocument document = new() { ["_id"] = ObjectId.GenerateNewId(), ["Brand"] = name };
        await brands.InsertOneAsync(document, cancellationToken: cancellationToken);
        return Success(new BrandRecord(ReadId(document), name));
    }

    public async Task<CatalogMutationResult<BrandRecord>> DeleteBrandAsync(string id, CancellationToken cancellationToken)
    {
        BsonDocument? document = await brands.FindOneAndDeleteAsync(BuildIdFilter(id), cancellationToken: cancellationToken);
        return document is null ? NotFound<BrandRecord>("Brand not found") : Success(new BrandRecord(ReadId(document), ReadString(document, "Brand")));
    }

    public async Task<ChipValuesRecord?> GetChipValuesAsync(CancellationToken cancellationToken)
    {
        BsonDocument? document = await chips.Find(FilterDefinition<BsonDocument>.Empty).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : MapChip(document);
    }

    public async Task<CatalogMutationResult<ChipValuesRecord>> AddChipValueAsync(string type, string value, CancellationToken cancellationToken)
    {
        BsonDocument? document = await chips.Find(FilterDefinition<BsonDocument>.Empty).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            document = NewChip(); document[type].AsBsonArray.Add(value);
            await chips.InsertOneAsync(document, cancellationToken: cancellationToken);
            return Success(MapChip(document));
        }
        if (ReadStrings(document, type).Contains(value, StringComparer.Ordinal))
            return Conflict<ChipValuesRecord>($"{type} already exists");
        BsonDocument? updated = await chips.FindOneAndUpdateAsync(Builders<BsonDocument>.Filter.Eq("_id", document["_id"]),
            Builders<BsonDocument>.Update.AddToSet(type, value),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is null ? Conflict<ChipValuesRecord>("Chip changed") : Success(MapChip(updated));
    }

    public async Task<CatalogMutationResult<ChipValuesRecord>> RemoveChipValueAsync(string type, string value, CancellationToken cancellationToken)
    {
        BsonDocument? document = await chips.FindOneAndUpdateAsync(Builders<BsonDocument>.Filter.AnyEq(type, value),
            Builders<BsonDocument>.Update.Pull(type, value),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return document is null ? NotFound<ChipValuesRecord>("Không tìm thấy chip phù hợp để xóa") : Success(MapChip(document));
    }

    public async Task<CatalogMutationResult<SectionDocumentRecord>> CreateSectionAsync(string name, CancellationToken cancellationToken)
    {
        BsonDocument? document = await GetSectionAsync(cancellationToken);
        if (document is null)
        {
            document = NewSections(); document["Section"].AsBsonArray.Add(NewSectionItem(name));
            await sections.InsertOneAsync(document, cancellationToken: cancellationToken);
            return Success(MapSection(document));
        }
        if (ReadItems(document).Any(item => string.Equals(ReadString(item, "name"), name, StringComparison.Ordinal)))
            return Conflict<SectionDocumentRecord>("Section đã tồn tại");
        BsonDocument? updated = await sections.FindOneAndUpdateAsync(Builders<BsonDocument>.Filter.Eq("_id", document["_id"]),
            Builders<BsonDocument>.Update.Push("Section", NewSectionItem(name)),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is null ? Conflict<SectionDocumentRecord>("Section changed") : Success(MapSection(updated));
    }

    public Task<CatalogMutationResult<SectionDocumentRecord>> RenameSectionAsync(
        string oldName, string newName, CancellationToken cancellationToken) =>
        MutateSectionAsync(oldName, document => document["name"] = newName, cancellationToken);

    public async Task<CatalogMutationResult<SectionDocumentRecord>> DeleteSectionAsync(string name, CancellationToken cancellationToken)
    {
        BsonDocument? document = await GetSectionAsync(cancellationToken);
        if (document is null) return NotFound<SectionDocumentRecord>("Không tìm thấy dữ liệu");
        BsonArray items = document["Section"].AsBsonArray;
        BsonValue? match = items.FirstOrDefault(item => item.IsBsonDocument && string.Equals(ReadString(item.AsBsonDocument, "name"), name, StringComparison.Ordinal));
        if (match is null) return NotFound<SectionDocumentRecord>("Không tìm thấy section");
        items.Remove(match);
        return await ReplaceSectionAsync(document, cancellationToken);
    }

    public Task<CatalogMutationResult<SectionDocumentRecord>> AddSectionValueAsync(
        string name, string value, CancellationToken cancellationToken) =>
        MutateSectionAsync(name, section =>
        {
            BsonArray values = EnsureArray(section, "value");
            if (!values.Any(item => item.IsString && item.AsString == value)) values.Add(value);
        }, cancellationToken);

    public Task<CatalogMutationResult<SectionDocumentRecord>> UpdateSectionValueAsync(
        string name, string oldValue, string newValue, string? imageUrl, CancellationToken cancellationToken) =>
        MutateSectionAsync(name, section =>
        {
            BsonArray values = EnsureArray(section, "value");
            int index = values.IndexOf(new BsonString(oldValue));
            if (index < 0) throw new SectionValueNotFoundException();
            values[index] = newValue;
            if (!string.IsNullOrWhiteSpace(imageUrl)) section["imgUrl"] = imageUrl;
        }, cancellationToken);

    public Task<CatalogMutationResult<SectionDocumentRecord>> DeleteSectionValueAsync(
        string name, string value, CancellationToken cancellationToken) =>
        MutateSectionAsync(name, section => EnsureArray(section, "value").Remove(new BsonString(value)), cancellationToken);

    private async Task<CatalogMutationResult<SectionDocumentRecord>> MutateSectionAsync(
        string name, Action<BsonDocument> mutation, CancellationToken cancellationToken)
    {
        BsonDocument? document = await GetSectionAsync(cancellationToken);
        if (document is null) return NotFound<SectionDocumentRecord>("Không tìm thấy dữ liệu");
        BsonDocument? section = ReadItems(document).FirstOrDefault(item => string.Equals(ReadString(item, "name"), name, StringComparison.Ordinal));
        if (section is null) return NotFound<SectionDocumentRecord>("Không tìm thấy section");
        try { mutation(section); }
        catch (SectionValueNotFoundException) { return NotFound<SectionDocumentRecord>("Không tìm thấy value"); }
        return await ReplaceSectionAsync(document, cancellationToken);
    }

    private async Task<CatalogMutationResult<SectionDocumentRecord>> ReplaceSectionAsync(BsonDocument document, CancellationToken cancellationToken)
    {
        ReplaceOneResult result = await sections.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", document["_id"]), document,
            cancellationToken: cancellationToken);
        return result.MatchedCount == 0 ? Conflict<SectionDocumentRecord>("Section changed") : Success(MapSection(document));
    }

    private async Task<BsonDocument?> GetSectionAsync(CancellationToken cancellationToken) =>
        await sections.Find(FilterDefinition<BsonDocument>.Empty).Limit(1).FirstOrDefaultAsync(cancellationToken);

    private static BsonDocument NewChip() => new()
    {
        ["_id"] = ObjectId.GenerateNewId(), ["Color"] = new BsonArray(), ["Shapes"] = new BsonArray(),
        ["Frames"] = new BsonArray(), ["ButtonCount"] = new BsonArray(),
    };
    private static BsonDocument NewSections() => new() { ["_id"] = ObjectId.GenerateNewId(), ["Section"] = new BsonArray() };
    private static BsonDocument NewSectionItem(string name) => new() { ["_id"] = ObjectId.GenerateNewId(), ["name"] = name, ["value"] = new BsonArray() };
    private static ChipValuesRecord MapChip(BsonDocument document) => new(ReadStrings(document, "Color"), ReadStrings(document, "Shapes"), ReadStrings(document, "Frames"), ReadStrings(document, "ButtonCount"));
    private static SectionDocumentRecord MapSection(BsonDocument document) => new(ReadId(document), ReadItems(document).Select(item => new SectionItemRecord(ReadId(item), ReadString(item, "name"), ReadStrings(item, "value"), ReadString(item, "imgUrl"))).ToArray());
    private static BsonArray EnsureArray(BsonDocument document, string field) { if (!document.TryGetValue(field, out BsonValue value) || !value.IsBsonArray) document[field] = new BsonArray(); return document[field].AsBsonArray; }
    private static IEnumerable<BsonDocument> ReadItems(BsonDocument document) => EnsureArray(document, "Section").Where(item => item.IsBsonDocument).Select(item => item.AsBsonDocument);
    private static string[] ReadStrings(BsonDocument document, string field) => EnsureArray(document, field).Where(item => item.IsString).Select(item => item.AsString).ToArray();
    private static string ReadId(BsonDocument document) => document.TryGetValue("_id", out BsonValue value) && !value.IsBsonNull ? value.ToString() ?? string.Empty : string.Empty;
    private static string? ReadString(BsonDocument document, string field) => document.TryGetValue(field, out BsonValue value) && !value.IsBsonNull ? value.IsString ? value.AsString : value.ToString() : null;
    private static string NormalizeKey(string? value) => new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static FilterDefinition<BsonDocument> BuildIdFilter(string id) { var builder = Builders<BsonDocument>.Filter; return ObjectId.TryParse(id, out ObjectId value) ? builder.Or(builder.Eq("_id", value), builder.Eq("_id", id)) : builder.Eq("_id", id); }
    private static bool ReferencesSectionImage(string? mediaUrl, string filename)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl) || mediaUrl.Length > 2_048 || mediaUrl.Contains('\0')) return false;
        if (!Uri.TryCreate(mediaUrl, UriKind.RelativeOrAbsolute, out Uri? uri)) return false;
        string path = uri.IsAbsoluteUri ? uri.AbsolutePath : mediaUrl.Split('?', '#')[0];
        string normalized = path.Replace('\\', '/');
        return string.Equals(normalized, $"/section-images/{filename}", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, $"/api/section-images/{filename}", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, $"/section-images/{Uri.EscapeDataString(filename)}", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, $"/api/section-images/{Uri.EscapeDataString(filename)}", StringComparison.OrdinalIgnoreCase);
    }
    private static CatalogMutationResult<T> Success<T>(T value) => new(CatalogMutationStatus.Success, value);
    private static CatalogMutationResult<T> NotFound<T>(string message) => new(CatalogMutationStatus.NotFound, Message: message);
    private static CatalogMutationResult<T> Conflict<T>(string message) => new(CatalogMutationStatus.Conflict, Message: message);
    private sealed class SectionValueNotFoundException : Exception;
}
