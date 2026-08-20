using Microsoft.Data.SqlClient;
using TTSmartEcom.Domain.Integrations;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Integrations;
using Xunit.Sdk;

namespace TTSmartEcom.IntegrationTests;

public sealed class SqlIntegrationSettingsTests
{
    [Fact]
    public async Task TelegramAndZalo_SettingsStoreOnlySecretReferencesInSql()
    {
        string? configured = Environment.GetEnvironmentVariable("TTSMART_SQL_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configured)) throw SkipException.ForSkip("Cần TTSMART_SQL_INTEGRATION_CONNECTION cho test SQL cô lập.");
        string db = $"TTSmartEcomV2IntegrationSettings_{Guid.NewGuid():N}";
        SqlConnectionStringBuilder master = new(configured) { InitialCatalog = "master" }; SqlConnectionStringBuilder test = new(configured) { InitialCatalog = db };
        try
        {
            await Exec(master.ConnectionString,$"CREATE DATABASE [{db}];");
            await Exec(test.ConnectionString,"CREATE TABLE dbo.Integrations(IntegrationId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,IntegrationType nvarchar(50) NOT NULL UNIQUE,ConfigurationJson nvarchar(max) NOT NULL,SecretReference nvarchar(500) NULL,Version bigint NOT NULL);");
            var secrets=new FakeSecrets(); var repo=new SqlProviderSettingsRepository(new Factory(test.ConnectionString),secrets);
            TelegramRecipient recipient=await repo.AddTelegramRecipientAsync(new TelegramRecipientInput("Đơn mới","chat-secret","personal",true,["new_order"]),CancellationToken.None);
            Assert.Equal("chat-secret",recipient.ChatId);
            await repo.UpdateZaloAsync(new ZaloSettingsInput("app","zalo-secret","oa","recipient"),CancellationToken.None);
            Assert.Equal("zalo-secret",await repo.GetZaloSecretKeyAsync(CancellationToken.None));
            string json=await ScalarAsync(test.ConnectionString,"SELECT STRING_AGG(ConfigurationJson,N'|') FROM dbo.Integrations;");
            Assert.DoesNotContain("chat-secret",json,StringComparison.Ordinal);
            Assert.DoesNotContain("zalo-secret",json,StringComparison.Ordinal);
            Assert.Equal(2,await CountAsync(test.ConnectionString));
        }
        finally {await Exec(master.ConnectionString,$"IF DB_ID(N'{db}') IS NOT NULL BEGIN ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{db}]; END");}
    }
    private static async Task Exec(string cs,string sql){await using var c=new SqlConnection(cs);await c.OpenAsync();await using var q=new SqlCommand(sql,c);await q.ExecuteNonQueryAsync();}
    private static async Task<string> ScalarAsync(string cs,string sql){await using var c=new SqlConnection(cs);await c.OpenAsync();await using var q=new SqlCommand(sql,c);return (string)(await q.ExecuteScalarAsync()??string.Empty);}
    private static async Task<long> CountAsync(string cs){await using var c=new SqlConnection(cs);await c.OpenAsync();await using var q=new SqlCommand("SELECT COUNT(*) FROM dbo.Integrations;",c);return Convert.ToInt64(await q.ExecuteScalarAsync(),System.Globalization.CultureInfo.InvariantCulture);}
    private sealed class Factory(string cs):ISqlConnectionFactory{public SqlConnection Create()=>new(cs);}
    private sealed class FakeSecrets:ISqlLocalSecretStore{private readonly Dictionary<string,string> data=[];public Task<string> PutAsync(string value,CancellationToken ct){string key="ref-"+data.Count;data[key]=value;return Task.FromResult(key);}public Task<string?> GetAsync(string? reference,CancellationToken ct)=>Task.FromResult(reference is not null&&data.TryGetValue(reference,out string? value)?value:null);}
}
