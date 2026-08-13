namespace TTSmartEcom.Api.Configuration;

public sealed class LegacyCompatibilityOptions
{
    public const string SectionName = "LegacyCompatibility";

    public bool AdminFullAccess { get; init; } = true;

    public bool EnableApiPrefixAlias { get; init; } = true;

    public bool PublicSignupEnabled { get; init; }
}
