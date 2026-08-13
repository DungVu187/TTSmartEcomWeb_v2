using TTSmartEcom.Application.Abstractions.Authentication;

namespace TTSmartEcom.Infrastructure.MongoDb.Security;

public sealed class PasswordHashCompatibilityVerifier : IPasswordHashCompatibilityVerifier
{
    private const int LegacyWorkFactor = 10;

    public bool Verify(string plaintextPassword, string storedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedHash);

        return BCrypt.Net.BCrypt.Verify(plaintextPassword, storedHash);
    }

    public string Hash(string plaintextPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextPassword);
        return BCrypt.Net.BCrypt.HashPassword(plaintextPassword, LegacyWorkFactor);
    }
}
