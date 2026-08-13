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
using TTSmartEcom.Infrastructure.MongoDb;
using TTSmartEcom.Application.Users;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Application.Catalog;
using TTSmartEcom.Application.Cart;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Abstractions.Catalog;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Products;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Catalog;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Cart;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Orders;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Application.Storefront;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Users;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Stations;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Storefront;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Audit;
using System.Threading.RateLimiting;
using TTSmartEcom.Application.Integrations;
using TTSmartEcom.Application.Inventory;
using TTSmartEcom.Application.Voice;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Integrations;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Inventory;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Voice;
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

        services.AddMongoInfrastructure(configuration);
        services.AddScoped<UserAuthenticationService>();
        services.AddScoped<UserPasswordRecoveryService>();
        services.AddSingleton<ISmtpMailTransport, SmtpMailTransport>();
        services.AddSingleton<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();
        services.AddScoped<ProductCatalogReadService>();
        services.AddScoped<ProductAccessScopeService>();
        services.AddScoped<CatalogReadService>();
        services.AddScoped<ProductCatalogWriteService>();
        services.AddScoped<CatalogWriteService>();
        services.AddScoped<ProductMediaService>();
        services.AddScoped<IProductAiProvider, GeminiProductAiProvider>();
        services.AddScoped<ProductInvoiceMatchingService>();
        services.AddScoped<CatalogMediaService>();
        services.AddScoped<ProductMediaFileService>();
        services.AddScoped<IProductCatalogRepository, MongoProductCatalogRepository>();
        services.AddScoped<ICatalogRepository, MongoCatalogRepository>();
        services.AddScoped<MongoProductCatalogWriteRepository>();
        services.AddScoped<MongoCatalogWriteRepository>();
        services.AddScoped<IProductCatalogWriteRepository>(services => services.GetRequiredService<MongoProductCatalogWriteRepository>());
        services.AddScoped<ICatalogWriteRepository>(services => services.GetRequiredService<MongoCatalogWriteRepository>());
        services.AddScoped<IProductMediaRepository>(services => services.GetRequiredService<MongoProductCatalogWriteRepository>());
        services.AddScoped<ICatalogMediaRepository>(services => services.GetRequiredService<MongoCatalogWriteRepository>());
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICartRepository, MongoCartRepository>();
        services.AddScoped<ICartProductCatalog>(services => services.GetRequiredService<MongoCartRepository>());
        services.AddScoped<MongoCartRepository>();
        services.AddScoped<IOrderRepository, MongoOrderRepository>();
        services.AddScoped<IOrderService, SalesOrderService>();
        services.AddScoped<IOrderStockPort, MongoOrderStockPort>();
        services.AddScoped<IUserProfileRepository, MongoUserProfileRepository>();
        services.AddScoped<ISuperAdminMutationGuard, MongoSuperAdminMutationGuard>();
        services.AddScoped<IPasswordHashWriter, MongoPasswordHashWriter>();
        services.AddScoped<IStationRepository, MongoStationRepository>();
        services.AddScoped<IStorefrontRepository, MongoStorefrontRepository>();
        services.AddScoped<MongoAuditRepository>();
        services.AddScoped<IAuditRepository>(serviceProvider =>
            serviceProvider.GetRequiredService<MongoAuditRepository>());
        services.AddScoped<IActivityLogWriter>(serviceProvider =>
            serviceProvider.GetRequiredService<MongoAuditRepository>());
        services.AddScoped<ActivityLogWriteService>();
        services.AddScoped<MongoStorageHistoryRepository>();
        services.AddScoped<IStorageHistoryRepository>(serviceProvider =>
            serviceProvider.GetRequiredService<MongoStorageHistoryRepository>());
        services.AddScoped<IStorageHistoryWriter>(serviceProvider =>
            serviceProvider.GetRequiredService<MongoStorageHistoryRepository>());
        services.AddScoped<IInventoryOrderRepository, MongoInventoryOrderRepository>();
        services.AddScoped<IInventoryOrderService, InventoryOrderService>();
        services.AddScoped<IProviderSettingsRepository, MongoProviderSettingsRepository>();
        services.AddScoped<ProviderSettingsService>();
        services.AddScoped<ZaloOAuthService>();
        services.AddScoped<ITelegramMessageSender, TelegramMessageSender>();
        services.AddScoped<ICustomerOrderNotificationDispatcher, CustomerOrderNotificationDispatcher>();
        services.AddScoped<ICustomerOrderEmailSender, CustomerOrderEmailSender>();
        services.AddScoped<IZaloOrderMessageSender, ZaloOrderMessageSender>();
        services.AddScoped<IZaloOrderCredentialRepository, MongoZaloOrderCredentialRepository>();
        services.AddSingleton<CustomerOrderNotificationScheduler>();
        services.AddSingleton<ICustomerOrderNotificationScheduler>(serviceProvider =>
            serviceProvider.GetRequiredService<CustomerOrderNotificationScheduler>());
        services.AddHostedService(serviceProvider =>
            serviceProvider.GetRequiredService<CustomerOrderNotificationScheduler>());
        services.AddSingleton<IZaloOAuthStateService, ZaloOAuthStateService>();
        services.AddScoped<IZaloOAuthClient, ZaloOAuthClient>();
        services.AddScoped<IVoiceVocabularyRepository, MongoVoiceVocabularyRepository>();
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
