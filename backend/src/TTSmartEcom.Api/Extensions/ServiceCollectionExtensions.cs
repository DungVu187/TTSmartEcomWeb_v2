using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Files;
using TTSmartEcom.Application.Abstractions.Users;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Security;
using TTSmartEcom.Infrastructure.SqlServer.Products;
using TTSmartEcom.Infrastructure.SqlServer.Stations;
using TTSmartEcom.Infrastructure.SqlServer.Storefront;
using TTSmartEcom.Infrastructure.SqlServer.Voice;
using TTSmartEcom.Infrastructure.SqlServer.Audit;
using TTSmartEcom.Infrastructure.SqlServer.Cart;
using TTSmartEcom.Infrastructure.SqlServer.Orders;
using TTSmartEcom.Infrastructure.SqlServer.Inventory;
using TTSmartEcom.Infrastructure.SqlServer.Users;
using TTSmartEcom.Infrastructure.SqlServer.Integrations;
using TTSmartEcom.Infrastructure.SqlServer.Catalog;
using TTSmartEcom.Infrastructure.SqlServer.Files;
using TTSmartEcom.Application.Users;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Application.Catalog;
using TTSmartEcom.Application.Cart;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Abstractions.Catalog;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Application.Storefront;
using TTSmartEcom.Application.Audit;
using System.Threading.RateLimiting;
using TTSmartEcom.Application.Integrations;
using TTSmartEcom.Application.Inventory;
using TTSmartEcom.Application.Voice;
using TTSmartEcom.Api.Controllers.Products;
using TTSmartEcom.Api.Files;
using TTSmartEcom.Api.Integrations;
using TTSmartEcom.Api.Voice;

