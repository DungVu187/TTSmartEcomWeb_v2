using TTSmartEcom.Infrastructure.MongoDb.Security;

namespace TTSmartEcom.UnitTests.Security;

public sealed class PasswordHashCompatibilityVerifierTests
{
    [Fact]
    public void Verify_WhenLegacyBcryptHashMatches_ShouldReturnTrue()
    {
        PasswordHashCompatibilityVerifier verifier = new();
        string hash = BCrypt.Net.BCrypt.HashPassword("correct horse battery staple", 10);

        Assert.True(verifier.Verify("correct horse battery staple", hash));
        Assert.False(verifier.Verify("wrong", hash));
    }
}
