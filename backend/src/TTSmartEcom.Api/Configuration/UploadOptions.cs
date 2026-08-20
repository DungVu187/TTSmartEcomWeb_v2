using System.ComponentModel.DataAnnotations;

namespace TTSmartEcom.Api.Configuration;

public sealed class UploadOptions
{
    public const string SectionName = "Uploads";

    [Required]
    public string RootPath { get; init; } = "uploads";

    public bool RecordMetadata { get; init; } = true;

    [Range(1, 100)]
    public int ProductImageMegabytes { get; init; } = 4;

    [Range(1, 100)]
    public int ProductDocumentMegabytes { get; init; } = 20;

    [Range(1, 100)]
    public int InvoiceMegabytes { get; init; } = 5;

    [Range(1, 100)]
    public int VoiceMegabytes { get; init; } = 10;
}
