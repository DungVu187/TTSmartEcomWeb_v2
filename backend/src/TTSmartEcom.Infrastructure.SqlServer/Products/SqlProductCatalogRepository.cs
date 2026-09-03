using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Text.Json;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.Infrastructure.SqlServer.Products;

#pragma warning disable CA1725

public sealed class SqlProductCatalogRepository(ICompanyDbConnectionFactory companyFactory) : IProductCatalogRepository
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public async Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken ct)
    {
        await using var c=companyFactory.Create(); await c.OpenAsync(ct);
        var where="IsDeleted=0"; if(!query.IncludePrivate) where+=" AND Display=1";
        if(!string.IsNullOrWhiteSpace(query.Search)) where+=" AND (Name LIKE @s OR NameUnsigned LIKE @s OR Code LIKE @s)";
        await using var count=new SqlCommand($"SELECT COUNT(*) FROM dbo.Products WHERE {where};",c); if(!string.IsNullOrWhiteSpace(query.Search))count.Parameters.AddWithValue("@s","%"+query.Search.Trim()+"%"); var total=Convert.ToInt64(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var command=new SqlCommand($"SELECT PublicId,TypeName,Name,NameUnsigned,Display,Code,VatRaw,Adjusted,BrandName,CategoryName,CategoryValue,Description,DetailsJson,DocumentsJson,PurchaseCount,SourceCreatedAtUtc,SourceUpdatedAtUtc FROM dbo.Products WHERE {where} ORDER BY Name OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;",c); if(!string.IsNullOrWhiteSpace(query.Search))command.Parameters.AddWithValue("@s","%"+query.Search.Trim()+"%");command.Parameters.AddWithValue("@skip",Math.Max(0,(query.Page-1)*query.Limit));command.Parameters.AddWithValue("@take",query.Limit);
        var rows=new List<ProductRecord>(); await using var r=await command.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)) rows.Add(await MapAsync(c,r,query.IncludePrivate,ct));return new(total,query.Page,query.Limit,rows);
    }
    public async Task<ProductRecord?> FindByIdAsync(string id,bool includePrivate,CancellationToken ct){await using var c=companyFactory.Create();await c.OpenAsync(ct);await using var q=new SqlCommand("SELECT PublicId,TypeName,Name,NameUnsigned,Display,Code,VatRaw,Adjusted,BrandName,CategoryName,CategoryValue,Description,DetailsJson,DocumentsJson,PurchaseCount,SourceCreatedAtUtc,SourceUpdatedAtUtc FROM dbo.Products WHERE PublicId=@id AND IsDeleted=0 AND (@p=1 OR Display=1);",c);q.Parameters.AddWithValue("@id",id);q.Parameters.AddWithValue("@p",includePrivate);await using var r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?await MapAsync(c,r,includePrivate,ct):null;}
    public async Task<IReadOnlyList<ProductRecord>> FindByIdsAsync(IReadOnlyCollection<string> ids,bool includePrivate,CancellationToken ct){var result=new List<ProductRecord>();foreach(var id in ids){var p=await FindByIdAsync(id,includePrivate,ct);if(p is not null)result.Add(p);}return result;}
    public async Task<IReadOnlyList<ProductTypeRecord>> ListTypesAsync(CancellationToken ct){await using var c=companyFactory.Create();await c.OpenAsync(ct);await using var q=new SqlCommand("SELECT PublicId,Name,Icon FROM dbo.ProductTypes ORDER BY Name;",c);await using var r=await q.ExecuteReaderAsync(ct);var a=new List<ProductTypeRecord>();while(await r.ReadAsync(ct))a.Add(new(r.GetString(0),r.IsDBNull(1)?null:r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),null,null));return a;}
    private static async Task<ProductRecord> MapAsync(SqlConnection c,SqlDataReader r,bool includePrivate,CancellationToken ct){var id=r.GetString(0);using JsonDocument details=Parse(r,12);JsonElement root=details.RootElement;await using var q=new SqlCommand("SELECT PublicId,PriceRaw,ImportPriceRaw,QuantityForSale,QuantityInStorage,DetailsJson FROM dbo.ProductVariants WHERE ProductId=(SELECT ProductId FROM dbo.Products WHERE PublicId=@id) ORDER BY SortOrder;",c);q.Parameters.AddWithValue("@id",id);await using var vr=await q.ExecuteReaderAsync(ct);var vs=new List<ProductVariant>();while(await vr.ReadAsync(ct)){using JsonDocument variant=Parse(vr,5);JsonElement value=variant.RootElement;vs.Add(new(vr.GetString(0),vr.IsDBNull(1)?null:vr.GetString(1),includePrivate&&!vr.IsDBNull(2)?vr.GetString(2):null,Number(value,"earn"),Text(value,"imgUrl"),Text(value,"color"),Text(value,"shape"),Text(value,"buttonCount"),Text(value,"frame"),vr.IsDBNull(3)?null:(double)vr.GetDecimal(3),vr.IsDBNull(4)?null:(double)vr.GetDecimal(4),Text(value,"note")));}var documents=Read<ProductLink[]>(r,13)??[];var reviews=Read<ProductReview[]>(root,"reviews")??[];var info=Read<ProductInfo>(root,"infoDoc");return new(id,S(r,1),S(r,2),S(r,3),B(r,4),S(r,5),S(r,6),B(r,7),S(r,8),S(r,9),S(r,10),vs,info,documents,r.IsDBNull(14)?0:r.GetInt64(14),reviews,Number(root,"totalRating")??0,Long(root,"reviewCount"),Number(root,"averageReviews")??0,Text(root,"warranty"),Text(root,"solution"),S(r,11),Text(root,"features"),Text(root,"operatingMethod"),Text(root,"advantages"),Text(root,"specifications"),D(r,15),D(r,16),false);}
    private static JsonDocument Parse(SqlDataReader reader,int index)=>JsonDocument.Parse(reader.IsDBNull(index)?"{}":reader.GetString(index));
    private static T? Read<T>(SqlDataReader reader,int index){try{return reader.IsDBNull(index)?default:JsonSerializer.Deserialize<T>(reader.GetString(index),Json);}catch(JsonException){return default;}}
    private static T? Read<T>(JsonElement root,string name){try{return root.TryGetProperty(name,out JsonElement value)?value.Deserialize<T>(Json):default;}catch(JsonException){return default;}}
    private static string? Text(JsonElement root,string name)=>root.TryGetProperty(name,out JsonElement value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
    private static double? Number(JsonElement root,string name)=>root.TryGetProperty(name,out JsonElement value)&&value.TryGetDouble(out double number)?number:null;
    private static long Long(JsonElement root,string name)=>root.TryGetProperty(name,out JsonElement value)&&value.TryGetInt64(out long number)?number:0;
    private static string? S(SqlDataReader r,int index)=>r.IsDBNull(index)?null:r.GetString(index); private static bool? B(SqlDataReader r,int index)=>r.IsDBNull(index)?null:r.GetBoolean(index); private static DateTimeOffset? D(SqlDataReader r,int index)=>r.IsDBNull(index)?null:new DateTimeOffset(r.GetDateTime(index),TimeSpan.Zero);
}
