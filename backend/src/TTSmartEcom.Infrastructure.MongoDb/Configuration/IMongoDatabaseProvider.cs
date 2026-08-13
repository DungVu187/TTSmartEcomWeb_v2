using MongoDB.Driver;

namespace TTSmartEcom.Infrastructure.MongoDb.Configuration;

public interface IMongoDatabaseProvider
{
    IMongoDatabase Database { get; }
}
