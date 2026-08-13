using System.Net;
using Microsoft.Extensions.Options;

namespace TTSmartEcom.Api.Configuration;

public sealed class ReverseProxyOptionsValidator : IValidateOptions<ReverseProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, ReverseProxyOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (options.ForwardLimit is < 1 or > 10)
        {
            return ValidateOptionsResult.Fail("ReverseProxy:ForwardLimit phải nằm trong khoảng 1–10.");
        }

        if (options.KnownProxies.Length == 0 && options.KnownNetworks.Length == 0)
        {
            return ValidateOptionsResult.Fail(
                "ReverseProxy được bật nhưng chưa cấu hình KnownProxies hoặc KnownNetworks.");
        }

        if (options.KnownProxies.Any(static value => !IPAddress.TryParse(value, out _)))
        {
            return ValidateOptionsResult.Fail("ReverseProxy:KnownProxies chứa địa chỉ IP không hợp lệ.");
        }

        if (options.KnownNetworks.Any(static value => !IPNetwork.TryParse(value, out _)))
        {
            return ValidateOptionsResult.Fail("ReverseProxy:KnownNetworks chứa CIDR không hợp lệ.");
        }

        return ValidateOptionsResult.Success;
    }
}
