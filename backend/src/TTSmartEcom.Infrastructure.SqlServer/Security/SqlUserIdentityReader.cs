using System.Text.Json;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Infrastructure.SqlServer;

namespace TTSmartEcom.Infrastructure.SqlServer.Security;

public sealed class SqlUserIdentityReader(ISqlConnectionFactory factory) : IUserIdentityReader
{
    public async Task<UserIdentitySnapshot?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create(); await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT PublicId,Email,Phone,Name,Role,FunctionsJson,PermissionsJson FROM dbo.Users WHERE PublicId=@id AND IsDeleted=0;", connection);
        command.Parameters.AddWithValue("@id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new UserIdentitySnapshot(reader.GetString(0), reader.IsDBNull(1)?null:reader.GetString(1), reader.IsDBNull(2)?string.Empty:reader.GetString(2), reader.IsDBNull(3)?null:reader.GetString(3), reader.IsDBNull(4)?"customer":reader.GetString(4), Read(reader,5), Read(reader,6), null, []);
    }
    private static string[] Read(SqlDataReader reader,int ordinal) => reader.IsDBNull(ordinal) ? [] : JsonSerializer.Deserialize<string[]>(reader.GetString(ordinal)) ?? [];
}
