using TTSmartEcom.Application.Audit;
using TTSmartEcom.Domain.Integrations;
using TTSmartEcom.Domain.Voice;

namespace TTSmartEcom.UnitTests.Audit;

public sealed class IntegrationActivityLogEntriesTests
{
    [Fact]
    public void ProviderFactories_ShouldEmitAllFiveLegacyActionsWithoutSecretOrChatId()
    {
        const string actor = "Quản trị viên";
        const string secret = "synthetic-sensitive-secret";
        const string chatId = "synthetic-sensitive-chat-id";
        ActivityLogWriteEntry[] entries =
        [
            IntegrationActivityLogEntries.UpdateZaloSettings(actor, "app-safe"),
            IntegrationActivityLogEntries.UpdateTelegramSettings(actor, true),
            IntegrationActivityLogEntries.CreateTelegramRecipient(actor, "Nhóm vận hành"),
            IntegrationActivityLogEntries.UpdateTelegramRecipient(actor, string.Empty),
            IntegrationActivityLogEntries.DeleteTelegramRecipient(actor, null),
        ];

        Assert.Equal(
        [
            "update_zalo_settings",
            "update_telegram_settings",
            "create_telegram_recipient",
            "update_telegram_recipient",
            "delete_telegram_recipient",
        ], entries.Select(static entry => entry.Action));
        Assert.All(entries, entry => Assert.Equal(actor, entry.UserName));
        string serialized = string.Join('|', entries.SelectMany(static entry =>
            entry.Details.Select(detail => $"{detail.Field}:{detail.OldValue}:{detail.NewValue}")));
        Assert.DoesNotContain(secret, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(chatId, serialized, StringComparison.Ordinal);
        Assert.Contains("(không có nhãn)", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void VoiceFactories_ShouldEmitThreeLegacyActionsAndEquivalentDetails()
    {
        VoiceVocabularyMutation create = new(Name: "Acme", Aliases: ["a", "acme"]);
        VoiceVocabularyMutation update = new(OldValue: "cũ", NewValue: "mới");
        VoiceVocabularyMutation delete = new(Code: "FX3U");

        ActivityLogWriteEntry[] entries =
        [
            IntegrationActivityLogEntries.CreateVoiceVocabulary("Admin", "brandAliases", create),
            IntegrationActivityLogEntries.UpdateVoiceVocabulary("Admin", "brands", update),
            IntegrationActivityLogEntries.DeleteVoiceVocabulary("Admin", "codeMap", delete),
        ];

        Assert.Equal(
            ["create_voice_vocab", "update_voice_vocab", "delete_voice_vocab"],
            entries.Select(static entry => entry.Action));
        Assert.Equal("Từ vựng voice: brandAliases", entries[0].ProductName);
        Assert.Equal("brandAliases", Assert.Single(entries[0].Details).Field);
        Assert.Contains("Thêm alias cho \"Acme\": a/acme", entries[0].Details[0].NewValue, StringComparison.Ordinal);
        Assert.Contains("Sửa \"cũ\" -> \"mới\"", entries[1].Details[0].NewValue, StringComparison.Ordinal);
        Assert.Contains("Xóa \"FX3U\"", entries[2].Details[0].NewValue, StringComparison.Ordinal);
    }

    [Fact]
    public void VoiceFactory_ShouldNormalizeControlCharactersAndBoundAuditText()
    {
        string oversized = "line\r\n" + new string('x', 1_000);

        ActivityLogWriteEntry entry = IntegrationActivityLogEntries.CreateVoiceVocabulary(
            "Admin",
            "brands",
            new VoiceVocabularyMutation(Value: oversized));

        string value = Assert.Single(entry.Details).NewValue!;
        Assert.DoesNotContain('\r', value);
        Assert.DoesNotContain('\n', value);
        Assert.True(value.Length <= 500);
    }
}
