using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Storefront;
using TTSmartEcom.Domain.Storefront;

namespace TTSmartEcom.Infrastructure.SqlServer.Storefront;

/// <summary>SQL-backed singleton storefront configuration while retaining legacy nested JSON fields.</summary>
public sealed class SqlStorefrontRepository(ISqlConnectionFactory factory) : IStorefrontRepository
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    public async Task<StorefrontContent?> GetAsync(CancellationToken cancellationToken)
    {
        Row? row = await GetRowAsync(cancellationToken);
        return row is null ? null : Map(row.PublicId, Parse(row.Json));
    }

    public Task<StorefrontContent> UpsertAsync(StorefrontPatch patch, CancellationToken cancellationToken) => UpdateAsync(root =>
    {
        if (patch.Introduction is not null) root["introduction"] = patch.Introduction;
        if (patch.IntroductionTranslations is not null) root["introductionTranslations"] = ToNode(patch.IntroductionTranslations);
        if (patch.MainPolicy is not null) root["mainPolicy"] = patch.MainPolicy;
        if (patch.FooterContent is not null) root["footerContent"] = ToNode(patch.FooterContent);
        if (patch.DisplayPartners.HasValue) root["displayPartners"] = patch.DisplayPartners.Value;
        if (patch.NewProductUrl is not null) root["newProductUrl"] = patch.NewProductUrl;
        if (patch.TopPurchaseUrl is not null) root["topPurchaseUrl"] = patch.TopPurchaseUrl;
        if (patch.HighestRatingUrl is not null) root["highestRatingUrl"] = patch.HighestRatingUrl;
        if (patch.OverviewImages is not null) root["overViewImg"] = ToNode(patch.OverviewImages);
        if (patch.Partners is not null) root["partners"] = ToNode(patch.Partners);
    }, cancellationToken);

    public Task<StorefrontContent> UpdateSectionAsync(string section, StorefrontSectionPatch patch, CancellationToken cancellationToken)
    {
        string field = section.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(field, "^section(?:[1-9]|1[01])$")) throw new ArgumentException("Invalid storefront section", nameof(section));
        return UpdateAsync(root =>
        {
            JsonObject value = Object(root, field);
            if (patch.Name is not null) value["name"] = patch.Name;
            if (patch.NameTranslations is not null) value["nameTranslations"] = ToNode(patch.NameTranslations);
            if (patch.ProductIds is not null) value["productId"] = ToNode(patch.ProductIds);
            if (patch.Display.HasValue) value["display"] = patch.Display.Value;
            if (patch.Image is not null) value["image"] = patch.Image;
            if (patch.Link is not null) value["link"] = patch.Link;
            root[field] = value;
        }, cancellationToken);
    }

    public Task<StorefrontContent> UpdateHomeCategoriesAsync(HomeCategoryConfigPatch patch, CancellationToken cancellationToken) => UpdateAsync(root =>
    {
        JsonObject home = Object(root, "homeCategoryConfig");
        if (patch.Configured.HasValue) home["configured"] = patch.Configured.Value;
        if (patch.SidebarTitle is not null) home["sidebarTitle"] = patch.SidebarTitle;
        if (patch.SidebarTitleTranslations is not null) home["sidebarTitleTranslations"] = ToNode(patch.SidebarTitleTranslations);
        if (patch.ShowSidebar.HasValue) home["showSidebar"] = patch.ShowSidebar.Value;
        if (patch.ShowQuickCategories.HasValue) home["showQuickCategories"] = patch.ShowQuickCategories.Value;
        if (patch.Items is not null) home["items"] = ToNode(patch.Items);
        root["homeCategoryConfig"] = home;
    }, cancellationToken);

    public Task<StorefrontContent> UpdatePoliciesAsync(IReadOnlyList<StorefrontPolicy> policies, CancellationToken cancellationToken) => UpdateAsync(root =>
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        StorefrontPolicy[] timestamped = policies.Select(x => x.UpdatedAt.HasValue ? x : x with { UpdatedAt = now }).ToArray();
        root["policies"] = ToNode(timestamped);
    }, cancellationToken);

    public async Task<bool> RemoveImageAsync(string imageUrl, CancellationToken cancellationToken)
    {
        Row? row = await GetRowAsync(cancellationToken); if (row is null) return false;
        JsonObject root = Parse(row.Json); bool found = false;
        foreach (string property in new[] { "overViewImg", "partners" })
        {
            if (root[property] is JsonArray items && items.Any(x => string.Equals(x?.GetValue<string>(), imageUrl, StringComparison.Ordinal)))
            { root[property] = new JsonArray(items.Where(x => !string.Equals(x?.GetValue<string>(), imageUrl, StringComparison.Ordinal)).Select(x => x?.DeepClone()).ToArray()); found = true; }
        }
        foreach (string property in new[] { "newProductUrl", "topPurchaseUrl", "highestRatingUrl" }) if (String(root, property) == imageUrl) { root[property] = string.Empty; found = true; }
        if (!found) return false;
        await SaveAsync(row, root, cancellationToken); return true;
    }

    public async Task<bool> ContainsImageAsync(string imageUrl, CancellationToken cancellationToken)
    {
        Row? row = await GetRowAsync(cancellationToken); if (row is null) return false; JsonObject root = Parse(row.Json);
        foreach (string property in new[] { "overViewImg", "partners" }) if (Strings(root, property).Contains(imageUrl, StringComparer.Ordinal)) return true;
        foreach (string property in new[] { "newProductUrl", "topPurchaseUrl", "highestRatingUrl" }) if (String(root, property) == imageUrl) return true;
        return Enumerable.Range(1, 11).Any(index => String(Object(root, $"section{index}"), "image") == imageUrl);
    }

    private async Task<StorefrontContent> UpdateAsync(Action<JsonObject> mutate, CancellationToken ct)
    {
        Row row = await GetRowAsync(ct) ?? new Row(Guid.NewGuid(), SqlPublicIds.New(), "{}", 0);
        JsonObject root = Parse(row.Json); mutate(root); root["updatedAt"] = DateTimeOffset.UtcNow.UtcDateTime;
        await SaveAsync(row, root, ct); return Map(row.PublicId, root);
    }

    private async Task SaveAsync(Row row, JsonObject root, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create(); await connection.OpenAsync(ct);
        if (row.Version < 0) throw new InvalidOperationException("Invalid storefront version.");
        if (await ExistsAsync(connection, row.PublicId, ct))
        {
            await using SqlCommand update = new("UPDATE dbo.StorefrontSettings SET ConfigurationJson=@json,Version=Version+1,SourceUpdatedAtUtc=SYSUTCDATETIME() WHERE PublicId=@id;", connection);
            update.Parameters.AddWithValue("@json", root.ToJsonString()); update.Parameters.AddWithValue("@id", row.PublicId); await update.ExecuteNonQueryAsync(ct); return;
        }
        await using SqlCommand insert = new("INSERT dbo.StorefrontSettings(StorefrontSettingsId,PublicId,ConfigurationJson,Version,SourceUpdatedAtUtc) VALUES(@key,@id,@json,0,SYSUTCDATETIME());", connection);
        insert.Parameters.AddWithValue("@key", row.Id); insert.Parameters.AddWithValue("@id", row.PublicId); insert.Parameters.AddWithValue("@json", root.ToJsonString()); await insert.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> ExistsAsync(SqlConnection connection, string id, CancellationToken ct) { await using SqlCommand command=new("SELECT COUNT(*) FROM dbo.StorefrontSettings WHERE PublicId=@id;",connection);command.Parameters.AddWithValue("@id",id);return Convert.ToInt64(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture)>0; }
    private async Task<Row?> GetRowAsync(CancellationToken ct) { await using SqlConnection c=factory.Create();await c.OpenAsync(ct);await using SqlCommand q=new("SELECT TOP(1) StorefrontSettingsId,PublicId,ConfigurationJson,Version FROM dbo.StorefrontSettings ORDER BY PublicId;",c);await using SqlDataReader r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?new Row(r.GetGuid(0),r.GetString(1),r.GetString(2),checked((int)r.GetInt64(3))):null; }

    private static StorefrontContent Map(string id, JsonObject root)
    {
        Dictionary<string, StorefrontSection?> sections = Enumerable.Range(1,11).ToDictionary(x=>$"section{x}",x => root[$"section{x}"] is JsonObject node ? MapSection(node) : null, StringComparer.Ordinal);
        return new StorefrontContent(id, Strings(root,"overViewImg"),Strings(root,"partners"),Bool(root,"displayPartners",true),MapFooter(Object(root,"footerContent")),String(root,"newProductUrl"),String(root,"topPurchaseUrl"),String(root,"highestRatingUrl"),String(root,"introduction"),MapLocalized(Object(root,"introductionTranslations")),String(root,"mainPolicy"),MapPolicies(root),MapHome(Object(root,"homeCategoryConfig")),sections,Date(root,"createdAt"),Date(root,"updatedAt"));
    }
    private static StorefrontFooter MapFooter(JsonObject x)=>new(String(x,"logo"),String(x,"description"),String(x,"address"),String(x,"phone"),String(x,"email"));
    private static StorefrontSection MapSection(JsonObject x)=>new(String(x,"name"),MapLocalized(Object(x,"nameTranslations")),Strings(x,"productId"),Bool(x,"display",true),String(x,"image"),String(x,"link"));
    private static HomeCategoryConfig MapHome(JsonObject x)=>new(Bool(x,"configured",false),String(x,"sidebarTitle"),MapLocalized(Object(x,"sidebarTitleTranslations")),Bool(x,"showSidebar",true),Bool(x,"showQuickCategories",true),Read<HomeCategoryItem[]>(x,"items")??[]);
    private static LocalizedText MapLocalized(JsonObject x)=>new(String(x,"vi"),String(x,"zh"),String(x,"en"));
    private static StorefrontPolicy[] MapPolicies(JsonObject root) => root["policies"] is JsonArray values
        ? values.OfType<JsonObject>().Select(MapPolicy).ToArray() : [];
    private static StorefrontPolicy MapPolicy(JsonObject x) => new(String(x,"key"), String(x,"title"), String(x,"summary"), MapPolicySections(x),
        new StorefrontPolicyTranslations(MapPolicyContent(Object(x,"translations"),"vi"),MapPolicyContent(Object(x,"translations"),"zh"),MapPolicyContent(Object(x,"translations"),"en")), Date(x,"updatedAt"));
    private static StorefrontPolicyContent? MapPolicyContent(JsonObject parent,string locale) => parent[locale] is JsonObject x
        ? new StorefrontPolicyContent(String(x,"title"),String(x,"summary"),MapPolicySections(x)) : null;
    private static StorefrontPolicySection[] MapPolicySections(JsonObject x) => x["sections"] is JsonArray values
        ? values.OfType<JsonObject>().Select(y=>new StorefrontPolicySection(String(y,"title"),String(y,"content"))).ToArray() : [];
    private static JsonObject Parse(string json)=>JsonNode.Parse(json) as JsonObject??new JsonObject();
    private static JsonObject Object(JsonObject root,string key)=>root[key] as JsonObject??new JsonObject();
    private static string? String(JsonObject root,string key)=>root[key] is JsonValue value&&value.TryGetValue<string>(out string? text)?text:null;
    private static bool Bool(JsonObject root,string key,bool fallback)=>root[key] is JsonValue value&&value.TryGetValue<bool>(out bool result)?result:fallback;
    private static string[] Strings(JsonObject root,string key)=>root[key] is JsonArray values?values.Select(x=>x is JsonValue value&&value.TryGetValue<string>(out string? text)?text:null).Where(x=>x is not null).Cast<string>().ToArray():[];
    private static DateTimeOffset? Date(JsonObject root,string key)
    {
        if (root[key] is JsonValue value && value.TryGetValue<DateTimeOffset>(out DateTimeOffset parsed)) return parsed;
        if (root[key] is JsonObject extended && extended["$date"] is JsonValue date)
        {
            if (date.TryGetValue<long>(out long milliseconds)) return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            if (date.TryGetValue<string>(out string? text) && DateTimeOffset.TryParse(text, out DateTimeOffset parsedDate)) return parsedDate;
        }
        return null;
    }
    private static T? Read<T>(JsonObject root,string key)=>root[key] is null?default:root[key]!.Deserialize<T>(Json);
    private static JsonNode? ToNode<T>(T value)=>JsonSerializer.SerializeToNode(value,Json);
    private sealed record Row(Guid Id,string PublicId,string Json,int Version);
}
