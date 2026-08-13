using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Integrations;

namespace TTSmartEcom.UnitTests.Integrations;

public sealed class ZaloOAuthStateServiceTests
{
    [Fact]
    public void State_IsSignedBoundToRedirectAndSingleUse()
    {
        ZaloOAuthStateService service = new(
            Options.Create(new ZaloOAuthOptions { StateSecret = new string('s', 32) }),
            TimeProvider.System);

        Assert.True(service.TryCreate("admin-1", "https://api.example.test/zalo/callback", out string state));
        Assert.False(service.TryConsume(state, "https://evil.example.test/zalo/callback"));
        Assert.True(service.TryConsume(state, "https://api.example.test/zalo/callback"));
        Assert.False(service.TryConsume(state, "https://api.example.test/zalo/callback"));
    }

    [Fact]
    public void TamperedState_IsRejectedWithoutConsumingOriginal()
    {
        ZaloOAuthStateService service = new(
            Options.Create(new ZaloOAuthOptions { StateSecret = new string('s', 32) }),
            TimeProvider.System);
        Assert.True(service.TryCreate("admin-1", "https://api.example.test/zalo/callback", out string state));

        int signatureStart = state.IndexOf('.', StringComparison.Ordinal) + 1;
        string tampered = state[..signatureStart] + (state[signatureStart] == 'A' ? 'B' : 'A') + state[(signatureStart + 1)..];

        Assert.False(service.TryConsume(tampered, "https://api.example.test/zalo/callback"));
        Assert.True(service.TryConsume(state, "https://api.example.test/zalo/callback"));
    }

    [Fact]
    public void State_WhenPendingCapacityIsReached_ShouldFailClosed()
    {
        ZaloOAuthStateService service = new(
            Options.Create(new ZaloOAuthOptions
            {
                StateSecret = new string('s', 32),
                MaxPendingStates = 16,
            }),
            TimeProvider.System);

        for (int index = 0; index < 16; index++)
        {
            Assert.True(service.TryCreate($"admin-{index}", "https://api.example.test/zalo/callback", out _));
        }

        Assert.False(service.TryCreate("admin-overflow", "https://api.example.test/zalo/callback", out _));
    }

    [Fact]
    public void State_WithOversizedInput_ShouldRejectWithoutAllocatingPendingEntry()
    {
        ZaloOAuthStateService service = new(
            Options.Create(new ZaloOAuthOptions { StateSecret = new string('s', 32) }),
            TimeProvider.System);

        Assert.False(service.TryCreate(new string('a', 257), "https://api.example.test/zalo/callback", out _));
        Assert.False(service.TryConsume(new string('x', 4_097), "https://api.example.test/zalo/callback"));
        Assert.True(service.TryCreate("admin-1", "https://api.example.test/zalo/callback", out _));
    }
}
