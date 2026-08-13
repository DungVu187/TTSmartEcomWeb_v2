using System.Security.Cryptography;
using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Users;
using TTSmartEcom.Domain.Users;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Users;

public sealed class MongoUserProfileRepository(IMongoDatabaseProvider databaseProvider) : IUserProfileRepository
{
    private readonly IMongoCollection<BsonDocument> users =
        databaseProvider.Database.GetCollection<BsonDocument>(UserDocument.CollectionName);

    public async Task<UserProfile?> FindProfileAsync(string userId, CancellationToken cancellationToken)
    {
        BsonDocument? document = await FindAsync(userId, cancellationToken);
        return document is null ? null : MapProfile(document);
    }

    public async Task<UserProfile?> UpdateProfileAsync(string userId, string? name, string? email, CancellationToken cancellationToken)
    {
        List<UpdateDefinition<BsonDocument>> updates = [];
        AddStringUpdate(updates, "name", name);
        AddStringUpdate(updates, "email", email);
        BsonDocument? document = await FindAfterUpdateAsync(
            BuildIdFilter(userId), updates, cancellationToken);
        return document is null ? null : MapProfile(document);
    }

    public async Task<IReadOnlyList<UserAddress>?> AddAddressAsync(string userId, UserAddress address, CancellationToken cancellationToken)
    {
        ObjectId addressId = ObjectId.TryParse(address.Id, out ObjectId parsed)
            ? parsed
            : ObjectId.GenerateNewId();
        BsonDocument value = new()
        {
            ["_id"] = addressId,
            ["label"] = address.Label ?? "Công trình",
            ["receiverName"] = address.ReceiverName is null ? BsonNull.Value : address.ReceiverName,
            ["receiverPhone"] = address.ReceiverPhone is null ? BsonNull.Value : address.ReceiverPhone,
            ["addressDetail"] = address.AddressDetail is null ? BsonNull.Value : address.AddressDetail,
        };
        MongoUserArrayMutationResult result = await MutateArrayAsync(
            userId,
            "addresses",
            addresses =>
            {
                BsonDocument added = value.DeepClone().AsBsonDocument;
                added["isDefault"] = addresses.Count == 0;
                addresses.Add(added);
                return true;
            },
            cancellationToken);
        return result.Status == MongoUserArrayMutationStatus.UserNotFound
            ? null
            : MapAddresses(result.Document!);
    }

    public async Task<IReadOnlyList<UserAddress>?> UpdateAddressAsync(string userId, string addressId, UserAddressPatch patch, CancellationToken cancellationToken)
    {
        MongoUserArrayMutationResult result = await MutateArrayAsync(
            userId,
            "addresses",
            addresses =>
            {
                BsonDocument? address = FindSubdocument(addresses, addressId);
                if (address is null) return false;
                if (patch.Label is not null) SetOrRemove(address, "label", patch.Label);
                if (patch.ReceiverName is not null) SetOrRemove(address, "receiverName", patch.ReceiverName);
                if (patch.ReceiverPhone is not null) SetOrRemove(address, "receiverPhone", patch.ReceiverPhone);
                if (patch.AddressDetail is not null) SetOrRemove(address, "addressDetail", patch.AddressDetail);
                return true;
            },
            cancellationToken);
        return AddressMutationResponse(result);
    }

    public async Task<IReadOnlyList<UserAddress>?> DeleteAddressAsync(string userId, string addressId, CancellationToken cancellationToken)
    {
        MongoUserArrayMutationResult result = await MutateArrayAsync(
            userId,
            "addresses",
            addresses =>
            {
                int index = FindSubdocumentIndex(addresses, addressId);
                if (index < 0) return false;
                bool wasDefault = ReadBool(addresses[index].AsBsonDocument, "isDefault");
                addresses.RemoveAt(index);
                if (wasDefault && addresses.Count > 0) addresses[0].AsBsonDocument["isDefault"] = true;
                return true;
            },
            cancellationToken);
        return AddressMutationResponse(result);
    }

    public async Task<IReadOnlyList<UserAddress>?> SetDefaultAddressAsync(string userId, string addressId, CancellationToken cancellationToken)
    {
        MongoUserArrayMutationResult result = await MutateArrayAsync(
            userId,
            "addresses",
            addresses =>
            {
                int index = FindSubdocumentIndex(addresses, addressId);
                if (index < 0) return false;
                for (int i = 0; i < addresses.Count; i++) addresses[i].AsBsonDocument["isDefault"] = i == index;
                return true;
            },
            cancellationToken);
        return AddressMutationResponse(result);
    }

