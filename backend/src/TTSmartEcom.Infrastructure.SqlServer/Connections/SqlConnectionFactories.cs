using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace TTSmartEcom.Infrastructure.SqlServer;

public interface IControlDbConnectionFactory
{
    SqlConnection Create();
}

public interface IOperationalDbConnectionFactory
{
    SqlConnection Create();
}

public interface ICompanyDbConnectionFactory
{
    SqlConnection Create();
}

public interface ISqlConnectionFactory
{
    SqlConnection Create();
}

public sealed class ControlDbConnectionFactory(IOptions<SqlServerOptions> options) : IControlDbConnectionFactory
{
    public SqlConnection Create()
    {
        string connStr = options.Value.GetControlConnectionString();
        var builder = new SqlConnectionStringBuilder(connStr)
        {
            MultipleActiveResultSets = true,
        };
        return new SqlConnection(builder.ConnectionString);
    }
}

public sealed class OperationalDbConnectionFactory(IOptions<SqlServerOptions> options) : IOperationalDbConnectionFactory
{
    public SqlConnection Create()
    {
        string connStr = options.Value.GetOperationalConnectionString();
        var builder = new SqlConnectionStringBuilder(connStr)
        {
            MultipleActiveResultSets = true,
        };
        return new SqlConnection(builder.ConnectionString);
    }
}

public sealed class CompanyDbConnectionFactory(IOptions<SqlServerOptions> options) : ICompanyDbConnectionFactory
{
    public SqlConnection Create()
    {
        string connStr = options.Value.GetCompanyConnectionString();
        var builder = new SqlConnectionStringBuilder(connStr)
        {
            MultipleActiveResultSets = true,
        };
        return new SqlConnection(builder.ConnectionString);
    }
}

public sealed class DefaultSqlConnectionFactory(IOperationalDbConnectionFactory operationalFactory) : ISqlConnectionFactory
{
    public SqlConnection Create() => operationalFactory.Create();
}
