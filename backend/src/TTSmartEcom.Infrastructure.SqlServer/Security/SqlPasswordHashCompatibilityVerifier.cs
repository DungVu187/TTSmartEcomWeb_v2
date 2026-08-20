using TTSmartEcom.Application.Abstractions.Authentication;

namespace TTSmartEcom.Infrastructure.SqlServer.Security;

public sealed class SqlPasswordHashCompatibilityVerifier : IPasswordHashCompatibilityVerifier
{
    public bool Verify(string plaintextPassword, string storedHash) => BCrypt.Net.BCrypt.Verify(plaintextPassword, storedHash);
    public string Hash(string plaintextPassword) => BCrypt.Net.BCrypt.HashPassword(plaintextPassword, 10);
}
