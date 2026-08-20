using System.Security.Cryptography;

namespace TTSmartEcom.Infrastructure.SqlServer;

/// <summary>Generates the lower-case, 24-hex public identifiers preserved by legacy APIs.</summary>
internal static class SqlPublicIds
{
    public static string New()
    {
        Span<byte> bytes = stackalloc byte[12];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
