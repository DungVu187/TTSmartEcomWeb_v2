using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;

namespace TTSmartEcom.Api.Extensions;

public static class ReverseProxyExtensions
{
    public static IServiceCollection AddTtsmartReverseProxy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<ReverseProxyOptions>, ReverseProxyOptionsValidator>();
        services.AddOptions<ReverseProxyOptions>()
            .Bind(configuration.GetSection(ReverseProxyOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ReverseProxyOptions>>((options, configuredOptions) =>
        {
            ReverseProxyOptions configured = configuredOptions.Value;
            options.ForwardedHeaders = configured.Enabled
                ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                : ForwardedHeaders.None;
            options.ForwardLimit = configured.ForwardLimit;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (string value in configured.KnownProxies)
            {
                if (IPAddress.TryParse(value, out IPAddress? address))
                {
                    options.KnownProxies.Add(address);
                }
            }

            foreach (string value in configured.KnownNetworks)
            {
                if (System.Net.IPNetwork.TryParse(value, out System.Net.IPNetwork network))
                {
                    options.KnownIPNetworks.Add(network);
                }
            }
        });

        return services;
    }

    public static WebApplication UseTtsmartReverseProxy(this WebApplication app)
    {
        ReverseProxyOptions options = app.Services
            .GetRequiredService<IOptions<ReverseProxyOptions>>()
            .Value;
        if (options.Enabled)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
