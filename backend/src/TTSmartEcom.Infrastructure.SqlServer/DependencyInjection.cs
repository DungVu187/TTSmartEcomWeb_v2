using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TTSmartEcom.Infrastructure.SqlServer;

public sealed class SqlServerOptions
{
    public const string SectionName = "SqlServer";
    public string ConnectionString { get; init; } = string.Empty;
}

public interface ISqlConnectionFactory { Microsoft.Data.SqlClient.SqlConnection Create(); }
internal sealed class SqlConnectionFactory(IOptions<SqlServerOptions> options) : ISqlConnectionFactory
{
    public Microsoft.Data.SqlClient.SqlConnection Create()
    {
        // Several legacy response shapes contain parent rows plus ordered child rows.
        // MARS lets repositories materialize those children without opening a second
        // connection inside the caller's ambient transaction.
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(options.Value.ConnectionString)
        {
            MultipleActiveResultSets = true,
        };
        return new(builder.ConnectionString);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddSqlServerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SqlServerOptions>().Bind(configuration.GetSection(SqlServerOptions.SectionName)).Validate(x =>
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(x.ConnectionString).InitialCatalog == "TTSmart", "SqlServer phải trỏ duy nhất [TTSmart].").ValidateOnStart();
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        return services;
    }
}
