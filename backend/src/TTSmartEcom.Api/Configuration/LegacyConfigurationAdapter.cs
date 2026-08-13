namespace TTSmartEcom.Api.Configuration;

internal static class LegacyConfigurationAdapter
{
    public static void AddLegacyEnvironmentAliases(this ConfigurationManager configuration)
    {
        Dictionary<string, string?> aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            [$"{JwtOptions.SectionName}:Secret"] = configuration["JWT_SECRET"],
            [$"{ExternalServicesOptions.SectionName}:PublicAddress"] = configuration["ADDRESS"],
            [$"{ExternalServicesOptions.SectionName}:FrontendUrl"] = configuration["FRONTEND_URL"],
            [$"{ExternalServicesOptions.SectionName}:GeminiApiKey"] = configuration["GEMINI_API_KEY"],
            [$"{ExternalServicesOptions.SectionName}:TelegramBotToken"] = configuration["TELEGRAM_BOT_TOKEN"],
            [$"{ExternalServicesOptions.SectionName}:GmailUser"] = configuration["GMAIL_USER"],
            [$"{ExternalServicesOptions.SectionName}:GmailAppPassword"] = configuration["GMAIL_APP_PASSWORD"],
            [$"{ExternalServicesOptions.SectionName}:AdminNotifyEmail"] = configuration["ADMIN_NOTIFY_EMAIL"],
            [$"{ZaloOAuthOptions.SectionName}:StateSecret"] = configuration["ZALO_OAUTH_STATE_SECRET"],
            [$"{LegacyCompatibilityOptions.SectionName}:PublicSignupEnabled"] = configuration["PUBLIC_SIGNUP_ENABLED"],
        };

        configuration.AddInMemoryCollection(
            aliases.Where(entry => !string.IsNullOrWhiteSpace(entry.Value))!);
    }
}