    public async Task<IReadOnlyList<UserOrderTemplate>?> GetOrderTemplatesAsync(string userId, CancellationToken cancellationToken)
    {
        BsonDocument? document = await FindAsync(userId, cancellationToken);
        return document is null ? null : MapTemplates(document);
    }

    public async Task<UserOrderTemplate?> AddOrderTemplateAsync(string userId, string? displayName, IReadOnlyList<UserTemplateProduct> products, CancellationToken cancellationToken)
    {
        ObjectId templateId = ObjectId.GenerateNewId();
        BsonDocument value = new()
        {
            ["_id"] = templateId,
            ["displayName"] = displayName is null ? BsonNull.Value : displayName,
            ["note"] = string.Empty,
            ["products"] = ToProductArray(products),
        };
        MongoUserArrayMutationResult result = await MutateArrayAsync(
            userId,
            "orderTemplate",
            templates =>
            {
                templates.Add(value.DeepClone());
                return true;
            },
            cancellationToken);
        return result.Status == MongoUserArrayMutationStatus.UserNotFound ? null : MapTemplate(value);
    }

    public async Task<UserOrderTemplate?> UpdateOrderTemplateAsync(string userId, int index, string? displayName, IReadOnlyList<UserTemplateProduct>? products, CancellationToken cancellationToken)
    {
        MongoUserArrayTarget? target = null;
        BsonDocument? updatedTemplate = null;
        MongoUserArrayMutationResult result = await MutateArrayAsync(
            userId,
            "orderTemplate",
            templates =>
            {
                if (target is null && !MongoUserArrayTarget.TryCreate(templates, index, out target)) return false;
                BsonDocument? value = target?.Find(templates);
                if (value is null) return false;
                if (displayName is not null) SetOrRemove(value, "displayName", displayName);
                if (products is not null) value["products"] = ToProductArray(products);
                updatedTemplate = value.DeepClone().AsBsonDocument;
                return true;
            },
            cancellationToken);
        return result.Status == MongoUserArrayMutationStatus.Updated && updatedTemplate is not null
            ? MapTemplate(updatedTemplate)
            : null;
    }

    public async Task<bool> DeleteOrderTemplateAsync(string userId, int index, CancellationToken cancellationToken)
    {
        MongoUserArrayTarget? target = null;
        MongoUserArrayMutationResult result = await MutateArrayAsync(
            userId,
            "orderTemplate",
            templates =>
            {
                if (target is null && !MongoUserArrayTarget.TryCreate(templates, index, out target)) return false;
                BsonDocument? value = target?.Find(templates);
                if (value is null) return false;
                templates.Remove(value);
                return true;
            },
            cancellationToken);
        return result.Status == MongoUserArrayMutationStatus.Updated;
    }

    public async Task<IReadOnlyList<UserSummary>> ListUsersAsync(string viewerRole, bool customersOnly, CancellationToken cancellationToken)
    {
        string[] roles = customersOnly ? ["customer"] : VisibleRoles(viewerRole);
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.In("role", roles);
        List<BsonDocument> documents = await users.Find(filter).ToListAsync(cancellationToken);
        return documents.Select(MapSummary).ToArray();
    }

    public async Task<UserSummary?> FindUserSummaryAsync(string userId, CancellationToken cancellationToken)
    {
        BsonDocument? document = await FindAsync(userId, cancellationToken);
        return document is null ? null : MapSummary(document);
    }

    public async Task<UserSummary?> FindUserSummaryByPhoneAsync(string phone, CancellationToken cancellationToken)
    {
        BsonDocument? document = await users.Find(Builders<BsonDocument>.Filter.Eq("phone", phone)).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : MapSummary(document);
    }

    public async Task<bool> HasOtherUserWithRoleAsync(string role, string? excludingUserId, CancellationToken cancellationToken)
    {
        FilterDefinitionBuilder<BsonDocument> builder = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = builder.Eq("role", role);
        if (!string.IsNullOrWhiteSpace(excludingUserId)) filter &= builder.Not(BuildIdFilter(excludingUserId));
        return await users.Find(filter).Limit(1).AnyAsync(cancellationToken);
    }

