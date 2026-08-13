using Microsoft.AspNetCore.Mvc;

namespace TTSmartEcom.Api.Contracts.Products;

public sealed class InvoiceScanRequest
{
    [FromForm(Name = "invoice")]
    public IFormFile? Invoice { get; set; }
}

public sealed class VoiceAudioRequest
{
    [FromForm(Name = "audio")]
    public IFormFile? Audio { get; set; }
}

public sealed record VoiceTextRequest(string? Text);
