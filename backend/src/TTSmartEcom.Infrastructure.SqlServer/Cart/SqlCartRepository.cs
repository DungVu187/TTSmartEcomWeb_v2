using System.Text.Json;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Cart;
using TTSmartEcom.Domain.Cart;

namespace TTSmartEcom.Infrastructure.SqlServer.Cart;

public sealed class SqlCartRepository(IOperationalDbConnectionFactory factory, ICompanyDbConnectionFactory companyFactory) : ICartRepository, ICartProductCatalog
{
    public async Task<CartOwner?> FindOwnerAsync(string userId, CancellationToken cancellationToken)
    {
        await using SqlConnection c=factory.Create();await c.OpenAsync(cancellationToken);await using SqlCommand q=new("SELECT UserId,PublicId,Phone,Name,Role,StationIdsJson,Version FROM dbo.Users WHERE PublicId=@id AND IsDeleted=0;",c);q.Parameters.AddWithValue("@id",userId);await using SqlDataReader r=await q.ExecuteReaderAsync(cancellationToken);if(!await r.ReadAsync(cancellationToken))return null;Guid key=r.GetGuid(0);string phone=r.IsDBNull(2)?string.Empty:r.GetString(2);string? name=r.IsDBNull(3)?null:r.GetString(3);string role=r.IsDBNull(4)?"customer":r.GetString(4);string[] stations=Strings(r.IsDBNull(5)?null:r.GetString(5));int version=checked((int)r.GetInt64(6));await r.CloseAsync();return new CartOwner(userId,phone,name,role,stations,await ItemsAsync(c,key,cancellationToken),version);
    }

    public async Task<IReadOnlyList<CartItem>> ReplaceAsync(string userId,IReadOnlyList<CartItem> items,int? expectedVersion,CancellationToken cancellationToken)
    {
        await using SqlConnection c=factory.Create();await c.OpenAsync(cancellationToken);await using SqlTransaction tx=(SqlTransaction)await c.BeginTransactionAsync(cancellationToken);
        try
        {
            IReadOnlyList<CartItem> persisted=items.Select(x=>x with{Id=string.IsNullOrWhiteSpace(x.Id)?SqlPublicIds.New():x.Id}).ToArray();
            Guid owner=await ClaimOwnerAsync(c,tx,userId,expectedVersion,cancellationToken);
            await ReplaceItemsAsync(c,tx,owner,persisted,cancellationToken);await tx.CommitAsync(cancellationToken);
            return persisted;
        }
        catch { await tx.RollbackAsync(cancellationToken);throw; }
    }

    public async Task UpdateAfterCustomerOrderAsync(string userId,IReadOnlyList<CartItem> items,string? stationId,int expectedVersion,CancellationToken cancellationToken)
    {
        await using SqlConnection c=factory.Create();await c.OpenAsync(cancellationToken);await using SqlTransaction tx=(SqlTransaction)await c.BeginTransactionAsync(cancellationToken);
        try
        {
            Guid owner=await ClaimOwnerAsync(c,tx,userId,expectedVersion,cancellationToken);
            if(!string.IsNullOrWhiteSpace(stationId)){await using SqlCommand station=new("UPDATE dbo.Users SET StationIdsJson=(SELECT CASE WHEN StationIdsJson IS NULL OR StationIdsJson NOT LIKE '%'+@station+'%' THEN JSON_MODIFY(COALESCE(StationIdsJson,N'[]'),'append $',@station) ELSE StationIdsJson END) WHERE UserId=@id;",c,tx);station.Parameters.AddWithValue("@station",stationId);station.Parameters.AddWithValue("@id",owner);await station.ExecuteNonQueryAsync(cancellationToken);}
            await ReplaceItemsAsync(c,tx,owner,items,cancellationToken);await tx.CommitAsync(cancellationToken);
        }
        catch { await tx.RollbackAsync(cancellationToken);throw; }
    }

    public async Task<ProductVariantSnapshot?> FindVariantAsync(string productId,int variantIndex,CartOwner viewer,CancellationToken cancellationToken)
    {
        if(variantIndex<0)return null;await using SqlConnection c=companyFactory.Create();await c.OpenAsync(cancellationToken);await using SqlCommand q=new("SELECT p.Name,p.BrandName,p.Code,p.Display,v.PriceRaw,v.QuantityForSale,v.QuantityInStorage,v.DetailsJson FROM dbo.Products p JOIN dbo.ProductVariants v ON v.ProductId=p.ProductId WHERE p.PublicId=@id AND p.IsDeleted=0 AND v.SortOrder=@index;",c);q.Parameters.AddWithValue("@id",productId);q.Parameters.AddWithValue("@index",variantIndex);await using SqlDataReader r=await q.ExecuteReaderAsync(cancellationToken);if(!await r.ReadAsync(cancellationToken)||viewer.Role=="customer"&&(!r.IsDBNull(3)&&!r.GetBoolean(3)))return null;using JsonDocument details=JsonDocument.Parse(r.IsDBNull(7)?"{}":r.GetString(7));JsonElement root=details.RootElement;return new ProductVariantSnapshot(productId,variantIndex,r.IsDBNull(0)?null:r.GetString(0),r.IsDBNull(1)?null:r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(4)?null:r.GetString(4),GetString(root,"imgUrl"),r.IsDBNull(5)?0:(double)r.GetDecimal(5),r.IsDBNull(6)?0:(double)r.GetDecimal(6),GetDouble(root,"earn",25),!r.IsDBNull(3)&&r.GetBoolean(3));
    }