    public async Task<UserSummary?> CreateUserAsync(NewUserData user, CancellationToken cancellationToken)
    {
        ObjectId id = ObjectId.GenerateNewId();
        BsonDocument document = new()
        {
            ["_id"] = id,
            ["email"] = user.Email is null ? BsonNull.Value : user.Email,
            ["phone"] = user.Phone,
            ["name"] = user.Name is null ? BsonNull.Value : user.Name,
            ["password"] = user.PasswordHash,
            ["role"] = user.Role,
            ["functions"] = new BsonArray(),
            ["permissions"] = new BsonArray(user.Permissions.Select(static item => (BsonValue)item)),
            ["orderTemplate"] = new BsonArray(),
            ["station"] = new BsonArray((user.Stations ?? []).Select(static item => (BsonValue)item)),
            ["addresses"] = new BsonArray(),
            ["logInString"] = user.LoginToken,
        };
        await users.InsertOneAsync(document, cancellationToken: cancellationToken);
        return MapSummary(document);
    }

    public async Task<UserSummary?> UpdateUserAsync(string userId, string expectedRole, UserUpdateData update, CancellationToken cancellationToken)
    {
        List<UpdateDefinition<BsonDocument>> updates = [];
        AddStringUpdate(updates, "name", update.Name);
        AddStringUpdate(updates, "email", update.Email);
        AddStringUpdate(updates, "phone", update.Phone);
        BsonDocument? document = await FindAfterUpdateAsync(
            Builders<BsonDocument>.Filter.And(BuildIdFilter(userId), ExpectedRoleFilter(expectedRole)),
            updates,
            cancellationToken);
        return document is null ? null : MapSummary(document);
    }

    public async Task<UserSummary?> UpdatePermissionsAsync(string userId, string expectedRole, UserPermissionUpdate update, CancellationToken cancellationToken)
    {
        List<UpdateDefinition<BsonDocument>> updates = [];
        UpdateDefinitionBuilder<BsonDocument> builder = Builders<BsonDocument>.Update;
        if (update.Role is not null) updates.Add(builder.Set("role", update.Role));
        if (update.Permissions is not null)
            updates.Add(builder.Set("permissions", new BsonArray(update.Permissions.Select(static item => (BsonValue)item))));
        if (update.Role is not null || update.Permissions is not null)
            updates.Add(builder.Set("functions", new BsonArray()));
        AddStringUpdate(updates, "name", update.Name);
        AddStringUpdate(updates, "email", update.Email);
        AddStringUpdate(updates, "phone", update.Phone);
        if (update.PasswordHash is not null) updates.Add(builder.Set("password", update.PasswordHash));
        if (update.LoginToken is not null) updates.Add(builder.Set("logInString", update.LoginToken));
        if (update.PasswordChangedAt.HasValue)
            updates.Add(builder.Set("passwordChangedAt", update.PasswordChangedAt.Value.UtcDateTime));
        BsonDocument? document = await FindAfterUpdateAsync(
            Builders<BsonDocument>.Filter.And(BuildIdFilter(userId), ExpectedRoleFilter(expectedRole)),
            updates,
            cancellationToken);
        return document is null ? null : MapSummary(document);
    }

    public async Task<string?> RotateAutologinTokenAsync(string userId, string expectedRole, CancellationToken cancellationToken)
    {
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        UpdateResult result = await users.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(BuildIdFilter(userId), ExpectedRoleFilter(expectedRole)),
            Builders<BsonDocument>.Update.Set("logInString", token),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1 ? token : null;
    }

    public async Task<UserSummary?> AddStationAsync(string userId, string expectedRole, string stationId, CancellationToken cancellationToken)
    {
        BsonDocument? document = await users.FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.And(BuildIdFilter(userId), ExpectedRoleFilter(expectedRole)),
            Builders<BsonDocument>.Update.AddToSet("station", stationId),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return document is null ? null : MapSummary(document);
    }

