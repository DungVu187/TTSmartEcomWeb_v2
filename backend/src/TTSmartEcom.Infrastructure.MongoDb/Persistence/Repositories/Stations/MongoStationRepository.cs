using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Domain.Stations;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Stations;

public sealed class MongoStationRepository(IMongoDatabaseProvider databaseProvider) : IStationRepository
{
    private readonly IMongoCollection<BsonDocument> stations = databaseProvider.Database.GetCollection<BsonDocument>(StationDocument.CollectionName);

    public async Task<StationPage> ListAsync(int page, int limit, string? search, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(search))
        {
            string escaped = System.Text.RegularExpressions.Regex.Escape(search.Trim()[..Math.Min(search.Trim().Length, 100)]);
            filter = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Regex("stationName", new BsonRegularExpression(escaped, "i")),
                Builders<BsonDocument>.Filter.Regex("stationCode", new BsonRegularExpression(escaped, "i")));
        }
        long total = await stations.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        List<BsonDocument> values = await stations.Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("stationCode")).Skip((page - 1) * limit).Limit(limit).ToListAsync(cancellationToken);
        return new StationPage(total, page, limit, values.Select(Map).ToArray());
    }

    public async Task<IReadOnlyList<Station>> SearchExactAsync(
        string? name,
        string? code,
        CancellationToken cancellationToken)
    {
        FilterDefinitionBuilder<BsonDocument> builder = Builders<BsonDocument>.Filter;
        List<FilterDefinition<BsonDocument>> clauses = [];
        AddExactSearch(clauses, builder, "stationName", name);
        AddExactSearch(clauses, builder, "stationCode", code);
        if (clauses.Count == 0) return [];
        FilterDefinition<BsonDocument> filter = clauses.Count == 1 ? clauses[0] : builder.And(clauses);
        List<BsonDocument> values = await stations.Find(filter).ToListAsync(cancellationToken);
        return values.Select(Map).ToArray();
    }

    public async Task<Station?> FindByIdAsync(string id, CancellationToken cancellationToken) => MapOrNull(await FindAsync(BuildIdFilter(id), cancellationToken));

    public async Task<Station?> FindByCodeAsync(string code, bool publicProjection, CancellationToken cancellationToken) =>
        MapOrNull(await FindAsync(Builders<BsonDocument>.Filter.Eq("stationCode", code.Trim()), cancellationToken));

    public async Task<IReadOnlyList<Station>> FindByCodesAsync(IReadOnlyList<string> codes, CancellationToken cancellationToken)
    {
        List<BsonDocument> values = await stations.Find(Builders<BsonDocument>.Filter.In("stationCode", codes)).ToListAsync(cancellationToken);
        return values.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<Station>> FindByIdsAsync(IReadOnlyList<string> ids, bool publicProjection, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Or(ids.Select(BuildIdFilter));
        List<BsonDocument> values = await stations.Find(filter).ToListAsync(cancellationToken);
        return values.Select(Map).ToArray();
    }

    public async Task<Station?> CreateAsync(NewStationData station, CancellationToken cancellationToken)
    {
        BsonDocument document = new()
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["stationName"] = station.StationName,
            ["stationCode"] = station.StationCode,
            ["allowPublicSignup"] = station.AllowPublicSignup,
            ["location"] = station.Location is null ? BsonNull.Value : station.Location,
            ["productId"] = new BsonArray(),
        };
        await stations.InsertOneAsync(document, cancellationToken: cancellationToken);
        return Map(document);
    }

    public async Task<Station?> UpdateAsync(string id, UpdateStationData station, CancellationToken cancellationToken)
    {
        BsonDocument? document = await FindAsync(BuildIdFilter(id), cancellationToken);
        if (document is null) return null;
        if (station.StationName is not null) document["stationName"] = station.StationName;
        if (station.StationCode is not null) document["stationCode"] = station.StationCode;
        if (station.Location is not null) document["location"] = station.Location;
        if (station.AllowPublicSignup.HasValue) document["allowPublicSignup"] = station.AllowPublicSignup.Value;
        await ReplaceAsync(document, cancellationToken);
        return Map(document);
    }

    public async Task<Station?> UpdateProductsAsync(string id, IReadOnlyList<string> productIds, CancellationToken cancellationToken)
    {
        BsonDocument? document = await FindAsync(BuildIdFilter(id), cancellationToken);
        if (document is null) return null;
        document["productId"] = new BsonArray(productIds.Select(static item => (BsonValue)item));
        await ReplaceAsync(document, cancellationToken);
        return Map(document);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken) => (await stations.DeleteOneAsync(BuildIdFilter(id), cancellationToken)).DeletedCount > 0;

    public async Task<Station?> UpdateImageAsync(string id, string imageUrl, CancellationToken cancellationToken)
    {
        BsonDocument? document = await FindAsync(BuildIdFilter(id), cancellationToken);
        if (document is null) return null;
        document["imgUrl"] = imageUrl;
        await ReplaceAsync(document, cancellationToken);
        return Map(document);
    }

    public async Task<Station?> RemoveImageAsync(string id, CancellationToken cancellationToken)
    {
        BsonDocument? document = await FindAsync(BuildIdFilter(id), cancellationToken);
        if (document is null) return null;
        document["imgUrl"] = string.Empty;
        await ReplaceAsync(document, cancellationToken);
        return Map(document);
    }

    private async Task<BsonDocument?> FindAsync(FilterDefinition<BsonDocument> filter, CancellationToken cancellationToken) => await stations.Find(filter).Limit(1).FirstOrDefaultAsync(cancellationToken);
    private async Task<ReplaceOneResult> ReplaceAsync(BsonDocument document, CancellationToken cancellationToken) => await stations.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", document["_id"]), document, cancellationToken: cancellationToken);
    private static FilterDefinition<BsonDocument> BuildIdFilter(string id)
    {
        FilterDefinitionBuilder<BsonDocument> builder = Builders<BsonDocument>.Filter;
        return ObjectId.TryParse(id, out ObjectId objectId) ? builder.Or(builder.Eq("_id", objectId), builder.Eq("_id", id)) : builder.Eq("_id", id);
    }

    private static void AddExactSearch(
        List<FilterDefinition<BsonDocument>> clauses,
        FilterDefinitionBuilder<BsonDocument> builder,
        string field,
        string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        string bounded = value[..Math.Min(value.Length, 100)];
        string escaped = System.Text.RegularExpressions.Regex.Escape(bounded);
        clauses.Add(builder.Regex(field, new BsonRegularExpression($"^{escaped}$", "i")));
    }

    private static Station? MapOrNull(BsonDocument? d) => d is null ? null : Map(d);
    private static Station Map(BsonDocument d) => new(ReadId(d), ReadString(d, "stationName"), ReadString(d, "imgUrl"), ReadString(d, "stationCode"), ReadBool(d, "allowPublicSignup", true), ReadString(d, "location"), ReadArray(d, "productId"));
    private static string ReadId(BsonDocument d) => d.TryGetValue("_id", out BsonValue value) && !value.IsBsonNull ? value.ToString() ?? string.Empty : string.Empty;
    private static string? ReadString(BsonDocument d, string name) => d.TryGetValue(name, out BsonValue value) && !value.IsBsonNull ? value.IsString ? value.AsString : value.ToString() : null;
    private static bool ReadBool(BsonDocument d, string name, bool fallback) => d.TryGetValue(name, out BsonValue value) && value.IsBoolean ? value.AsBoolean : fallback;
    private static string[] ReadArray(BsonDocument d, string name) =>
        d.TryGetValue(name, out BsonValue value) && value.IsBsonArray
            ? value.AsBsonArray
                .Where(static item => !item.IsBsonNull && item.BsonType is not BsonType.Document and not BsonType.Array)
                .Select(static item => item.IsString ? item.AsString : item.ToString() ?? string.Empty)
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .ToArray()
            : [];
}
