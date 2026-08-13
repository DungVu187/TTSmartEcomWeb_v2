using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;

namespace TTSmartEcom.Infrastructure.MongoDb.Health;

public sealed class MongoHealthCheck(IMongoDatabaseProvider databaseProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await databaseProvider.Database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("MongoDB is unavailable.");
        }
    }
}