    public async Task<IReadOnlyList<string>?> ReplaceStationsByPhoneAsync(string phone, string expectedRole, IReadOnlyList<string> stations, CancellationToken cancellationToken)
    {
        UpdateResult result = await users.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("phone", phone),
                ExpectedRoleFilter(expectedRole)),
            Builders<BsonDocument>.Update.Set("station", new BsonArray(stations.Select(static item => (BsonValue)item))),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1 ? stations.ToArray() : null;
    }

    public async Task<bool> DeleteUserAsync(string userId, string expectedRole, CancellationToken cancellationToken) =>
        (await users.DeleteOneAsync(Builders<BsonDocument>.Filter.And(BuildIdFilter(userId), ExpectedRoleFilter(expectedRole)), cancellationToken)).DeletedCount > 0;

    public async Task<UserPasswordRecord?> FindPasswordAsync(string userId, CancellationToken cancellationToken)
    {
        BsonDocument? document = await FindAsync(userId, cancellationToken);
        string? passwordHash = document is null ? null : ReadString(document, "password");
        return document is null || string.IsNullOrWhiteSpace(passwordHash)
            ? null
            : new UserPasswordRecord(ReadId(document), passwordHash);
    }

    public async Task<bool> ReplacePasswordAsync(
        string userId,
        string passwordHash,
        string loginToken,
        DateTimeOffset passwordChangedAt,
        CancellationToken cancellationToken)
    {
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set("password", passwordHash)
            .Set("logInString", loginToken)
            .Set("passwordChangedAt", passwordChangedAt.UtcDateTime);
        UpdateResult result = await users.UpdateOneAsync(BuildIdFilter(userId), update, cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    private async Task<BsonDocument?> FindAsync(string id, CancellationToken cancellationToken) =>
        await users.Find(BuildIdFilter(id)).Limit(1).FirstOrDefaultAsync(cancellationToken);

    private async Task<BsonDocument?> FindAfterUpdateAsync(
        FilterDefinition<BsonDocument> filter,
        List<UpdateDefinition<BsonDocument>> updates,
        CancellationToken cancellationToken)
    {
        if (updates.Count == 0)
            return await users.Find(filter).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return await users.FindOneAndUpdateAsync(
            filter,
            Builders<BsonDocument>.Update.Combine(updates),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    private Task<MongoUserArrayMutationResult> MutateArrayAsync(
        string userId,
        string field,
        Func<BsonArray, bool> mutate,
        CancellationToken cancellationToken) =>
        MongoUserArrayCompareExchange.ExecuteAsync(
            field,
            token => FindAsync(userId, token),
            (source, value, token) => TryUpdateArrayAsync(source, field, value, token),
            mutate,
            cancellationToken);

    private async Task<BsonDocument?> TryUpdateArrayAsync(
        BsonDocument source,
        string field,
        BsonArray value,
        CancellationToken cancellationToken)
    {
        FilterDefinitionBuilder<BsonDocument> builder = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> snapshot = source.TryGetValue(field, out BsonValue sourceValue)
            ? builder.Eq(field, sourceValue)
            : builder.Exists(field, false);
        bool hasLegacyVersion = source.TryGetValue("__v", out BsonValue version) && version.IsNumeric;
        if (hasLegacyVersion)
        {
            snapshot &= builder.Eq("__v", version);
        }

        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update.Set(field, value);
        if (hasLegacyVersion)
        {
            update = Builders<BsonDocument>.Update.Combine(
                update,
                Builders<BsonDocument>.Update.Inc("__v", 1));
        }

        return await users.FindOneAndUpdateAsync(
            builder.And(builder.Eq("_id", source["_id"]), snapshot),
            update,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    private static FilterDefinition<BsonDocument> BuildIdFilter(string id)
    {
        FilterDefinitionBuilder<BsonDocument> builder = Builders<BsonDocument>.Filter;
        if (ObjectId.TryParse(id, out ObjectId objectId)) return builder.Or(builder.Eq("_id", objectId), builder.Eq("_id", id));
        return builder.Eq("_id", id);
    }

    private static FilterDefinition<BsonDocument> ExpectedRoleFilter(string expectedRole)
    {
        FilterDefinitionBuilder<BsonDocument> builder = Builders<BsonDocument>.Filter;
        return expectedRole == "customer"
            ? builder.Or(builder.Eq("role", expectedRole), builder.Exists("role", false), builder.Eq("role", BsonNull.Value))
            : builder.Eq("role", expectedRole);
    }

    private static string[] VisibleRoles(string viewerRole) => viewerRole switch
    {
        "superadmin" => ["customer", "staff", "admin", "superadmin"],
        "admin" => ["customer", "staff", "admin"],
        "staff" => ["customer", "staff"],
        _ => [],
    };

    private static UserProfile MapProfile(BsonDocument d) => new(ReadId(d), ReadString(d, "email"), ReadString(d, "phone") ?? string.Empty,
        ReadString(d, "name"), ReadString(d, "role") ?? "customer", ReadStrings(d, "functions"), ReadStrings(d, "permissions"),
        ReadStrings(d, "station"), MapAddresses(d), MapTemplates(d));

    private static UserSummary MapSummary(BsonDocument d)
    {
        UserProfile profile = MapProfile(d);
        return new UserSummary(profile.Id, profile.Email, profile.Phone, profile.Name, profile.Role, profile.Functions,
            profile.Permissions, profile.Stations, profile.Addresses, profile.OrderTemplates);
    }

    private static UserAddress[] MapAddresses(BsonDocument d) =>
        ReadArray(d, "addresses").Where(static v => v.IsBsonDocument).Select(static v =>
        {
            BsonDocument a = v.AsBsonDocument;
            return new UserAddress(ReadId(a), ReadString(a, "label"), ReadString(a, "receiverName"), ReadString(a, "receiverPhone"), ReadString(a, "addressDetail"), ReadBool(a, "isDefault"));
        }).ToArray();

    private static UserAddress[]? AddressMutationResponse(MongoUserArrayMutationResult result) =>
        result.Status switch
        {
            MongoUserArrayMutationStatus.UserNotFound => null,
            MongoUserArrayMutationStatus.ItemNotFound => [],
            _ => MapAddresses(result.Document!),
        };

    private static UserOrderTemplate[] MapTemplates(BsonDocument d) =>
        ReadArray(d, "orderTemplate").Where(static v => v.IsBsonDocument).Select(MapTemplate).ToArray();

    private static UserOrderTemplate MapTemplate(BsonValue value) => MapTemplate(value.AsBsonDocument);

    private static UserOrderTemplate MapTemplate(BsonDocument t) => new(ReadId(t), ReadString(t, "displayName"), ReadString(t, "note"),
        ReadArray(t, "products").Where(static v => v.IsBsonDocument).Select(static v => new UserTemplateProduct(ReadString(v.AsBsonDocument, "productId"), ReadDouble(v.AsBsonDocument, "quantity", 1))).ToArray());

    private static BsonArray ToProductArray(IEnumerable<UserTemplateProduct> products) => new(products.Select(static p => new BsonDocument
    {
        ["productId"] = p.ProductId is null ? BsonNull.Value : p.ProductId,
        ["quantity"] = p.Quantity,
    }));

    private static BsonDocument? FindSubdocument(BsonArray values, string id) => values.FirstOrDefault(v => v.IsBsonDocument && ReadId(v.AsBsonDocument) == id)?.AsBsonDocument;
    private static int FindSubdocumentIndex(BsonArray values, string id)
    {
        for (int i = 0; i < values.Count; i++) if (values[i].IsBsonDocument && ReadId(values[i].AsBsonDocument) == id) return i;
        return -1;
    }

    private static void SetOrRemove(BsonDocument document, string field, string value)
    {
        if (value.Length == 0) document.Remove(field); else document[field] = value;
    }

    private static void AddStringUpdate(
        List<UpdateDefinition<BsonDocument>> updates,
        string field,
        string? value)
    {
        if (value is null) return;
        UpdateDefinitionBuilder<BsonDocument> builder = Builders<BsonDocument>.Update;
        updates.Add(value.Length == 0 ? builder.Unset(field) : builder.Set(field, value));
    }

    private static BsonArray ReadArray(BsonDocument d, string field) => d.TryGetValue(field, out BsonValue value) && value.IsBsonArray ? value.AsBsonArray : [];
    private static string ReadId(BsonDocument d) =>
        d.TryGetValue("_id", out BsonValue value) && !value.IsBsonNull
            ? value.ToString() ?? string.Empty
            : string.Empty;
    private static string? ReadString(BsonDocument d, string field) => d.TryGetValue(field, out BsonValue value) && !value.IsBsonNull ? value.IsString ? value.AsString : value.ToString() : null;
    private static bool ReadBool(BsonDocument d, string field) => d.TryGetValue(field, out BsonValue value) && value.IsBoolean && value.AsBoolean;
    private static double ReadDouble(BsonDocument d, string field, double fallback) => d.TryGetValue(field, out BsonValue value) && value.IsNumeric ? value.ToDouble() : fallback;
    private static string[] ReadStrings(BsonDocument d, string field) => ReadArray(d, field).Where(static v => v.IsString).Select(static v => v.AsString).ToArray();
}
