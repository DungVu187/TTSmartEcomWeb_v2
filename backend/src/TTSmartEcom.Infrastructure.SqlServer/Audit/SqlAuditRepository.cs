using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Domain.Audit;

namespace TTSmartEcom.Infrastructure.SqlServer.Audit;

public sealed class SqlAuditRepository(
    IOperationalDbConnectionFactory factory,
    ICompanyDbConnectionFactory companyFactory) : IAuditRepository, IActivityLogWriter
{
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["create_product"]="Tạo sản phẩm", ["update_product"]="Sửa sản phẩm", ["delete_product"]="Xóa sản phẩm",
        ["create_user"]="Tạo tài khoản", ["update_user"]="Sửa tài khoản", ["delete_user"]="Xóa tài khoản",
        ["create_station"]="Tạo trạm trộn", ["update_station"]="Sửa trạm trộn", ["delete_station"]="Xóa trạm trộn",
        ["update_zalo_settings"]="Cập nhật cấu hình Zalo OA", ["update_telegram_settings"]="Cập nhật cấu hình Telegram",
        ["create_voice_vocab"]="Thêm từ vựng tìm kiếm giọng nói", ["update_voice_vocab"]="Sửa từ vựng tìm kiếm giọng nói", ["delete_voice_vocab"]="Xóa từ vựng tìm kiếm giọng nói",
    };

    public async Task AppendAsync(ActivityLogWriteEntry entry, CancellationToken cancellationToken) => await AppendManyAsync([entry], cancellationToken);

    public async Task AppendManyAsync(IReadOnlyCollection<ActivityLogWriteEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count == 0) return;
        await using SqlConnection connection=factory.Create(); await connection.OpenAsync(cancellationToken); await using SqlTransaction transaction=(SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (ActivityLogWriteEntry entry in entries)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(entry.UserName); ArgumentException.ThrowIfNullOrWhiteSpace(entry.Action);
                JsonObject json=new() { ["details"] = JsonSerializer.SerializeToNode(entry.Details, JsonOptions) };
                if (entry.ProductId is not null) json["productId"] = entry.ProductId;
                if (entry.ProductName is not null) json["productName"] = entry.ProductName;
                await using SqlCommand command=new("INSERT dbo.ActivityLogs(ActivityLogId,PublicId,Action,ActorName,DetailsJson,CreatedAtUtc,Version) VALUES(NEWID(),@id,@action,@actor,@details,SYSUTCDATETIME(),0);",connection,transaction);
                command.Parameters.AddWithValue("@id",SqlPublicIds.New()); command.Parameters.AddWithValue("@action",entry.Action); command.Parameters.AddWithValue("@actor",entry.UserName); command.Parameters.AddWithValue("@details",json.ToJsonString()); await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }
    }

    public async Task<ActivityLogPage> QueryAsync(ActivityLogQuery query, CancellationToken cancellationToken)
    {
        await using SqlConnection connection=factory.Create();await connection.OpenAsync(cancellationToken);
        List<string> filters=[]; await using SqlCommand count=new(){Connection=connection};
        AddFilters(filters,count,query); count.CommandText=$"SELECT COUNT(*) FROM dbo.ActivityLogs{Where(filters)};";
        long total=Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken),System.Globalization.CultureInfo.InvariantCulture);
        await using SqlCommand command=new(){Connection=connection}; AddFilters(filters,command,query); command.CommandText=$"SELECT PublicId,ActorName,Action,DetailsJson,CreatedAtUtc FROM dbo.ActivityLogs{Where(filters)} ORDER BY CreatedAtUtc DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;";command.Parameters.AddWithValue("@skip",checked((Math.Max(1,query.Page)-1)*Math.Max(1,query.Limit)));command.Parameters.AddWithValue("@take",Math.Max(1,query.Limit));
        List<ActivityLog> logs=[];await using SqlDataReader reader=await command.ExecuteReaderAsync(cancellationToken);while(await reader.ReadAsync(cancellationToken))logs.Add(Map(reader));
        return new ActivityLogPage(true,query.Page,query.Limit,total,(int)Math.Ceiling(total/(double)Math.Max(1,query.Limit)),logs,Labels,await ReferencesAsync(connection,logs,cancellationToken));
    }

    private static void AddFilters(List<string> filters,SqlCommand command,ActivityLogQuery query)
    {
        if(query.StartDate.HasValue){filters.Add("CreatedAtUtc>=@start");command.Parameters.AddWithValue("@start",query.StartDate.Value.UtcDateTime);} if(query.EndDate.HasValue){filters.Add("CreatedAtUtc<=@end");command.Parameters.AddWithValue("@end",query.EndDate.Value.UtcDateTime);} if(!string.IsNullOrWhiteSpace(query.UserName)){filters.Add("ActorName LIKE @user");command.Parameters.AddWithValue("@user","%"+query.UserName.Trim()+"%");} if(!string.IsNullOrWhiteSpace(query.ProductName)){filters.Add("DetailsJson LIKE @product");command.Parameters.AddWithValue("@product","%"+query.ProductName.Trim()+"%");} if(!string.IsNullOrWhiteSpace(query.Action)){filters.Add("Action=@action");command.Parameters.AddWithValue("@action",query.Action.Trim());}
    }
    private static string Where(List<string> filters)=>filters.Count==0?string.Empty:" WHERE "+string.Join(" AND ",filters);
    private static ActivityLog Map(SqlDataReader reader)
    {
        JsonObject details=Parse(reader.IsDBNull(3)?"{}":reader.GetString(3)); JsonArray lines=details["details"] as JsonArray??[];
        return new ActivityLog(reader.GetString(0),reader.IsDBNull(1)?null:reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),String(details,"productId"),String(details,"productName"),lines.OfType<JsonObject>().Select(x=>new ActivityLogDetail(String(x,"field"),String(x,"oldValue")??string.Empty,String(x,"newValue")??string.Empty)).ToArray(),reader.IsDBNull(4)?null:new DateTimeOffset(reader.GetDateTime(4),TimeSpan.Zero),reader.IsDBNull(4)?null:new DateTimeOffset(reader.GetDateTime(4),TimeSpan.Zero));
    }
    private async Task<ActivityLogReferences> ReferencesAsync(SqlConnection c,IReadOnlyList<ActivityLog> logs,CancellationToken ct)
    {
        HashSet<string> products=[];HashSet<string> stations=[];foreach(ActivityLog log in logs)foreach(ActivityLogDetail detail in log.Details){string field=detail.Field??string.Empty;HashSet<string> target=field.Equals("station",StringComparison.OrdinalIgnoreCase)?stations:field.Equals("productId",StringComparison.OrdinalIgnoreCase)?products:null!;if(target is not null)foreach(Match match in Regex.Matches((detail.OldValue??string.Empty)+" "+(detail.NewValue??string.Empty),@"\b[0-9a-fA-F]{24}\b"))target.Add(match.Value.ToLowerInvariant());}
        await using SqlConnection company=companyFactory.Create();await company.OpenAsync(ct);
        return new ActivityLogReferences(await LabelsAsync(company,"dbo.Products","PublicId","Code","Name",products,ct),await LabelsAsync(c,"dbo.Stations","PublicId","Code","Name",stations,ct));
    }
    private static async Task<Dictionary<string,string>> LabelsAsync(SqlConnection c,string table,string idColumn,string primary,string secondary,IEnumerable<string> ids,CancellationToken ct){string[] values=ids.Take(200).ToArray();Dictionary<string,string> result=new(StringComparer.OrdinalIgnoreCase);if(values.Length==0)return result;await using SqlCommand q=new($"SELECT {idColumn},{primary},{secondary} FROM {table} WHERE {idColumn} IN ({string.Join(',',values.Select((_,i)=>"@p"+i))});",c);for(int i=0;i<values.Length;i++)q.Parameters.AddWithValue("@p"+i,values[i]);await using SqlDataReader r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)){string id=r.GetString(0),a=r.IsDBNull(1)?string.Empty:r.GetString(1),b=r.IsDBNull(2)?string.Empty:r.GetString(2);result[id]=string.IsNullOrWhiteSpace(a)?b:string.IsNullOrWhiteSpace(b)?a:$"{a} - {b}";}return result;}
    private static JsonObject Parse(string value)=>JsonNode.Parse(value) as JsonObject??new JsonObject();
    private static string? String(JsonObject value,string property)=>value[property] is JsonValue node&&node.TryGetValue<string>(out string? text)?text:null;
    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
}
