using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Users;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Security;
using TTSmartEcom.Infrastructure.MongoDb.Health;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Mappings;

namespace TTSmartEcom.Infrastructure.MongoDb;

public static class DependencyInjection
{
    public static IServiceCollection AddMongoInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        LegacyMongoClassMaps.Register();
        MongoDbOptions options = MongoDbOptionsResolver.Resolve(configuration);
        services.AddSingleton(options);
        services.AddSingleton<IMongoClient>(_ => new MongoClient(options.ConnectionString));
        services.AddSingleton<IMongoDatabaseProvider, MongoDatabaseProvider>();
        services.AddSingleton<IPasswordHashCompatibilityVerifier, PasswordHashCompatibilityVerifier>();
        services.AddSingleton<IUserIdentityReader, MongoUserIdentityReader>();
        services.AddSingleton<IUserRepository, MongoUserRepository>();
        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb", tags: ["ready"]);

        return services;
    }
}
