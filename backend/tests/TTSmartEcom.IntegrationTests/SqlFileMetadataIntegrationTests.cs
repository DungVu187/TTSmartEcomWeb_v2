using Microsoft.Data.SqlClient;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Files;
using Xunit.Sdk;

namespace TTSmartEcom.IntegrationTests;

public sealed class SqlFileMetadataIntegrationTests
{
    [Fact]
    public async Task MetadataRepository_ShouldUpsertThenRemoveFileRecord_InIsolatedSqlDatabase()
    {
        string? configuredConnection = Environment.GetEnvironmentVariable("TTSMART_SQL_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            throw SkipException.ForSkip("Cần TTSMART_SQL_INTEGRATION_CONNECTION trỏ SQL Server local dành cho test cô lập.");
        }

        string databaseName = $"TTSmartEcomV2FilesIntegration_{Guid.NewGuid():N}";
        SqlConnectionStringBuilder masterBuilder = new(configuredConnection) { InitialCatalog = "master" };
        SqlConnectionStringBuilder testBuilder = new(configuredConnection) { InitialCatalog = databaseName };
        try
        {
            await ExecuteAsync(masterBuilder.ConnectionString, $"CREATE DATABASE [{databaseName}];");
            await ExecuteAsync(testBuilder.ConnectionString, """
                CREATE TABLE dbo.Files (
                    FileId uniqueidentifier NOT NULL PRIMARY KEY,
                    PublicId char(24) NOT NULL UNIQUE,
                    StorageKey nvarchar(1000) NULL,
                    FileName nvarchar(500) NULL,
                    MimeType nvarchar(200) NULL,
                    ByteLength bigint NULL,
                    Sha256 char(64) NULL,
                    SourceUrl nvarchar(2000) NULL,
                    OwnerType nvarchar(100) NULL,
                    OwnerPublicId char(24) NULL,
                    DetailsJson nvarchar(max) NULL,
                    Version bigint NOT NULL CONSTRAINT DF_Files_Version DEFAULT 0
                );
                """);

            var repository = new SqlFileMetadataRepository(new TestConnectionFactory(testBuilder.ConnectionString));
            const string storageKey = "images/integration-file.png";
            const string firstHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            const string secondHash = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

            await repository.RecordAsync(storageKey, "integration-file.png", "image/png", 10, firstHash, "/images/integration-file.png", CancellationToken.None);
            await repository.RecordAsync(storageKey, "integration-file.png", "image/png", 12, secondHash, "/images/integration-file.png", CancellationToken.None);

            (long count, long length, string checksum, long version) = await ReadAsync(testBuilder.ConnectionString, storageKey);
            Assert.Equal(1, count);
            Assert.Equal(12, length);
            Assert.Equal(secondHash, checksum);
            Assert.Equal(1, version);

            await repository.MarkDeletedAsync(storageKey, CancellationToken.None);
            Assert.Equal(0, await CountAsync(testBuilder.ConnectionString));
        }
        finally
        {
            await ExecuteAsync(masterBuilder.ConnectionString, $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END");
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(long Count, long Length, string Checksum, long Version)> ReadAsync(string connectionString, string storageKey)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*), MAX(ByteLength), MAX(Sha256), MAX(Version) FROM dbo.Files WHERE StorageKey=@key;", connection);
        command.Parameters.AddWithValue("@key", storageKey);
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (
            Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
            reader.GetInt64(1),
            reader.GetString(2),
            reader.GetInt64(3));
    }

    private static async Task<long> CountAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.Files;", connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class TestConnectionFactory(string connectionString) : ISqlConnectionFactory
    {
        public SqlConnection Create() => new(connectionString);
    }
}
