using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Users;
using TTSmartEcom.Domain.Users;

namespace TTSmartEcom.SecurityTests;

public sealed class UserAdministrationTargetRoleSecurityTests(
    UserAdministrationSecurityFactory factory) :
    IClassFixture<UserAdministrationSecurityFactory>
{
    private static readonly string[] ReplacementStations = ["507f1f77bcf86cd799439099"];

    [Theory]
    [InlineData("PUT", "/users/507f1f77bcf86cd799439012/permissions")]
    [InlineData("PUT", "/users/507f1f77bcf86cd799439012")]
    [InlineData("PUT", "/users/stations")]
    [InlineData("DELETE", "/users/507f1f77bcf86cd799439012")]
    [InlineData("POST", "/users/507f1f77bcf86cd799439012/stations")]
    [InlineData("POST", "/users/507f1f77bcf86cd799439012/rotate-autologin-token")]
    [InlineData("PUT", "/api/users/507f1f77bcf86cd799439012/permissions")]
    public async Task Admin_WithEndpointPermission_CannotMutatePeerAdmin(
        string method,
        string path)
    {
        using HttpRequestMessage request = UserAdministrationSecurityFactory.AuthenticatedRequest(
            new HttpMethod(method),
            path,
            method switch
            {
                "PUT" when path.EndsWith("/stations", StringComparison.Ordinal) =>
                    JsonContent.Create(new
                    {
                        phone = "0900000002",
                        stations = ReplacementStations,
                    }),
                "PUT" => JsonContent.Create(new { name = "Không được đổi" }),
                "POST" when path.EndsWith("/stations", StringComparison.Ordinal) =>
                    JsonContent.Create(new { stationId = "507f1f77bcf86cd799439099" }),
                "POST" => JsonContent.Create(new { }),
                _ => null,
            });

        using HttpResponseMessage response = await factory.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, factory.Users.MutationCalls);
    }

    [Fact]
    public async Task Admin_WithCreatePermission_CannotCreatePeerAdmin()
    {
        using HttpRequestMessage request = UserAdministrationSecurityFactory.AuthenticatedRequest(
            HttpMethod.Post,
            "/users/admin-create",
            JsonContent.Create(new
            {
                phone = "0900000001",
                password = "synthetic-password",
                role = "admin",
            }));

        using HttpResponseMessage response = await factory.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, factory.Users.MutationCalls);
    }

    [Fact]
    public async Task SuperAdmin_WhenMutationGuardIsHeld_CannotCreateSecondSuperAdmin()
    {
        using WebApplicationFactory<Program> guardedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUserIdentityReader>();
                services.RemoveAll<ISuperAdminMutationGuard>();
                services.AddSingleton<IUserIdentityReader>(new SuperAdminIdentityReader());
                services.AddSingleton<ISuperAdminMutationGuard>(new UnavailableSuperAdminGuard());
            }));
        using HttpClient client = guardedFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpRequestMessage request = UserAdministrationSecurityFactory.AuthenticatedRequest(
            HttpMethod.Post,
            "/users/admin-create",
            JsonContent.Create(new
            {
                phone = "0900000003",
                password = "synthetic-password",
                role = "superadmin",
            }));
        int mutationsBefore = factory.Users.MutationCalls;

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(mutationsBefore, factory.Users.MutationCalls);
    }

    private sealed class SuperAdminIdentityReader : IUserIdentityReader
    {
        public Task<UserIdentitySnapshot?> FindByIdAsync(
            string userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<UserIdentitySnapshot?>(new(
                userId,
                "superadmin@example.test",
                "0900000009",
                "Super Admin kiểm thử",
                "superadmin",
                [],
                [],
                null));
    }

    private sealed class UnavailableSuperAdminGuard : ISuperAdminMutationGuard
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IAsyncDisposable?>(null);
    }
}

public sealed class UserAdministrationSecurityFactory : WebApplicationFactory<Program>
{
    private const string JwtSecret = "security-test-user-role-secret-at-least-thirty-two-bytes";
    private const string ActorId = "507f1f77bcf86cd799439011";
    private const string TargetId = "507f1f77bcf86cd799439012";

    public UserAdministrationSecurityFactory()
    {
        Users = new TargetRoleRepository(TargetId);
        Client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public HttpClient Client { get; }
    public TargetRoleRepository Users { get; }

    public static HttpRequestMessage AuthenticatedRequest(
        HttpMethod method,
        string path,
        HttpContent? content)
    {
        HttpRequestMessage request = new(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
        return request;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = JwtSecret,
                ["JWT_SECRET"] = JwtSecret,
                ["LegacyCompatibility:AdminFullAccess"] = "false",
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserIdentityReader>();
            services.RemoveAll<IUserProfileRepository>();
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
            });
            services.AddSingleton<IUserIdentityReader>(new AdministrativeIdentityReader());
            services.AddSingleton<IUserProfileRepository>(Users);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Client.Dispose();
        base.Dispose(disposing);
    }

