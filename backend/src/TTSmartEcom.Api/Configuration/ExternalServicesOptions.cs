namespace TTSmartEcom.Api.Configuration;

public sealed class ExternalServicesOptions
{
    public const string SectionName = "ExternalServices";

    public string? PublicAddress { get; init; }

    public string? FrontendUrl { get; init; }

    public string? GeminiApiKey { get; init; }

    public int GeminiTimeoutSeconds { get; init; } = 25;

    public string? TelegramBotToken { get; init; }

    public string? GmailUser { get; init; }

    public string? GmailAppPassword { get; init; }

    public string GmailSmtpHost { get; init; } = "smtp.gmail.com";

    public int GmailSmtpPort { get; init; } = 587;

    public int GmailTimeoutSeconds { get; init; } = 15;

    public string? AdminNotifyEmail { get; init; }

}