namespace TTSmartEcom.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTtsmartApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTtsmartReverseProxy(configuration);
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName));
        services.AddOptions<LegacyCompatibilityOptions>()
            .Bind(configuration.GetSection(LegacyCompatibilityOptions.SectionName));
        services.AddOptions<FrontendHostingOptions>()
            .Bind(configuration.GetSection(FrontendHostingOptions.SectionName));
        services.AddOptions<UploadOptions>()
            .Bind(configuration.GetSection(UploadOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<ExternalServicesOptions>()
            .Bind(configuration.GetSection(ExternalServicesOptions.SectionName));
        services.AddOptions<ZaloOAuthOptions>()
            .Bind(configuration.GetSection(ZaloOAuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSqlServerInfrastructure(configuration);
        services.AddSingleton<IUserIdentityReader, SqlUserIdentityReader>();
        services.AddSingleton<IPasswordHashCompatibilityVerifier, SqlPasswordHashCompatibilityVerifier>();
        services.AddScoped<IUserRepository, SqlUserRepository>();
        services.AddScoped<UserAuthenticationService>();
        services.AddScoped<UserPasswordRecoveryService>();
        services.AddSingleton<ISmtpMailTransport, SmtpMailTransport>();
        services.AddSingleton<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();
        services.AddScoped<ProductCatalogReadService>();
        services.AddScoped<ProductAccessScopeService>();
        services.AddScoped<ProductBranchDistributionService>();
        services.AddScoped<CatalogReadService>();
        services.AddScoped<ProductCatalogWriteService>();
        services.AddScoped<CatalogWriteService>();
        services.AddScoped<ProductMediaService>();
        services.AddScoped<IProductAiProvider, GeminiProductAiProvider>();
        services.AddScoped<ProductInvoiceMatchingService>();
        services.AddScoped<CatalogMediaService>();
        services.AddScoped<ProductMediaFileService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, SalesOrderService>();
        services.AddScoped<ActivityLogWriteService>();
        services.AddScoped<IInventoryOrderService, InventoryOrderService>();
        services.AddScoped<ProviderSettingsService>();
        services.AddScoped<ZaloOAuthService>();
        services.AddScoped<ITelegramMessageSender, TelegramMessageSender>();
        services.AddScoped<ICustomerOrderNotificationDispatcher, CustomerOrderNotificationDispatcher>();
        services.AddScoped<ICustomerOrderEmailSender, CustomerOrderEmailSender>();
        services.AddScoped<IZaloOrderMessageSender, ZaloOrderMessageSender>();
        services.AddSingleton<CustomerOrderNotificationScheduler>();
        services.AddSingleton<ICustomerOrderNotificationScheduler>(serviceProvider =>
            serviceProvider.GetRequiredService<CustomerOrderNotificationScheduler>());
        services.AddHostedService(serviceProvider =>
            serviceProvider.GetRequiredService<CustomerOrderNotificationScheduler>());
        services.AddSingleton<IZaloOAuthStateService, ZaloOAuthStateService>();
        services.AddScoped<IZaloOAuthClient, ZaloOAuthClient>();
        // SQL registrations come last while Mongo implementations remain available solely
        // for rollback/migration code; default runtime must not resolve Mongo here.
        services.AddScoped<SqlProductCatalogRepository>();
        services.AddScoped<SqlBranchProductReader>();
        services.AddScoped<IProductCatalogRepository>(serviceProvider => serviceProvider.GetRequiredService<SqlProductCatalogRepository>());
        services.AddScoped<IProductBranchAssignmentRepository, SqlProductBranchAssignmentRepository>();
        services.AddScoped<ICompanyBranchDirectory, SqlCompanyBranchDirectory>();
        services.AddScoped<SqlProductMutationRepository>();
        services.AddScoped<IProductCatalogWriteRepository>(serviceProvider => serviceProvider.GetRequiredService<SqlProductMutationRepository>());
        services.AddScoped<IProductMediaRepository>(serviceProvider => serviceProvider.GetRequiredService<SqlProductMutationRepository>());
        services.AddScoped<IOrderRepository, SqlOrderRepository>();
        services.AddScoped<IOrderStockPort, SqlOrderStockPort>();
        services.AddScoped<IInventoryOrderRepository, SqlInventoryOrderRepository>();
        services.AddScoped<IUserProfileRepository, SqlUserProfileRepository>();
        services.AddDataProtection();
        services.AddSingleton<ISqlLocalSecretStore, SqlLocalSecretStore>();
        services.AddScoped<SqlProviderSettingsRepository>();
        services.AddScoped<IProviderSettingsRepository>(serviceProvider => serviceProvider.GetRequiredService<SqlProviderSettingsRepository>());
        services.AddScoped<IZaloOrderCredentialRepository, SqlZaloOrderCredentialRepository>();
        services.AddScoped<SqlCatalogRepository>();
        services.AddScoped<ICatalogRepository>(serviceProvider => serviceProvider.GetRequiredService<SqlCatalogRepository>());
        services.AddScoped<ICatalogWriteRepository>(serviceProvider => serviceProvider.GetRequiredService<SqlCatalogRepository>());
        services.AddScoped<ICatalogMediaRepository>(serviceProvider => serviceProvider.GetRequiredService<SqlCatalogRepository>());
        services.AddScoped<SqlFileMetadataRepository>();
        services.AddSingleton<ISuperAdminMutationGuard, SqlSuperAdminMutationGuard>();
        services.AddSingleton<IPasswordHashWriter, SqlPasswordHashWriter>();
        services.AddScoped<IStationRepository, SqlStationRepository>();
        services.AddScoped<IStorefrontRepository, SqlStorefrontRepository>();
        services.AddScoped<IVoiceVocabularyRepository, SqlVoiceVocabularyRepository>();
        services.AddScoped<SqlCartRepository>();
        services.AddScoped<ICartRepository>(serviceProvider => serviceProvider.GetRequiredService<SqlCartRepository>());
        services.AddScoped<ICartProductCatalog>(serviceProvider => serviceProvider.GetRequiredService<SqlCartRepository>());
        services.AddScoped<SqlAuditRepository>();
        services.AddScoped<IAuditRepository>(serviceProvider => serviceProvider.GetRequiredService<SqlAuditRepository>());
        services.AddScoped<IActivityLogWriter>(serviceProvider => serviceProvider.GetRequiredService<SqlAuditRepository>());
        services.AddScoped<SqlStorageHistoryRepository>();
        services.AddScoped<IStorageHistoryRepository>(serviceProvider => serviceProvider.GetRequiredService<SqlStorageHistoryRepository>());
        services.AddScoped<IStorageHistoryWriter>(serviceProvider => serviceProvider.GetRequiredService<SqlStorageHistoryRepository>());
        services.AddSingleton<IVoiceVocabularyRuntime, VoiceVocabularyRuntime>();
        services.AddScoped<VoiceVocabularyService>();
        services.AddHostedService<VoiceVocabularyInitializationService>();
        services.AddSingleton<IFileValidationService, FileValidationService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<LocalMediaFileService>();
        services.AddHttpClient("telegram", client => client.Timeout = TimeSpan.FromSeconds(10))
            // Telegram requires the bot token in the URL path. Disable the factory's
            // URI loggers for this named client; the adapter emits redacted EventIds.
            .RemoveAllLoggers();
        services.AddHttpClient("zalo", client => client.Timeout = TimeSpan.FromSeconds(10));
        int geminiTimeout = Math.Clamp(configuration.GetValue<int?>("ExternalServices:GeminiTimeoutSeconds") ?? 25, 5, 60);
        services.AddHttpClient("gemini", client => client.Timeout = TimeSpan.FromSeconds(geminiTimeout));

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 20L * 1024 * 1024;
        });

        JwtOptions jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = "userId",
                    RoleClaimType = "role",
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.TryGetValue("authToken", out string? token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser())
            .AddPolicy("admin", policy => policy.RequireRole("superadmin", "admin", "staff"))
            .AddPolicy("admin-only", policy => policy.RequireRole("superadmin", "admin"));
        foreach (string permission in Domain.Security.SystemPermissions.All)
        {
            services.AddAuthorizationBuilder().AddPolicy($"permission:{permission}", policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
        }

        services.AddCors();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0,
                }));
            options.AddPolicy("public", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        services.AddHealthChecks();
        return services;
    }
}