    private static string CreateToken()
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            claims:
            [
                new Claim("userId", ActorId),
                new Claim("role", "admin"),
                new Claim(
                    JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(now).ToUnixTimeSeconds().ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64),
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(15),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class AdministrativeIdentityReader : IUserIdentityReader
    {
        public Task<UserIdentitySnapshot?> FindByIdAsync(
            string userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<UserIdentitySnapshot?>(new(
                ActorId,
                "admin@example.test",
                "0900000000",
                "Quản trị kiểm thử",
                "admin",
                [],
                [
                    "account.manage",
                    "customer.create",
                    "customer.edit",
                    "customer.delete",
                    "customer.assign_station",
                ],
                null));
    }
}

public sealed class TargetRoleRepository(string targetId) : IUserProfileRepository
{
    private readonly UserSummary target = new(
        targetId,
        "peer-admin@example.test",
        "0900000002",
        "Quản trị ngang cấp",
        "admin",
        [],
        [],
        [],
        [],
        []);

    public int MutationCalls { get; private set; }

    public Task<UserSummary?> FindUserSummaryAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult<UserSummary?>(userId == target.Id ? target : null);

    public Task<UserSummary?> FindUserSummaryByPhoneAsync(string phone, CancellationToken cancellationToken) =>
        Task.FromResult<UserSummary?>(phone == target.Phone ? target : null);

    public Task<UserSummary?> CreateUserAsync(NewUserData user, CancellationToken cancellationToken)
    {
        MutationCalls++;
        return Task.FromResult<UserSummary?>(target);
    }

    public Task<UserSummary?> UpdateUserAsync(string userId, string expectedRole, UserUpdateData update, CancellationToken cancellationToken)
    {
        MutationCalls++;
        return Task.FromResult<UserSummary?>(target);
    }

    public Task<UserSummary?> UpdatePermissionsAsync(string userId, string expectedRole, UserPermissionUpdate update, CancellationToken cancellationToken)
    {
        MutationCalls++;
        return Task.FromResult<UserSummary?>(target);
    }

    public Task<string?> RotateAutologinTokenAsync(string userId, string expectedRole, CancellationToken cancellationToken)
    {
        MutationCalls++;
        return Task.FromResult<string?>("must-not-be-returned");
    }

    public Task<UserSummary?> AddStationAsync(string userId, string expectedRole, string stationId, CancellationToken cancellationToken)
    {
        MutationCalls++;
        return Task.FromResult<UserSummary?>(target);
    }

    public Task<IReadOnlyList<string>?> ReplaceStationsByPhoneAsync(string phone, string expectedRole, IReadOnlyList<string> stations, CancellationToken cancellationToken)
    {
        MutationCalls++;
        return Task.FromResult<IReadOnlyList<string>?>(stations);
    }

    public Task<bool> DeleteUserAsync(string userId, string expectedRole, CancellationToken cancellationToken)
    {
        MutationCalls++;
        return Task.FromResult(true);
    }

    public Task<UserProfile?> FindProfileAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<UserProfile?> UpdateProfileAsync(string userId, string? name, string? email, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<UserAddress>?> AddAddressAsync(string userId, UserAddress address, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<UserAddress>?> UpdateAddressAsync(string userId, string addressId, UserAddressPatch patch, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<UserAddress>?> DeleteAddressAsync(string userId, string addressId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<UserAddress>?> SetDefaultAddressAsync(string userId, string addressId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<UserOrderTemplate>?> GetOrderTemplatesAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<UserOrderTemplate?> AddOrderTemplateAsync(string userId, string? displayName, IReadOnlyList<UserTemplateProduct> products, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<UserOrderTemplate?> UpdateOrderTemplateAsync(string userId, int index, string? displayName, IReadOnlyList<UserTemplateProduct>? products, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> DeleteOrderTemplateAsync(string userId, int index, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<UserSummary>> ListUsersAsync(string viewerRole, bool customersOnly, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> HasOtherUserWithRoleAsync(string role, string? excludingUserId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<UserPasswordRecord?> FindPasswordAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> ReplacePasswordAsync(string userId, string passwordHash, string loginToken, DateTimeOffset passwordChangedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
}
