using Microsoft.Data.SqlClient;
using TTSmartEcom.Domain.Voice;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Voice;
using Xunit.Sdk;

namespace TTSmartEcom.IntegrationTests;

public sealed class SqlVoiceIntegrationTests
{
    [Fact]
    public async Task VoiceVocabulary_SaveAndCompareAndSwap_PersistSqlSingleton()
    {
        string? configured = Environment.GetEnvironmentVariable("TTSMART_SQL_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configured)) throw SkipException.ForSkip("Cần TTSMART_SQL_INTEGRATION_CONNECTION cho test SQL cô lập.");
        string db = $"TTSmartEcomV2VoiceIntegration_{Guid.NewGuid():N}";
        SqlConnectionStringBuilder master = new(configured) { InitialCatalog = "master" };
        SqlConnectionStringBuilder test = new(configured) { InitialCatalog = db };
        try
        {
            await ExecuteAsync(master.ConnectionString, $"CREATE DATABASE [{db}];");
            await ExecuteAsync(test.ConnectionString, "CREATE TABLE dbo.VoiceSettings(VoiceSettingsId uniqueidentifier NOT NULL PRIMARY KEY,PublicId char(24) NOT NULL UNIQUE,ConfigurationJson nvarchar(max) NOT NULL,Version bigint NOT NULL);");
            var repository = new SqlVoiceVocabularyRepository(new Factory(test.ConnectionString));
            VoiceVocabulary source = new(["và"], ["Brand"], ["Type"], [], [], [], [], 0);
            VoiceVocabulary saved = Assert.IsType<VoiceVocabulary>(await repository.SaveAsync(source, 0, CancellationToken.None));
            Assert.Equal(0, saved.Version);
            VoiceVocabulary reloaded = Assert.IsType<VoiceVocabulary>(await repository.FindAsync(CancellationToken.None));
            Assert.Equal("Brand", Assert.Single(reloaded.Brands));
            VoiceVocabulary updated = Assert.IsType<VoiceVocabulary>(await repository.SaveAsync(reloaded with { Stopwords = ["và", "là"] }, reloaded.Version, CancellationToken.None));
            Assert.Equal(1, updated.Version);
            Assert.Null(await repository.SaveAsync(source, 0, CancellationToken.None));
        }
        finally { await ExecuteAsync(master.ConnectionString, $"IF DB_ID(N'{db}') IS NOT NULL BEGIN ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{db}]; END"); }
    }
    private static async Task ExecuteAsync(string cs,string sql){await using var c=new SqlConnection(cs);await c.OpenAsync();await using var q=new SqlCommand(sql,c);await q.ExecuteNonQueryAsync();}
    private sealed class Factory(string cs):ISqlConnectionFactory{public SqlConnection Create()=>new(cs);}
}
