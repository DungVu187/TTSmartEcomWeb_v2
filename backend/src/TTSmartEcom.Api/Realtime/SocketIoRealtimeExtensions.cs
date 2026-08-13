using Microsoft.Extensions.DependencyInjection.Extensions;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Application.Realtime;

namespace TTSmartEcom.Api.Realtime;

public static class SocketIoRealtimeExtensions
{
    private const string DecoratedOrderServiceKey = "TTSmartEcom.Realtime.InnerOrderService";

    /// <summary>
    /// Registers the bounded Engine.IO v4 / Socket.IO v5 adapter and decorates the existing order service.
    /// Call this after AddTtsmartApi so the existing IOrderService registration can be captured.
    /// </summary>
    public static IServiceCollection AddTtsmartSocketIoRealtime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SocketIoRealtimeOptions>()
            .Bind(configuration.GetSection(SocketIoRealtimeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<SocketIoAuthenticator>();
        services.TryAddSingleton<SocketIoOriginPolicy>();
        services.TryAddSingleton<SocketIoServer>();
        services.TryAddSingleton<IOrderRealtimePublisher, SocketIoOrderRealtimePublisher>();
        DecorateOrderService(services);
        return services;
    }

    /// <summary>Enables the ASP.NET Core WebSocket feature with the Socket.IO heartbeat owning liveness.</summary>
    public static IApplicationBuilder UseTtsmartSocketIoRealtime(this IApplicationBuilder app)
    {
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.Zero });
        return app;
    }

    /// <summary>Maps both legacy-compatible transport paths. Endpoint routing accepts an optional trailing slash.</summary>
    public static IEndpointRouteBuilder MapTtsmartSocketIoRealtime(this IEndpointRouteBuilder endpoints)
    {
        SocketIoServer server = endpoints.ServiceProvider.GetRequiredService<SocketIoServer>();
        endpoints.MapMethods("/socket.io", ["GET", "POST", "OPTIONS"], server.HandleAsync);
        endpoints.MapMethods("/api/socket.io", ["GET", "POST", "OPTIONS"], server.HandleAsync);
        return endpoints;
    }

    private static void DecorateOrderService(IServiceCollection services)
    {
        ServiceDescriptor? descriptor = services.LastOrDefault(
            static candidate => candidate.ServiceType == typeof(IOrderService)
                && candidate.ServiceKey is null);
        if (descriptor is null)
        {
            throw new InvalidOperationException(
                "AddTtsmartSocketIoRealtime must be called after the IOrderService registration.");
        }

        services.Remove(descriptor);
        services.Add(ServiceDescriptor.DescribeKeyed(
            typeof(IOrderService),
            DecoratedOrderServiceKey,
            (serviceProvider, _) => CreateFromDescriptor(serviceProvider, descriptor),
            descriptor.Lifetime));
        services.Add(ServiceDescriptor.Describe(
            typeof(IOrderService),
            serviceProvider => new OrderRealtimeServiceDecorator(
                serviceProvider.GetRequiredKeyedService<IOrderService>(DecoratedOrderServiceKey),
                serviceProvider.GetRequiredService<IOrderRealtimePublisher>(),
                serviceProvider.GetRequiredService<ILogger<OrderRealtimeServiceDecorator>>()),
            descriptor.Lifetime));
    }

    private static object CreateFromDescriptor(
        IServiceProvider serviceProvider,
        ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(serviceProvider);
        }

        Type implementationType = descriptor.ImplementationType
            ?? throw new InvalidOperationException("The IOrderService registration has no implementation.");
        return ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, implementationType);
    }
}
