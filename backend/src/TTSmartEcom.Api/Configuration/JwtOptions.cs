using System.ComponentModel.DataAnnotations;

namespace TTSmartEcom.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    [MinLength(32)]
    public string Secret { get; init; } = string.Empty;

    [Range(1, 168)]
    public int SessionHours { get; init; } = 12;

    [Range(0, 300)]
    public int ClockSkewSeconds { get; init; }
}