    public async Task<IReadOnlySet<string>?> GetVisibleProductIdsAsync(CartOwner viewer,CancellationToken cancellationToken)
    {
        if(viewer.Role is "superadmin" or "admin" or "staff"||viewer.StationIds.Count==0)return null;await using SqlConnection c=factory.Create();await c.OpenAsync(cancellationToken);await using SqlCommand q=new($"SELECT DISTINCT SourceProductId FROM dbo.StationProducts sp JOIN dbo.Stations s ON s.StationId=sp.StationId WHERE s.PublicId IN ({string.Join(',',viewer.StationIds.Select((_,i)=>"@p"+i))}) AND SourceProductId IS NOT NULL;",c);for(int i=0;i<viewer.StationIds.Count;i++)q.Parameters.AddWithValue("@p"+i,viewer.StationIds[i]);HashSet<string> values=new(StringComparer.Ordinal);await using SqlDataReader r=await q.ExecuteReaderAsync(cancellationToken);while(await r.ReadAsync(cancellationToken))values.Add(r.GetString(0));return values;
    }

    private static async Task<Guid> ClaimOwnerAsync(SqlConnection c,SqlTransaction tx,string publicId,int? expected,CancellationToken ct)
    {
        await using SqlCommand q=new(expected.HasValue?"UPDATE dbo.Users SET Version=Version+1 OUTPUT inserted.UserId WHERE PublicId=@id AND IsDeleted=0 AND Version=@version;":"UPDATE dbo.Users SET Version=Version+1 OUTPUT inserted.UserId WHERE PublicId=@id AND IsDeleted=0;",c,tx);q.Parameters.AddWithValue("@id",publicId);if(expected.HasValue)q.Parameters.AddWithValue("@version",expected.Value);object? result=await q.ExecuteScalarAsync(ct);return result is Guid id?id:throw new InvalidOperationException(expected.HasValue?"Cart was changed by another request":"User not found");
    }
    private static async Task ReplaceItemsAsync(SqlConnection c,SqlTransaction tx,Guid owner,IReadOnlyList<CartItem> items,CancellationToken ct)
    {
        await using(SqlCommand clear=new("DELETE FROM dbo.CartItems WHERE UserId=@user;",c,tx)){clear.Parameters.AddWithValue("@user",owner);await clear.ExecuteNonQueryAsync(ct);}for(int index=0;index<items.Count;index++){CartItem item=items[index];string publicId=string.IsNullOrWhiteSpace(item.Id)?SqlPublicIds.New():item.Id!;await using SqlCommand add=new("INSERT dbo.CartItems(CartItemId,PublicId,UserId,ProductId,ProductVariantId,SourceProductId,VariantIndex,Quantity,Status,SortOrder,Version) VALUES(NEWID(),@id,@user,(SELECT ProductId FROM dbo.Products WHERE PublicId=@product),(SELECT TOP(1) ProductVariantId FROM dbo.ProductVariants WHERE ProductId=(SELECT ProductId FROM dbo.Products WHERE PublicId=@product) AND SortOrder=@variant),@product,@variant,@quantity,@status,@sort,0);",c,tx);add.Parameters.AddWithValue("@id",publicId);add.Parameters.AddWithValue("@user",owner);add.Parameters.AddWithValue("@product",item.ProductId);add.Parameters.AddWithValue("@variant",item.VariantIndex);add.Parameters.AddWithValue("@quantity",item.Quantity);add.Parameters.AddWithValue("@status",item.Status);add.Parameters.AddWithValue("@sort",index);await add.ExecuteNonQueryAsync(ct);}
    }
    private static async Task<IReadOnlyList<CartItem>> ItemsAsync(SqlConnection c,Guid user,CancellationToken ct){await using SqlCommand q=new("SELECT PublicId,SourceProductId,VariantIndex,Quantity,Status FROM dbo.CartItems WHERE UserId=@user ORDER BY SortOrder;",c);q.Parameters.AddWithValue("@user",user);List<CartItem> items=[];await using SqlDataReader r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))items.Add(new CartItem(r.IsDBNull(1)?string.Empty:r.GetString(1),r.IsDBNull(2)?0:r.GetInt32(2),r.IsDBNull(3)?1:Math.Max(1,(int)r.GetDecimal(3)),r.IsDBNull(4)||r.GetBoolean(4),Id:r.GetString(0)));return items;}
    private static string[] Strings(string? json){if(string.IsNullOrWhiteSpace(json))return[];try{return JsonSerializer.Deserialize<string[]>(json)??[];}catch(JsonException){return[];}}
    private static string? GetString(JsonElement root,string field)=>root.TryGetProperty(field,out JsonElement value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
    private static double GetDouble(JsonElement root,string field,double fallback)=>root.TryGetProperty(field,out JsonElement value)&&value.TryGetDouble(out double result)?result:fallback;
}
