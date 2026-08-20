using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Orders;

namespace TTSmartEcom.Infrastructure.SqlServer.Integrations;

#pragma warning disable CA1725

public sealed class SqlZaloOrderCredentialRepository(ISqlConnectionFactory factory,SqlProviderSettingsRepository settings,ISqlLocalSecretStore secrets) : IZaloOrderCredentialRepository
{
    public async Task<ZaloOrderDeliveryCredentials?> FindAsync(CancellationToken ct)
    {
        await using SqlConnection c=factory.Create();await c.OpenAsync(ct);await using SqlCommand q=new("SELECT PublicId,Version FROM dbo.Integrations WHERE IntegrationType=N'Zalo';",c);await using SqlDataReader r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;string id=r.GetString(0);int version=checked((int)r.GetInt64(1));SqlProviderSettingsRepository.Config value=await settings.GetAsync("Zalo",ct);string? secret=await secrets.GetAsync(value.SecretKeyReference,ct);string? access=await secrets.GetAsync(value.AccessTokenReference,ct);string? refresh=await secrets.GetAsync(value.RefreshTokenReference,ct);return new ZaloOrderDeliveryCredentials(id,version,value.AppId??string.Empty,secret??string.Empty,value.RecipientUserId??string.Empty,access??string.Empty,refresh??string.Empty,value.ExpiresAt);
    }
    public async Task<bool> TryUpdateTokensAsync(string configurationId,int expectedVersion,string accessToken,string refreshToken,DateTimeOffset expiresAt,CancellationToken ct)
    {
        SqlProviderSettingsRepository.Config value=await settings.GetAsync("Zalo",ct);var desired=value with{AccessTokenReference=await secrets.PutAsync(accessToken,ct),RefreshTokenReference=await secrets.PutAsync(refreshToken,ct),ExpiresAt=expiresAt};await using SqlConnection c=factory.Create();await c.OpenAsync(ct);await using SqlCommand q=new("UPDATE dbo.Integrations SET ConfigurationJson=@json,Version=Version+1 WHERE IntegrationType=N'Zalo' AND PublicId=@id AND Version=@version;",c);q.Parameters.AddWithValue("@json",System.Text.Json.JsonSerializer.Serialize(desired));q.Parameters.AddWithValue("@id",configurationId);q.Parameters.AddWithValue("@version",expectedVersion);return await q.ExecuteNonQueryAsync(ct)==1;
    }
}
