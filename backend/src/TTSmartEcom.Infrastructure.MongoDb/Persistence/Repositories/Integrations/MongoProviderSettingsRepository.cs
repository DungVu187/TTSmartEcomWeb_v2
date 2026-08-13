using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Integrations;
using TTSmartEcom.Domain.Integrations;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Integrations;

public sealed class MongoProviderSettingsRepository(IMongoDatabaseProvider databaseProvider) : IProviderSettingsRepository
{
    private readonly IMongoCollection<TelegramConfigDocument> telegram = databaseProvider.Database.GetCollection<TelegramConfigDocument>(TelegramConfigDocument.CollectionName);
    private readonly IMongoCollection<ZaloConfigDocument> zalo = databaseProvider.Database.GetCollection<ZaloConfigDocument>(ZaloConfigDocument.CollectionName);

    public async Task<TelegramSettings> GetTelegramAsync(CancellationToken cancellationToken) => Map(await GetTelegramDocumentAsync(cancellationToken));

    public async Task<TelegramSettings> SetTelegramEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        TelegramConfigDocument document = await GetTelegramDocumentAsync(cancellationToken);
        document.Enabled = enabled;
        await SaveTelegramAsync(document, cancellationToken);
        return Map(document);
    }

    public async Task<TelegramRecipient> AddTelegramRecipientAsync(TelegramRecipientInput input, CancellationToken cancellationToken)
    {
        TelegramConfigDocument document = await GetTelegramDocumentAsync(cancellationToken);
        TelegramRecipientDocument recipient = ToDocument(input, ObjectId.GenerateNewId());
        (document.Recipients ??= []).Add(recipient);
        await SaveTelegramAsync(document, cancellationToken);
        return Map(recipient);
    }

    public async Task<TelegramRecipient?> UpdateTelegramRecipientAsync(string id, TelegramRecipientInput input, CancellationToken cancellationToken)
    {
        TelegramConfigDocument document = await GetTelegramDocumentAsync(cancellationToken);
        TelegramRecipientDocument? recipient = (document.Recipients ?? []).FirstOrDefault(x => x.Id?.ToString() == id);
        if (recipient is null) return null;
        if (input.Label is not null) recipient.Label = input.Label;
        if (input.ChatId is not null) recipient.ChatId = input.ChatId;
        if (input.Type is not null) recipient.Type = input.Type;
        if (input.Enabled.HasValue) recipient.Enabled = input.Enabled.Value;
        if (input.NotifyTypes is not null) recipient.NotifyTypes = input.NotifyTypes.ToList();
        await SaveTelegramAsync(document, cancellationToken);
        return Map(recipient);
    }

    public async Task<bool> DeleteTelegramRecipientAsync(string id, CancellationToken cancellationToken)
    {
        TelegramConfigDocument document = await GetTelegramDocumentAsync(cancellationToken);
        int removed = (document.Recipients ??= []).RemoveAll(x => x.Id?.ToString() == id);
        if (removed == 0) return false;
        await SaveTelegramAsync(document, cancellationToken);
        return true;
    }

    public async Task<ZaloSettings> GetZaloAsync(CancellationToken cancellationToken) => Map(await GetZaloDocumentAsync(cancellationToken));

    public async Task<ZaloSettings> UpdateZaloAsync(ZaloSettingsInput input, CancellationToken cancellationToken)
    {
        ZaloConfigDocument document = await GetZaloDocumentAsync(cancellationToken);
        if (input.AppId is not null) document.AppId = input.AppId;
        if (input.SecretKey is not null) document.SecretKey = input.SecretKey;
        if (input.OaId is not null) document.OaId = input.OaId;
        if (input.RecipientUserId is not null) document.RecipientUserId = input.RecipientUserId;
        await zalo.ReplaceOneAsync(x => x.Id == document.Id, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        return Map(document);
    }

    public async Task<string?> GetZaloSecretKeyAsync(CancellationToken cancellationToken) =>
        (await GetZaloDocumentAsync(cancellationToken)).SecretKey;

    public async Task SaveZaloOAuthTokensAsync(
        string accessToken,
        string? refreshToken,
        DateTimeOffset expiresAt,
        string? oaId,
        CancellationToken cancellationToken)
    {
        ZaloConfigDocument document = await GetZaloDocumentAsync(cancellationToken);
        document.AccessToken = accessToken;
        document.RefreshToken = refreshToken;
        document.ExpiresAt = expiresAt.UtcDateTime;
        if (!string.IsNullOrWhiteSpace(oaId)) document.OaId = oaId;
        document.UpdatedAt = DateTime.UtcNow;
        await zalo.ReplaceOneAsync(x => x.Id == document.Id, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    private async Task<TelegramConfigDocument> GetTelegramDocumentAsync(CancellationToken cancellationToken)
    {
        TelegramConfigDocument? document = await telegram.Find(Builders<TelegramConfigDocument>.Filter.Empty).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return document ?? new TelegramConfigDocument { Id = ObjectId.GenerateNewId(), Enabled = false, Recipients = [] };
    }

    private async Task<ZaloConfigDocument> GetZaloDocumentAsync(CancellationToken cancellationToken)
    {
        ZaloConfigDocument? document = await zalo.Find(Builders<ZaloConfigDocument>.Filter.Empty).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return document ?? new ZaloConfigDocument { Id = ObjectId.GenerateNewId() };
    }

    private Task<ReplaceOneResult> SaveTelegramAsync(TelegramConfigDocument document, CancellationToken cancellationToken) =>
        telegram.ReplaceOneAsync(x => x.Id == document.Id, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);

    private static TelegramSettings Map(TelegramConfigDocument document) => new(document.Enabled ?? false, (document.Recipients ?? []).Select(Map).ToArray());
    private static TelegramRecipient Map(TelegramRecipientDocument document) => new(document.Id?.ToString() ?? string.Empty, document.Label ?? string.Empty, document.ChatId ?? string.Empty, document.Type ?? "personal", document.Enabled ?? true, (document.NotifyTypes ?? ["new_order"]).ToArray());
    private static TelegramRecipientDocument ToDocument(TelegramRecipientInput input, ObjectId id) => new() { Id = id, Label = input.Label ?? string.Empty, ChatId = input.ChatId ?? string.Empty, Type = input.Type ?? "personal", Enabled = input.Enabled ?? true, NotifyTypes = (input.NotifyTypes ?? ["new_order"]).ToList() };
    private static ZaloSettings Map(ZaloConfigDocument document) => new(document.AppId ?? string.Empty, document.OaId ?? string.Empty, document.RecipientUserId ?? string.Empty, !string.IsNullOrWhiteSpace(document.AccessToken) && document.ExpiresAt > DateTime.UtcNow, document.ExpiresAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(document.ExpiresAt.Value, DateTimeKind.Utc)) : null, !string.IsNullOrWhiteSpace(document.SecretKey));
}
