using MongoDB.Bson;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Users;

namespace TTSmartEcom.UnitTests.Users;

public sealed class MongoUserArrayCompareExchangeTests
{
    [Fact]
    public async Task ExecuteAsync_WhenAddressAppendConflicts_PreservesConcurrentAddressAndDefaultInvariant()
    {
        BsonDocument state = User("addresses", []);
        int writes = 0;

        MongoUserArrayMutationResult result = await MongoUserArrayCompareExchange.ExecuteAsync(
            "addresses",
            _ => Task.FromResult<BsonDocument?>(state.DeepClone().AsBsonDocument),
            (_, proposed, _) =>
            {
                writes++;
                if (writes == 1)
                {
                    state["addresses"] = new BsonArray([Address("concurrent", true)]);
                    return Task.FromResult<BsonDocument?>(null);
                }

                state["addresses"] = proposed.DeepClone();
                return Task.FromResult<BsonDocument?>(state.DeepClone().AsBsonDocument);
            },
            addresses =>
            {
                addresses.Add(Address("request", addresses.Count == 0));
                return true;
            },
            CancellationToken.None);

        Assert.Equal(MongoUserArrayMutationStatus.Updated, result.Status);
        Assert.Equal(2, writes);
        BsonArray addresses = result.Document!["addresses"].AsBsonArray;
        Assert.Equal(["concurrent", "request"], addresses.Select(Id));
        Assert.Single(addresses, value => value["isDefault"].AsBoolean);
        Assert.True(addresses[0]["isDefault"].AsBoolean);
        Assert.False(addresses[1]["isDefault"].AsBoolean);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateIndexShifts_RetriesAgainstOriginalTemplateId()
    {
        BsonDocument first = Template("first", "Mẫu đầu");
        BsonDocument selected = Template("selected", "Mẫu cần sửa");
        BsonDocument state = User("orderTemplate", [first, selected]);
        MongoUserArrayTarget? target = null;
        int writes = 0;

        MongoUserArrayMutationResult result = await MongoUserArrayCompareExchange.ExecuteAsync(
            "orderTemplate",
            _ => Task.FromResult<BsonDocument?>(state.DeepClone().AsBsonDocument),
            (_, proposed, _) =>
            {
                writes++;
                if (writes == 1)
                {
                    state["orderTemplate"] = new BsonArray([selected.DeepClone()]);
                    return Task.FromResult<BsonDocument?>(null);
                }

                state["orderTemplate"] = proposed.DeepClone();
                return Task.FromResult<BsonDocument?>(state.DeepClone().AsBsonDocument);
            },
            templates =>
            {
                if (target is null && !MongoUserArrayTarget.TryCreate(templates, 1, out target)) return false;
                BsonDocument? template = target?.Find(templates);
                if (template is null) return false;
                template["displayName"] = "Đã cập nhật";
                return true;
            },
            CancellationToken.None);

        Assert.Equal(MongoUserArrayMutationStatus.Updated, result.Status);
        Assert.Equal(2, writes);
        BsonDocument template = Assert.Single(result.Document!["orderTemplate"].AsBsonArray).AsBsonDocument;
        Assert.Equal("selected", Id(template));
        Assert.Equal("Đã cập nhật", template["displayName"].AsString);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTargetDisappearsDuringRetry_ReturnsItemNotFoundWithoutOverwritingArray()
    {
        BsonDocument selected = Template("selected", "Mẫu cần xóa");
        BsonDocument state = User("orderTemplate", [selected]);
        MongoUserArrayTarget? target = null;
        int writes = 0;

        MongoUserArrayMutationResult result = await MongoUserArrayCompareExchange.ExecuteAsync(
            "orderTemplate",
            _ => Task.FromResult<BsonDocument?>(state.DeepClone().AsBsonDocument),
            (_, _, _) =>
            {
                writes++;
                state["orderTemplate"] = new BsonArray();
                return Task.FromResult<BsonDocument?>(null);
            },
            templates =>
            {
                if (target is null && !MongoUserArrayTarget.TryCreate(templates, 0, out target)) return false;
                BsonDocument? template = target?.Find(templates);
                if (template is null) return false;
                templates.Remove(template);
                return true;
            },
            CancellationToken.None);

        Assert.Equal(MongoUserArrayMutationStatus.ItemNotFound, result.Status);
        Assert.Equal(1, writes);
        Assert.Empty(state["orderTemplate"].AsBsonArray);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLegacyTemplateHasNoId_ReturnsTheUpdatedWinningSubdocument()
    {
        BsonDocument state = User("orderTemplate", [TemplateWithoutId("Mẫu cũ")]);
        MongoUserArrayTarget? target = null;
        BsonDocument? updatedTemplate = null;

        MongoUserArrayMutationResult result = await MongoUserArrayCompareExchange.ExecuteAsync(
            "orderTemplate",
            _ => Task.FromResult<BsonDocument?>(state.DeepClone().AsBsonDocument),
            (_, proposed, _) =>
            {
                state["orderTemplate"] = proposed.DeepClone();
                return Task.FromResult<BsonDocument?>(state.DeepClone().AsBsonDocument);
            },
            templates =>
            {
                if (target is null && !MongoUserArrayTarget.TryCreate(templates, 0, out target)) return false;
                BsonDocument? template = target?.Find(templates);
                if (template is null) return false;
                template["displayName"] = "Đã cập nhật";
                updatedTemplate = template.DeepClone().AsBsonDocument;
                return true;
            },
            CancellationToken.None);

        Assert.Equal(MongoUserArrayMutationStatus.Updated, result.Status);
        Assert.NotNull(updatedTemplate);
        Assert.Equal("Đã cập nhật", updatedTemplate!["displayName"].AsString);
        Assert.Equal("Đã cập nhật", state["orderTemplate"][0]["displayName"].AsString);
    }

    private static BsonDocument User(string field, BsonArray values) => new()
    {
        ["_id"] = "user-1",
        [field] = values,
    };

    private static BsonDocument Address(string id, bool isDefault) => new()
    {
        ["_id"] = id,
        ["isDefault"] = isDefault,
    };

    private static BsonDocument Template(string id, string displayName) => new()
    {
        ["_id"] = id,
        ["displayName"] = displayName,
        ["products"] = new BsonArray(),
    };

    private static BsonDocument TemplateWithoutId(string displayName) => new()
    {
        ["displayName"] = displayName,
        ["products"] = new BsonArray(),
    };

    private static string Id(BsonValue value) => Id(value.AsBsonDocument);

    private static string Id(BsonDocument value) => value["_id"].AsString;
}
