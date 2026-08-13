using MongoDB.Driver;

namespace TTSmartEcom.Infrastructure.MongoDb.Configuration;

internal sealed class MongoDatabaseProvider(IMongoClient client, MongoDbOptions options) : IMongoDatabaseProvider
{
    public IMongoDatabase Database { get; } = client.GetDatabase(options.DatabaseName);
}
