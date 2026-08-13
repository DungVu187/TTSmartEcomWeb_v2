using MongoDB.Bson;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Users;

internal enum MongoUserArrayMutationStatus
{
    Updated,
    UserNotFound,
    ItemNotFound,
}

internal sealed record MongoUserArrayMutationResult(
    MongoUserArrayMutationStatus Status,
    BsonDocument? Document = null);

internal static class MongoUserArrayCompareExchange
{
    private const int MaximumAttempts = 8;

    internal static async Task<MongoUserArrayMutationResult> ExecuteAsync(
        string field,
        Func<CancellationToken, Task<BsonDocument?>> readAsync,
        Func<BsonDocument, BsonArray, CancellationToken, Task<BsonDocument?>> compareExchangeAsync,
        Func<BsonArray, bool> mutate,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            BsonDocument? source = await readAsync(cancellationToken);
            if (source is null)
            {
                return new MongoUserArrayMutationResult(MongoUserArrayMutationStatus.UserNotFound);
            }

            BsonArray value = CopyArray(source, field);
            if (!mutate(value))
            {
                return new MongoUserArrayMutationResult(MongoUserArrayMutationStatus.ItemNotFound);
            }

            BsonDocument? updated = await compareExchangeAsync(source, value, cancellationToken);
            if (updated is not null)
            {
                return new MongoUserArrayMutationResult(MongoUserArrayMutationStatus.Updated, updated);
            }
        }

        throw new InvalidOperationException("Concurrent user profile mutation retry limit exceeded.");
    }

    private static BsonArray CopyArray(BsonDocument source, string field) =>
        source.TryGetValue(field, out BsonValue value) && value.IsBsonArray
            ? value.AsBsonArray.DeepClone().AsBsonArray
            : [];
}

internal sealed class MongoUserArrayTarget
{
    private readonly string? id;
    private readonly BsonDocument snapshot;

    private MongoUserArrayTarget(BsonDocument value)
    {
        id = ReadId(value);
        snapshot = value.DeepClone().AsBsonDocument;
    }

    internal static bool TryCreate(BsonArray values, int index, out MongoUserArrayTarget? target)
    {
        if (index < 0 || index >= values.Count || !values[index].IsBsonDocument)
        {
            target = null;
            return false;
        }

        target = new MongoUserArrayTarget(values[index].AsBsonDocument);
        return true;
    }

    internal BsonDocument? Find(BsonArray values)
    {
        foreach (BsonValue value in values)
        {
            if (!value.IsBsonDocument)
            {
                continue;
            }

            BsonDocument candidate = value.AsBsonDocument;
            if (id is not null
                    ? string.Equals(ReadId(candidate), id, StringComparison.Ordinal)
                    : candidate.Equals(snapshot))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ReadId(BsonDocument value)
    {
        if (!value.TryGetValue("_id", out BsonValue idValue) || idValue.IsBsonNull)
        {
            return null;
        }

        string? normalized = idValue.ToString();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
