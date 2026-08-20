using MongoDB.Bson;
using MongoDB.Driver;

namespace TTSmartEcom.IntegrationTests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MongoAvailableFactAttribute : FactAttribute
{
    public MongoAvailableFactAttribute()
    {
        if (!IsLocalMongoAvailable())
        {
            Skip = "MongoDB local không khả dụng; integration Mongo được bỏ qua thay vì làm hỏng bộ test runtime SQL.";
        }
    }

    private static bool IsLocalMongoAvailable()
    {
        try
        {
            MongoClientSettings settings = MongoClientSettings.FromConnectionString(
                "mongodb://127.0.0.1:27017/?serverSelectionTimeoutMS=500");
            new MongoClient(settings).GetDatabase("admin")
                .RunCommand<BsonDocument>(new BsonDocument("ping", 1));
            return true;
        }
        catch (MongoException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
