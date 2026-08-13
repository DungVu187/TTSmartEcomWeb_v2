namespace TTSmartEcom.Application.Abstractions.Authentication;

/// <summary>Verifies the bcrypt hashes already stored by the Node.js application.</summary>
public interface IPasswordHashCompatibilityVerifier
{
    bool Verify(string plaintextPassword, string storedHash);

    string Hash(string plaintextPassword);
}
