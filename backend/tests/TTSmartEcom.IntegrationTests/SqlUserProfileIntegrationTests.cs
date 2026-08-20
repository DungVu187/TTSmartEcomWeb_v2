using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Users;
using TTSmartEcom.Domain.Users;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Users;
using Xunit.Sdk;

namespace TTSmartEcom.IntegrationTests;

public sealed class SqlUserProfileIntegrationTests
{
    private const string UserId = "507f191e810c19729de860ea";

    [Fact]
    public async Task ProfileAddressesAndTemplates_PersistWithDefaultAddressInvariant()
    {
        string? configuredConnection = Environment.GetEnvironmentVariable("TTSMART_SQL_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            throw SkipException.ForSkip("Cần TTSMART_SQL_INTEGRATION_CONNECTION trỏ SQL Server local dành cho test cô lập.");
        }

        string databaseName = $"TTSmartEcomV2ProfileIntegration_{Guid.NewGuid():N}";
        SqlConnectionStringBuilder master = new(configuredConnection) { InitialCatalog = "master" };
        SqlConnectionStringBuilder test = new(configuredConnection) { InitialCatalog = databaseName };
        try
        {
            await ExecuteAsync(master.ConnectionString, $"CREATE DATABASE [{databaseName}];");
            await ExecuteAsync(test.ConnectionString, Schema);
            await ExecuteAsync(test.ConnectionString, $"""
                INSERT dbo.Users(UserId,PublicId,Email,Phone,Name,PasswordHash,Role,FunctionsJson,PermissionsJson,AddressesJson,OrderTemplatesJson,StationIdsJson,Version,IsDeleted)
                VALUES(NEWID(),N'{UserId}',N'old@example.test',N'0900000000',N'Tên cũ',N'hash',N'customer',N'[]',N'[]',N'[]',N'[]',N'[]',0,0);
                """);

            var repository = new SqlUserProfileRepository(new TestConnectionFactory(test.ConnectionString));
            UserProfile updatedProfile = Assert.IsType<UserProfile>(await repository.UpdateProfileAsync(UserId, "Tên mới", "new@example.test", CancellationToken.None));
            Assert.Equal("Tên mới", updatedProfile.Name);
            Assert.Equal("new@example.test", updatedProfile.Email);

            IReadOnlyList<UserAddress> firstAddresses = Assert.IsAssignableFrom<IReadOnlyList<UserAddress>>(await repository.AddAddressAsync(
                UserId, new UserAddress(string.Empty, "Nhà", "A", "0900000000", "Địa chỉ A", false), CancellationToken.None));
            UserAddress first = Assert.Single(firstAddresses);
            Assert.True(first.IsDefault);
            IReadOnlyList<UserAddress> secondAddresses = Assert.IsAssignableFrom<IReadOnlyList<UserAddress>>(await repository.AddAddressAsync(
                UserId, new UserAddress(string.Empty, "Công ty", "B", "0910000000", "Địa chỉ B", false), CancellationToken.None));
            UserAddress second = secondAddresses.Single(address => address.Id != first.Id);
            Assert.False(second.IsDefault);

            IReadOnlyList<UserAddress> switched = Assert.IsAssignableFrom<IReadOnlyList<UserAddress>>(await repository.SetDefaultAddressAsync(UserId, second.Id, CancellationToken.None));
            Assert.True(switched.Single(address => address.Id == second.Id).IsDefault);
            IReadOnlyList<UserAddress> afterDelete = Assert.IsAssignableFrom<IReadOnlyList<UserAddress>>(await repository.DeleteAddressAsync(UserId, second.Id, CancellationToken.None));
            Assert.True(Assert.Single(afterDelete).IsDefault);

            UserOrderTemplate template = Assert.IsType<UserOrderTemplate>(await repository.AddOrderTemplateAsync(
                UserId, "Mẫu A", [new UserTemplateProduct("507f191e810c19729de860eb", 2)], CancellationToken.None));
            Assert.Equal("Mẫu A", template.DisplayName);
            UserOrderTemplate edited = Assert.IsType<UserOrderTemplate>(await repository.UpdateOrderTemplateAsync(
                UserId, 0, "Mẫu B", null, CancellationToken.None));
            Assert.Equal("Mẫu B", edited.DisplayName);
            Assert.True(await repository.DeleteOrderTemplateAsync(UserId, 0, CancellationToken.None));

            UserProfile profile = Assert.IsType<UserProfile>(await repository.FindProfileAsync(UserId, CancellationToken.None));
            Assert.Equal("Tên mới", profile.Name);
            Assert.Single(profile.Addresses);
            Assert.Empty(profile.OrderTemplates);
        }
        finally
        {
            await ExecuteAsync(master.ConnectionString, $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END");
        }
    }

    private const string Schema = """
        CREATE TABLE dbo.Users (
            UserId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE,
            Email nvarchar(320) NULL, Phone nvarchar(50) NULL, Name nvarchar(200) NULL,
            PasswordHash nvarchar(500) NULL, Role nvarchar(80) NULL, FunctionsJson nvarchar(max) NULL,
            PermissionsJson nvarchar(max) NULL, AddressesJson nvarchar(max) NULL,
            OrderTemplatesJson nvarchar(max) NULL, StationIdsJson nvarchar(max) NULL,
            AutoLoginTokenHash char(64) NULL, PasswordChangedAtUtc datetime2(7) NULL,
            Version bigint NOT NULL, IsDeleted bit NOT NULL
        );
        """;

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TestConnectionFactory(string connectionString) : ISqlConnectionFactory
    {
        public SqlConnection Create() => new(new SqlConnectionStringBuilder(connectionString)
        {
            MultipleActiveResultSets = true,
        }.ConnectionString);
    }
}
