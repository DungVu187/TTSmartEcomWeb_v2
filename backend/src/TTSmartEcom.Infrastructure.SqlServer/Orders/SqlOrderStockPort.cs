using System.Text.Json;
using System.Data;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Application.Orders;

namespace TTSmartEcom.Infrastructure.SqlServer.Orders;

#pragma warning disable CA1725

public sealed class SqlOrderStockPort(ISqlConnectionFactory factory) : IOrderStockPort
{
    public async Task<ProductOrderSnapshot?> GetProductAsync(string productId,int variantIndex,CancellationToken ct)
    {
        if(variantIndex<0)return null;await using SqlConnection c=factory.Create();await c.OpenAsync(ct);await using SqlCommand q=new("SELECT p.Name,p.BrandName,p.Code,p.Display,v.PublicId,v.PriceRaw,v.ImportPriceRaw,v.QuantityForSale,v.QuantityInStorage,v.DetailsJson FROM dbo.Products p JOIN dbo.ProductVariants v ON v.ProductId=p.ProductId WHERE p.PublicId=@id AND p.IsDeleted=0 AND v.SortOrder=@variant;",c);q.Parameters.AddWithValue("@id",productId);q.Parameters.AddWithValue("@variant",variantIndex);await using SqlDataReader r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;using JsonDocument details=JsonDocument.Parse(r.IsDBNull(9)?"{}":r.GetString(9));JsonElement root=details.RootElement;return new ProductOrderSnapshot(productId,variantIndex,r.GetString(4),r.IsDBNull(0)?null:r.GetString(0),r.IsDBNull(1)?null:r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(5)?null:r.GetString(5),Text(root,"imgUrl"),Text(root,"color"),Text(root,"shape"),r.IsDBNull(7)?0:(double)r.GetDecimal(7),r.IsDBNull(8)?0:(double)r.GetDecimal(8),Number(root,"earn",25),!r.IsDBNull(3)&&r.GetBoolean(3),r.IsDBNull(6)?null:r.GetString(6));
    }
    public async Task<IReadOnlyList<StockAdjustment>> AdjustAsync(IReadOnlyList<StockAdjustment> adjustments,CancellationToken ct)
    {
        if(adjustments.Count==0)return[];await using SqlConnection c=factory.Create();await c.OpenAsync(ct);await using SqlTransaction tx=(SqlTransaction)await c.BeginTransactionAsync(ct);try{List<StockAdjustment> applied=[];foreach(StockAdjustment a in adjustments){await ApplyAsync(c,tx,a,ct);applied.Add(a);}await tx.CommitAsync(ct);return applied;}catch(Exception e){await tx.RollbackAsync(ct);throw e as TTSmartEcom.Application.Common.Errors.ApplicationException??Fail(409,"Tồn kho vừa được thay đổi hoặc không đủ số lượng.",e);}
    }
    public Task RollbackAsync(IReadOnlyList<StockAdjustment> adjustments,CancellationToken ct)=>AdjustAsync(adjustments.Reverse().Select(x=>x with{QuantityForSaleDelta=-x.QuantityForSaleDelta,QuantityInStorageDelta=-x.QuantityInStorageDelta,PurchaseCountDelta=-x.PurchaseCountDelta}).ToArray(),ct);
    private static async Task ApplyAsync(SqlConnection c,SqlTransaction tx,StockAdjustment a,CancellationToken ct)
    {
        if(a.VariantIndex<0)throw Fail(400,"Mã sản phẩm hoặc phiên bản không hợp lệ.");await using SqlCommand q=new("UPDATE v SET QuantityForSale=COALESCE(v.QuantityForSale,0)+@sale,QuantityInStorage=COALESCE(v.QuantityInStorage,0)+@storage FROM dbo.ProductVariants v JOIN dbo.Products p ON p.ProductId=v.ProductId WHERE p.PublicId=@product AND v.SortOrder=@variant AND (@expected IS NULL OR v.PublicId=@expected) AND (@sale>=0 OR COALESCE(v.QuantityForSale,0)>=-@sale) AND (@storage>=0 OR COALESCE(v.QuantityInStorage,0)>=-@storage) AND (@purchase>=0 OR p.PurchaseCount>=-@purchase); IF @@ROWCOUNT=1 UPDATE p SET PurchaseCount=p.PurchaseCount+@purchase,Version=p.Version+1 FROM dbo.Products p JOIN dbo.ProductVariants v ON v.ProductId=p.ProductId WHERE p.PublicId=@product AND v.SortOrder=@variant AND (@expected IS NULL OR v.PublicId=@expected);",c,tx);q.Parameters.AddWithValue("@product",a.ProductId);q.Parameters.AddWithValue("@variant",a.VariantIndex);q.Parameters.AddWithValue("@expected",(object?)a.ExpectedVariantId??DBNull.Value);q.Parameters.AddWithValue("@sale",a.QuantityForSaleDelta);q.Parameters.AddWithValue("@storage",a.QuantityInStorageDelta);SqlParameter purchase=q.Parameters.Add("@purchase",SqlDbType.Decimal);purchase.Precision=19;purchase.Scale=6;purchase.Value=(decimal)a.PurchaseCountDelta;if(await q.ExecuteNonQueryAsync(ct)!=2)throw Fail(409,"Tồn kho vừa được thay đổi hoặc không đủ số lượng.");
    }
    private static string? Text(JsonElement root,string name)=>root.TryGetProperty(name,out JsonElement value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;private static double Number(JsonElement root,string name,double fallback)=>root.TryGetProperty(name,out JsonElement value)&&value.TryGetDouble(out double x)?x:fallback;private static TTSmartEcom.Application.Common.Errors.ApplicationException Fail(int status,string message,Exception? inner=null)=>new(new ApplicationError($"TTS-STOCK-{status}",4400+status,status,message),inner);
}
