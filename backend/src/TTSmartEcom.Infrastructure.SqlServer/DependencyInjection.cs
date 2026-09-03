using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Application.Security;
using TTSmartEcom.Infrastructure.SqlServer.Security;

namespace TTSmartEcom.Infrastructure.SqlServer;

public sealed class SqlServerOptions
{
    public const string SectionName = "SqlServer";

    /// <summary>
    /// Legacy or default operational connection string.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Explicit connection string for Operational DB (e.g. [TTSmart]).
    /// </summary>
    public string? OperationalConnectionString { get; init; }

    /// <summary>
    /// Explicit connection string for Company Shared DB (e.g. [TTSmart]).
    /// </summary>
    public string? CompanyConnectionString { get; init; }

    /// <summary>
    /// Explicit connection string for Control Plane DB (e.g. [ttsmart.com.vn]).
    /// </summary>
    public string? ControlConnectionString { get; init; }

    public string GetOperationalConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(OperationalConnectionString))
        {
            return OperationalConnectionString;
        }

        return ConnectionString;
    }

    public string GetControlConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(ControlConnectionString))
        {
            return ControlConnectionString;
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return string.Empty;
        }

        // Derive control connection string by replacing Initial Catalog with ttsmart.com.vn
        var builder = new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = "ttsmart.com.vn",
        };
        return builder.ConnectionString;
    }

    public string GetCompanyConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(CompanyConnectionString))
        {
            return CompanyConnectionString;
        }

        return ConnectionString;
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddSqlServerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SqlServerOptions>()
            .Bind(configuration.GetSection(SqlServerOptions.SectionName))
            .Validate(options =>
            {
                string opConn = options.GetOperationalConnectionString();
                if (string.IsNullOrWhiteSpace(opConn))
                {
                    return false;
                }

                var opBuilder = new SqlConnectionStringBuilder(opConn);
                string opCatalog = opBuilder.InitialCatalog;
                bool isOpValid = opCatalog.Equals("TTSmart", StringComparison.OrdinalIgnoreCase)
                    || opCatalog.Equals("TTSmart_Operational_V1_Test", StringComparison.OrdinalIgnoreCase)
                    || opCatalog.EndsWith("_online", StringComparison.OrdinalIgnoreCase)
                    || (opCatalog.StartsWith("TTSmartEcomV2", StringComparison.OrdinalIgnoreCase)
                        && !opCatalog.StartsWith("TTSmartEcomV2ControlPlaneIntegration_", StringComparison.OrdinalIgnoreCase));

                if (!isOpValid)
                {
                    return false;
                }

                string companyConn = options.GetCompanyConnectionString();
                if (string.IsNullOrWhiteSpace(companyConn))
                {
                    return false;
                }

                var companyBuilder = new SqlConnectionStringBuilder(companyConn);
                string companyCatalog = companyBuilder.InitialCatalog;
                bool isCompanyValid = companyCatalog.Equals("TTSmart", StringComparison.OrdinalIgnoreCase)
                    || companyCatalog.Equals("TTSmart_Company_V1_Test", StringComparison.OrdinalIgnoreCase)
                    || companyCatalog.StartsWith("TTSmartEcomV2", StringComparison.OrdinalIgnoreCase);

                if (!isCompanyValid)
                {
                    return false;
                }

                string ctrlConn = options.GetControlConnectionString();
                if (string.IsNullOrWhiteSpace(ctrlConn))
                {
                    return false;
                }

                var ctrlBuilder = new SqlConnectionStringBuilder(ctrlConn);
                string ctrlCatalog = ctrlBuilder.InitialCatalog;
                bool isCtrlValid = ctrlCatalog.Equals("ttsmart.com.vn", StringComparison.OrdinalIgnoreCase)
                    || ctrlCatalog.Equals("TTSmart_Control_V1_Test", StringComparison.OrdinalIgnoreCase)
                    || ctrlCatalog.StartsWith("TTSmartEcomV2ControlPlaneIntegration_", StringComparison.OrdinalIgnoreCase);

                return isCtrlValid;
            }, "SqlServer configuration must define valid connection strings for Company, Operational, and Control Plane databases.")
            .ValidateOnStart();

        services.AddSingleton<IControlDbConnectionFactory, ControlDbConnectionFactory>();
        services.AddSingleton<ICompanyDbConnectionFactory, CompanyDbConnectionFactory>();
        services.AddSingleton<IOperationalDbConnectionFactory, OperationalDbConnectionFactory>();
        services.AddSingleton<ISqlConnectionFactory, DefaultSqlConnectionFactory>();

        services.AddSingleton<IControlPlaneIdentityReader, SqlControlPlaneIdentityReader>();
        services.AddScoped<IControlPlaneUserRepository, SqlControlPlaneUserRepository>();
        services.AddSingleton<IAccessScopeService, AccessScopeService>();
        services.AddScoped<ControlPlaneAuthenticationService>();

        return services;
    }
}
