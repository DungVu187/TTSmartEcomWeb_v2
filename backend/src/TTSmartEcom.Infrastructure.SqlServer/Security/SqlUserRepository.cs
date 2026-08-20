using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Users;
#pragma warning disable CA1725

namespace TTSmartEcom.Infrastructure.SqlServer.Security;

public sealed class SqlUserRepository(ISqlConnectionFactory factory) : IUserRepository
{
    public async Task<UserRecord?> FindByLoginAsync(string identifier, CancellationToken ct) => await FindAsync("(Phone=@x OR LOWER(Email)=LOWER(@x))", identifier, ct);
    public async Task<PasswordRecoveryUser?> FindForPasswordRecoveryAsync(string identifier, CancellationToken ct)
    {
        UserRecord? user=await FindByLoginAsync(identifier,ct); return user is null?null:new(user.Id,user.Phone,user.Email,user.Name);
    }
    public Task<UserIdentitySnapshot?> FindIdentityAsync(string id,CancellationToken ct) => new SqlUserIdentityReader(factory).FindByIdAsync(id,ct);
    public async Task<bool> StorePasswordResetOtpAsync(string id,string otp,DateTimeOffset expires,CancellationToken ct) => await WriteAsync("UPDATE dbo.Users SET ResetOtpHash=@h,ResetOtpExpiresAtUtc=@e WHERE PublicId=@i AND IsDeleted=0;",ct,("@h",Hash(otp)),("@e",expires.UtcDateTime),("@i",id));
    public async Task<bool> ClearPasswordResetOtpAsync(string id,string otp,CancellationToken ct) => await WriteAsync("UPDATE dbo.Users SET ResetOtpHash=NULL,ResetOtpExpiresAtUtc=NULL WHERE PublicId=@i AND ResetOtpHash=@h;",ct,("@i",id),("@h",Hash(otp)));
    public async Task<bool> ResetPasswordWithOtpAsync(string id,string otp,DateTimeOffset now,string hash,string replacement,DateTimeOffset changed,CancellationToken ct) => await WriteAsync("UPDATE dbo.Users SET PasswordHash=@p,AutoLoginTokenHash=@t,PasswordChangedAtUtc=@c,ResetOtpHash=NULL,ResetOtpExpiresAtUtc=NULL WHERE PublicId=@i AND ResetOtpHash=@h AND ResetOtpExpiresAtUtc>=@n;",ct,("@p",hash),("@t",Hash(replacement)),("@c",changed.UtcDateTime),("@i",id),("@h",Hash(otp)),("@n",now.UtcDateTime));
    public async Task<UserRecord?> ConsumeAutologinTokenAsync(string token,string replacement,CancellationToken ct)
    {
        await using SqlConnection c=factory.Create(); await c.OpenAsync(ct); await using var q=new SqlCommand("UPDATE dbo.Users SET AutoLoginTokenHash=@n OUTPUT inserted.PublicId,inserted.Phone,inserted.Email,inserted.Name,inserted.PasswordHash,inserted.Role,inserted.FunctionsJson,inserted.PermissionsJson,inserted.PasswordChangedAtUtc WHERE AutoLoginTokenHash=@o AND IsDeleted=0;",c); q.Parameters.AddWithValue("@n",Hash(replacement));q.Parameters.AddWithValue("@o",Hash(token)); await using var r=await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct)?Read(r):null;
    }
    private async Task<UserRecord?> FindAsync(string predicate,string identifier,CancellationToken ct) { await using SqlConnection c=factory.Create();await c.OpenAsync(ct);await using var q=new SqlCommand($"SELECT PublicId,Phone,Email,Name,PasswordHash,Role,FunctionsJson,PermissionsJson,PasswordChangedAtUtc FROM dbo.Users WHERE {predicate} AND IsDeleted=0;",c);q.Parameters.AddWithValue("@x",identifier);await using var r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?Read(r):null; }
    private async Task<bool> WriteAsync(string sql,CancellationToken ct,params (string,object)[] ps){await using SqlConnection c=factory.Create();await c.OpenAsync(ct);await using var q=new SqlCommand(sql,c);foreach(var(n,v) in ps)q.Parameters.AddWithValue(n,v);return await q.ExecuteNonQueryAsync(ct)==1;}
    private static UserRecord Read(SqlDataReader r)=>new(r.GetString(0),r.IsDBNull(1)?string.Empty:r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),r.IsDBNull(4)?string.Empty:r.GetString(4),r.IsDBNull(5)?"customer":r.GetString(5),Json(r,6),Json(r,7),r.IsDBNull(8)?null:new DateTimeOffset(r.GetDateTime(8),TimeSpan.Zero));
    private static string[] Json(SqlDataReader r,int n)=>r.IsDBNull(n)?[]:JsonSerializer.Deserialize<string[]>(r.GetString(n))??[];
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
