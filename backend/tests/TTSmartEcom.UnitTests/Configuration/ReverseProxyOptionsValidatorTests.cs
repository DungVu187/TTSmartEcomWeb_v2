using TTSmartEcom.Api.Configuration;

namespace TTSmartEcom.UnitTests.Configuration;

public sealed class ReverseProxyOptionsValidatorTests
{
    private readonly ReverseProxyOptionsValidator validator = new();

    [Fact]
    public void DisabledConfiguration_DoesNotRequireTrustedForwarders()
    {
        Assert.True(validator.Validate(null, new ReverseProxyOptions()).Succeeded);
    }

    [Fact]
    public void EnabledConfiguration_RequiresAtLeastOneTrustedForwarder()
    {
        Assert.False(validator.Validate(null, new ReverseProxyOptions { Enabled = true }).Succeeded);
    }

    [Theory]
    [InlineData("not-an-ip", null)]
    [InlineData(null, "10.0.0.0/not-a-prefix")]
    public void EnabledConfiguration_RejectsInvalidAddressOrNetwork(string? proxy, string? network)
    {
        ReverseProxyOptions options = new()
        {
            Enabled = true,
            KnownProxies = proxy is null ? [] : [proxy],
            KnownNetworks = network is null ? [] : [network],
        };

        Assert.False(validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void EnabledConfiguration_AcceptsValidAddressAndCidr()
    {
        ReverseProxyOptions options = new()
        {
            Enabled = true,
            ForwardLimit = 2,
            KnownProxies = ["10.0.0.10"],
            KnownNetworks = ["192.0.2.0/24"],
        };

        Assert.True(validator.Validate(null, options).Succeeded);
    }
}
