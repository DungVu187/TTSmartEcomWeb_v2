using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Domain.Stations;

namespace TTSmartEcom.Infrastructure.SqlServer.Stations;

public sealed class SqlStationRepository(ISqlConnectionFactory factory) : IStationRepository
{
    public async Task<StationPage> ListAsync(int page, int limit, string? search, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        string predicate = string.IsNullOrWhiteSpace(search) ? "" : " WHERE Name LIKE @search OR Code LIKE @search";
        await using SqlCommand count = new($"SELECT COUNT(*) FROM dbo.Stations{predicate};", connection);
        AddSearch(count, search);
        long total = Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        await using SqlCommand command = new($"SELECT PublicId,Name,Code,DetailsJson FROM dbo.Stations{predicate} ORDER BY Code OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;", connection);
        AddSearch(command, search);
        command.Parameters.AddWithValue("@skip", checked((Math.Max(1, page) - 1) * Math.Max(1, limit)));
        command.Parameters.AddWithValue("@take", Math.Max(1, limit));
        return new StationPage(total, page, limit, await ReadManyAsync(connection, command, cancellationToken));
    }

    public async Task<IReadOnlyList<Station>> SearchExactAsync(string? name, string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(code)) return [];
        await using SqlConnection connection = factory.Create(); await connection.OpenAsync(cancellationToken);
        List<string> clauses = [];
        await using SqlCommand command = new() { Connection = connection };
        if (!string.IsNullOrWhiteSpace(name)) { clauses.Add("Name COLLATE Latin1_General_100_CI_AI=@name"); command.Parameters.AddWithValue("@name", name.Trim()); }
        if (!string.IsNullOrWhiteSpace(code)) { clauses.Add("Code COLLATE Latin1_General_100_CI_AI=@code"); command.Parameters.AddWithValue("@code", code.Trim()); }
        command.CommandText = $"SELECT PublicId,Name,Code,DetailsJson FROM dbo.Stations WHERE {string.Join(" AND ", clauses)};";
        return await ReadManyAsync(connection, command, cancellationToken);
    }

    public Task<Station?> FindByIdAsync(string id, CancellationToken cancellationToken) => FindOneAsync("PublicId=@id", id, cancellationToken);
    public Task<Station?> FindByCodeAsync(string code, bool publicProjection, CancellationToken cancellationToken) => FindOneAsync("Code=@id", code.Trim(), cancellationToken);

    public Task<IReadOnlyList<Station>> FindByCodesAsync(IReadOnlyList<string> codes, CancellationToken cancellationToken) => FindManyAsync("Code", codes, cancellationToken);
    public Task<IReadOnlyList<Station>> FindByIdsAsync(IReadOnlyList<string> ids, bool publicProjection, CancellationToken cancellationToken) => FindManyAsync("PublicId", ids, cancellationToken);

    public async Task<Station?> CreateAsync(NewStationData station, CancellationToken cancellationToken)
    {
        string id = SqlPublicIds.New();
        JsonObject details = new() { ["stationName"] = station.StationName, ["stationCode"] = station.StationCode, ["allowPublicSignup"] = station.AllowPublicSignup, ["location"] = station.Location, ["productId"] = new JsonArray() };
        await using SqlConnection connection = factory.Create(); await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = new("INSERT dbo.Stations(StationId,PublicId,Name,Code,DetailsJson,Version) VALUES(NEWID(),@id,@name,@code,@details,0);", connection);
        command.Parameters.AddWithValue("@id", id); command.Parameters.AddWithValue("@name", station.StationName); command.Parameters.AddWithValue("@code", station.StationCode); command.Parameters.AddWithValue("@details", details.ToJsonString());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1 ? await FindByIdAsync(id, cancellationToken) : null;
    }

    public async Task<Station?> UpdateAsync(string id, UpdateStationData station, CancellationToken cancellationToken)
    {
        StationRow? current = await FindRowAsync(id, cancellationToken); if (current is null) return null;
        JsonObject details = Parse(current.DetailsJson);
        string? name = station.StationName ?? current.Name; string? code = station.StationCode ?? current.Code;
        if (station.StationName is not null) details["stationName"] = station.StationName;
        if (station.StationCode is not null) details["stationCode"] = station.StationCode;
        if (station.Location is not null) details["location"] = station.Location;
        if (station.AllowPublicSignup.HasValue) details["allowPublicSignup"] = station.AllowPublicSignup.Value;
        await using SqlConnection connection = factory.Create(); await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = new("UPDATE dbo.Stations SET Name=@name,Code=@code,DetailsJson=@details,Version=Version+1 WHERE PublicId=@id;", connection);
        command.Parameters.AddWithValue("@name", (object?)name ?? DBNull.Value); command.Parameters.AddWithValue("@code", (object?)code ?? DBNull.Value); command.Parameters.AddWithValue("@details", details.ToJsonString()); command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1 ? await FindByIdAsync(id, cancellationToken) : null;
    }

    public async Task<Station?> UpdateProductsAsync(string id, IReadOnlyList<string> productIds, CancellationToken cancellationToken)
    {
        StationRow? current = await FindRowAsync(id, cancellationToken); if (current is null) return null;
        await using SqlConnection connection = factory.Create(); await connection.OpenAsync(cancellationToken); await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (SqlCommand clear = new("DELETE FROM dbo.StationProducts WHERE StationId=@station;", connection, transaction)) { clear.Parameters.AddWithValue("@station", current.Id); await clear.ExecuteNonQueryAsync(cancellationToken); }
            for (int index = 0; index < productIds.Count; index++)
            {
                await using SqlCommand insert = new("INSERT dbo.StationProducts(StationProductId,PublicId,StationId,ProductId,SourceProductId,SortOrder,DetailsJson,Version) VALUES(NEWID(),@id,@station,(SELECT ProductId FROM dbo.Products WHERE PublicId=@product),@product,@sort,N'{}',0);", connection, transaction);
                insert.Parameters.AddWithValue("@id", SqlPublicIds.New()); insert.Parameters.AddWithValue("@station", current.Id); insert.Parameters.AddWithValue("@product", productIds[index]); insert.Parameters.AddWithValue("@sort", index); await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            JsonObject details = Parse(current.DetailsJson);
            JsonArray productArray = new();
            foreach (string productId in productIds) productArray.Add(productId);
            details["productId"] = productArray;
            await using (SqlCommand update = new("UPDATE dbo.Stations SET DetailsJson=@details,Version=Version+1 WHERE StationId=@station;", connection, transaction)) { update.Parameters.AddWithValue("@details", details.ToJsonString()); update.Parameters.AddWithValue("@station", current.Id); await update.ExecuteNonQueryAsync(cancellationToken); }
            await transaction.CommitAsync(cancellationToken);
        }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }
        return await FindByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create(); await connection.OpenAsync(cancellationToken); await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try { await using (SqlCommand lines = new("DELETE sp FROM dbo.StationProducts sp JOIN dbo.Stations s ON s.StationId=sp.StationId WHERE s.PublicId=@id;", connection, transaction)) { lines.Parameters.AddWithValue("@id", id); await lines.ExecuteNonQueryAsync(cancellationToken); } await using SqlCommand station = new("DELETE FROM dbo.Stations WHERE PublicId=@id;", connection, transaction); station.Parameters.AddWithValue("@id", id); bool deleted = await station.ExecuteNonQueryAsync(cancellationToken) == 1; await transaction.CommitAsync(cancellationToken); return deleted; } catch { await transaction.RollbackAsync(cancellationToken); throw; }
    }

    public Task<Station?> UpdateImageAsync(string id, string imageUrl, CancellationToken cancellationToken) => UpdateJsonAsync(id, "imgUrl", imageUrl, cancellationToken);
    public Task<Station?> RemoveImageAsync(string id, CancellationToken cancellationToken) => UpdateJsonAsync(id, "imgUrl", string.Empty, cancellationToken);

    private async Task<Station?> UpdateJsonAsync(string id, string property, string value, CancellationToken cancellationToken)
    {
        StationRow? current = await FindRowAsync(id, cancellationToken); if (current is null) return null; JsonObject details = Parse(current.DetailsJson); details[property] = value;
        await using SqlConnection connection = factory.Create(); await connection.OpenAsync(cancellationToken); await using SqlCommand command = new("UPDATE dbo.Stations SET DetailsJson=@details,Version=Version+1 WHERE PublicId=@id;", connection); command.Parameters.AddWithValue("@details", details.ToJsonString()); command.Parameters.AddWithValue("@id", id); return await command.ExecuteNonQueryAsync(cancellationToken) == 1 ? await FindByIdAsync(id, cancellationToken) : null;
    }

    private async Task<Station?> FindOneAsync(string predicate, string value, CancellationToken ct) { await using SqlConnection c = factory.Create(); await c.OpenAsync(ct); await using SqlCommand q = new($"SELECT PublicId,Name,Code,DetailsJson FROM dbo.Stations WHERE {predicate};", c); q.Parameters.AddWithValue("@id", value); await using SqlDataReader r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? await MapAsync(c, r, ct) : null; }
    private async Task<IReadOnlyList<Station>> FindManyAsync(string column, IReadOnlyList<string> values, CancellationToken ct) { if (values.Count == 0) return []; await using SqlConnection c = factory.Create(); await c.OpenAsync(ct); await using SqlCommand q = new($"SELECT PublicId,Name,Code,DetailsJson FROM dbo.Stations WHERE {column} IN ({string.Join(',', values.Select((_, i) => "@p" + i))});", c); for (int i=0;i<values.Count;i++) q.Parameters.AddWithValue("@p"+i,values[i]); return await ReadManyAsync(c,q,ct); }
    private async Task<StationRow?> FindRowAsync(string publicId, CancellationToken ct) { await using SqlConnection c=factory.Create(); await c.OpenAsync(ct); await using SqlCommand q=new("SELECT StationId,PublicId,Name,Code,DetailsJson FROM dbo.Stations WHERE PublicId=@id;",c);q.Parameters.AddWithValue("@id",publicId);await using SqlDataReader r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?new(r.GetGuid(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),r.IsDBNull(4)?"{}":r.GetString(4)):null; }
    private static async Task<IReadOnlyList<Station>> ReadManyAsync(SqlConnection c, SqlCommand q, CancellationToken ct) { List<Station> result=[]; await using SqlDataReader r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))result.Add(await MapAsync(c,r,ct));return result; }
    private static async Task<Station> MapAsync(SqlConnection c, SqlDataReader r, CancellationToken ct) { string id=r.GetString(0); JsonObject details=Parse(r.IsDBNull(3)?"{}":r.GetString(3)); List<string> products=[]; await using SqlCommand q=new("SELECT SourceProductId FROM dbo.StationProducts WHERE StationId=(SELECT StationId FROM dbo.Stations WHERE PublicId=@id) ORDER BY SortOrder;",c);q.Parameters.AddWithValue("@id",id);await using SqlDataReader productsReader=await q.ExecuteReaderAsync(ct);while(await productsReader.ReadAsync(ct))if(!productsReader.IsDBNull(0))products.Add(productsReader.GetString(0));return new Station(id,r.IsDBNull(1)?StringValue(details,"stationName"):r.GetString(1),StringValue(details,"imgUrl"),r.IsDBNull(2)?StringValue(details,"stationCode"):r.GetString(2),BoolValue(details,"allowPublicSignup",true),StringValue(details,"location"),products); }
    private static JsonObject Parse(string json) => JsonNode.Parse(json) as JsonObject ?? new JsonObject();
    private static string? StringValue(JsonObject json, string name) => json[name]?.GetValue<string>();
    private static bool BoolValue(JsonObject json, string name, bool fallback) => json[name] is JsonValue value && value.TryGetValue<bool>(out bool result) ? result : fallback;
    private static void AddSearch(SqlCommand command, string? search) { if (!string.IsNullOrWhiteSpace(search)) command.Parameters.AddWithValue("@search", "%"+search.Trim()+"%"); }
    private sealed record StationRow(Guid Id,string PublicId,string? Name,string? Code,string DetailsJson);
}
