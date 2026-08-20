using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Users;

namespace TTSmartEcom.Infrastructure.SqlServer.Users;

public sealed class SqlPasswordHashWriter : IPasswordHashWriter
{
    public string Hash(string password) => global::BCrypt.Net.BCrypt.HashPassword(password, 10);
}

public sealed class SqlSuperAdminMutationGuard(ISqlConnectionFactory factory) : ISuperAdminMutationGuard
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
    {
        SqlConnection connection = factory.Create();
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using SqlCommand command = new("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'TTSmart.SuperAdminMutation',@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=0; SELECT @result;", connection);
            int result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
            if (result < 0) { await connection.DisposeAsync(); return null; }
            return new Handle(connection);
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    private sealed class Handle(SqlConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { await using SqlCommand command=new("EXEC sys.sp_releaseapplock @Resource=N'TTSmart.SuperAdminMutation',@LockOwner=N'Session';",connection);await command.ExecuteNonQueryAsync(); }
            finally { await connection.DisposeAsync(); }
        }
    }
}
