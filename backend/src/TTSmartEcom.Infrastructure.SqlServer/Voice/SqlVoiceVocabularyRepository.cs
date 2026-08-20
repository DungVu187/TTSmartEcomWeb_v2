using System.Text.Json;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Voice;
using TTSmartEcom.Domain.Voice;

namespace TTSmartEcom.Infrastructure.SqlServer.Voice;

/// <summary>
/// Stores the legacy singleton voice document as JSON.  The SQL version column
/// is the compare-and-swap token; it deliberately remains distinct from rowversion.
/// </summary>
public sealed class SqlVoiceVocabularyRepository(ISqlConnectionFactory factory) : IVoiceVocabularyRepository
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<VoiceVocabulary?> FindAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = new("SELECT TOP (1) ConfigurationJson, Version FROM dbo.VoiceSettings ORDER BY PublicId;", connection);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? Read(reader.GetString(0), checked((int)reader.GetInt64(1)))
            : null;
    }

    public async Task<VoiceVocabulary?> SaveAsync(VoiceVocabulary vocabulary, int expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        string payload = JsonSerializer.Serialize(new VoicePayload(
            vocabulary.Stopwords, vocabulary.Brands, vocabulary.Types, vocabulary.BrandAliases,
            vocabulary.TypeAliases, vocabulary.IntentAliases, vocabulary.CodeMap));
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);

        // There is one document by contract.  An insert is used only for the empty database
        // path; a duplicate key caused by a concurrent seed is treated as CAS failure.
        await using (SqlCommand update = new("UPDATE dbo.VoiceSettings SET ConfigurationJson=@json, Version=Version+1 WHERE VoiceSettingsId=(SELECT TOP (1) VoiceSettingsId FROM dbo.VoiceSettings ORDER BY PublicId) AND Version=@expected;", connection))
        {
            update.Parameters.AddWithValue("@json", payload);
            update.Parameters.AddWithValue("@expected", expectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) == 1)
            {
                return vocabulary with { Version = checked(expectedVersion + 1) };
            }
        }

        if (expectedVersion != 0) return null;
        await using SqlCommand insert = new("INSERT dbo.VoiceSettings(VoiceSettingsId,PublicId,ConfigurationJson,Version) SELECT NEWID(),@id,@json,0 WHERE NOT EXISTS(SELECT 1 FROM dbo.VoiceSettings WITH(UPDLOCK,HOLDLOCK));", connection);
        insert.Parameters.AddWithValue("@id", SqlPublicIds.New());
        insert.Parameters.AddWithValue("@json", payload);
        return await insert.ExecuteNonQueryAsync(cancellationToken) == 1
            ? vocabulary with { Version = 0 }
            : null;
    }

    private static VoiceVocabulary Read(string payload, int version)
    {
        VoicePayload? source = JsonSerializer.Deserialize<VoicePayload>(payload, Json);
        return new VoiceVocabulary(
            source?.Stopwords ?? [], source?.Brands ?? [], source?.Types ?? [],
            source?.BrandAliases ?? [], source?.TypeAliases ?? [], source?.IntentAliases ?? [],
            source?.CodeMap ?? [], version);
    }

    private sealed record VoicePayload(
        IReadOnlyList<string>? Stopwords,
        IReadOnlyList<string>? Brands,
        IReadOnlyList<string>? Types,
        IReadOnlyList<VoiceBrandAlias>? BrandAliases,
        IReadOnlyList<VoiceTypeAlias>? TypeAliases,
        IReadOnlyList<VoiceIntentAlias>? IntentAliases,
        IReadOnlyList<VoiceCodeMap>? CodeMap);
}
