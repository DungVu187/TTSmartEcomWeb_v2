using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace TTSmartEcom.Infrastructure.MongoDb.Configuration;

internal static class MongoDbOptionsResolver
{
    public static MongoDbOptions Resolve(IConfiguration configuration)
    {
        string? configuredConnection = configuration[$"{MongoDbOptions.SectionName}:ConnectionString"];
        string? legacyConnection = configuration["MONGODB_URI"];
        string? connectionString = FirstNonEmpty(configuredConnection, legacyConnection);

        string? configuredDatabase = configuration[$"{MongoDbOptions.SectionName}:DatabaseName"];
        string? legacyDatabase = configuration["DB_NAME"];
        string? databaseName = FirstNonEmpty(configuredDatabase, legacyDatabase);

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            MongoUrl url;
            try
            {
                url = new MongoUrl(connectionString);
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                throw new InvalidOperationException("MongoDB connection configuration is invalid.", exception);
            }

            databaseName ??= url.DatabaseName;
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("MongoDB database name is required.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(databaseName, "^[A-Za-z0-9_-]+$"))
        {
            throw new InvalidOperationException("MongoDB database name contains unsupported characters.");
        }

        connectionString ??= $"mongodb://localhost:27017/{databaseName}";

        return new MongoDbOptions
        {
            ConnectionString = connectionString,
            DatabaseName = databaseName,
        };
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
