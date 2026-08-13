using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Voice;
using TTSmartEcom.Domain.Voice;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Voice;

public sealed class MongoVoiceVocabularyRepository(IMongoDatabaseProvider databaseProvider) : IVoiceVocabularyRepository
{
    private readonly IMongoCollection<VoiceVocabDocument> collection = databaseProvider.Database.GetCollection<VoiceVocabDocument>(VoiceVocabDocument.CollectionName);

    public async Task<VoiceVocabulary?> FindAsync(CancellationToken cancellationToken)
    {
        VoiceVocabDocument? document = await collection.Find(Builders<VoiceVocabDocument>.Filter.Empty).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : Map(document);
    }

    public async Task<VoiceVocabulary?> SaveAsync(VoiceVocabulary vocabulary, int expectedVersion, CancellationToken cancellationToken)
    {
        VoiceVocabDocument? existing = await collection.Find(Builders<VoiceVocabDocument>.Filter.Empty).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            if (expectedVersion != 0) return null;
            VoiceVocabDocument created = ToDocument(vocabulary with { Version = 0 }, ObjectId.GenerateNewId());
            VoiceVocabDocument? seeded = await collection.FindOneAndUpdateAsync(
                Builders<VoiceVocabDocument>.Filter.Empty,
                Builders<VoiceVocabDocument>.Update
                    .SetOnInsert(x => x.Id, created.Id)
                    .SetOnInsert(x => x.Version, created.Version)
                    .SetOnInsert(x => x.Stopwords, created.Stopwords)
                    .SetOnInsert(x => x.Brands, created.Brands)
                    .SetOnInsert(x => x.Types, created.Types)
                    .SetOnInsert(x => x.BrandAliases, created.BrandAliases)
                    .SetOnInsert(x => x.TypeAliases, created.TypeAliases)
                    .SetOnInsert(x => x.IntentAliases, created.IntentAliases)
                    .SetOnInsert(x => x.CodeMap, created.CodeMap)
                    .SetOnInsert(x => x.CreatedAt, DateTime.UtcNow)
                    .SetOnInsert(x => x.UpdatedAt, DateTime.UtcNow),
                new FindOneAndUpdateOptions<VoiceVocabDocument>
                {
                    IsUpsert = true,
                    ReturnDocument = ReturnDocument.After,
                },
                cancellationToken);
            return seeded is not null && seeded.Id == created.Id ? Map(seeded) : null;
        }
        VoiceVocabDocument replacement = ToDocument(vocabulary with { Version = checked(expectedVersion + 1) }, existing.Id);
        replacement.CreatedAt = existing.CreatedAt;
        replacement.UpdatedAt = DateTime.UtcNow;
        replacement.ExtraElements = existing.ExtraElements;
        FilterDefinition<VoiceVocabDocument> versionFilter = expectedVersion == 0
            ? Builders<VoiceVocabDocument>.Filter.Or(
                Builders<VoiceVocabDocument>.Filter.Eq(x => x.Version, 0),
                Builders<VoiceVocabDocument>.Filter.Eq(x => x.Version, null))
            : Builders<VoiceVocabDocument>.Filter.Eq(x => x.Version, expectedVersion);
        ReplaceOneResult result = await collection.ReplaceOneAsync(
            Builders<VoiceVocabDocument>.Filter.And(
                Builders<VoiceVocabDocument>.Filter.Eq(x => x.Id, existing.Id),
                versionFilter),
            replacement, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1 ? Map(replacement) : null;
    }

    private static VoiceVocabulary Map(VoiceVocabDocument document) => new(
        (document.Stopwords ?? []).ToArray(), (document.Brands ?? []).ToArray(), (document.Types ?? []).ToArray(),
        (document.BrandAliases ?? []).Select(x => new VoiceBrandAlias(x.Name ?? string.Empty, (x.Aliases ?? []).ToArray())).ToArray(),
        (document.TypeAliases ?? []).Select(x => new VoiceTypeAlias(x.Type ?? string.Empty, x.Keyword ?? string.Empty, (x.Aliases ?? []).ToArray())).ToArray(),
        (document.IntentAliases ?? []).Select(x => new VoiceIntentAlias(x.Intent ?? string.Empty, x.Label ?? string.Empty, (x.Aliases ?? []).ToArray())).ToArray(),
        (document.CodeMap ?? []).Select(x => new VoiceCodeMap(x.Code ?? string.Empty, x.Keyword ?? string.Empty, x.Brand, x.Type, (x.Patterns ?? []).ToArray(), x.Compact ?? string.Empty)).ToArray(), document.Version ?? 0);

    private static VoiceVocabDocument ToDocument(VoiceVocabulary vocabulary, ObjectId id) => new()
    {
        Id = id,
        Version = vocabulary.Version,
        Stopwords = vocabulary.Stopwords.ToList(),
        Brands = vocabulary.Brands.ToList(),
        Types = vocabulary.Types.ToList(),
        BrandAliases = vocabulary.BrandAliases.Select(x => new VoiceBrandAliasDocument { Name = x.Name, Aliases = x.Aliases.ToList() }).ToList(),
        TypeAliases = vocabulary.TypeAliases.Select(x => new VoiceTypeAliasDocument { Type = x.Type, Keyword = x.Keyword, Aliases = x.Aliases.ToList() }).ToList(),
        IntentAliases = vocabulary.IntentAliases.Select(x => new VoiceIntentAliasDocument { Intent = x.Intent, Label = x.Label, Aliases = x.Aliases.ToList() }).ToList(),
        CodeMap = vocabulary.CodeMap.Select(x => new VoiceCodeMapDocument { Code = x.Code, Keyword = x.Keyword, Brand = x.Brand, Type = x.Type, Patterns = x.Patterns.ToList(), Compact = x.Compact }).ToList(),
    };
}
