using System.ComponentModel.DataAnnotations;

namespace TTSmartEcom.Infrastructure.MongoDb.Configuration;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string DatabaseName { get; init; } = string.Empty;
}
