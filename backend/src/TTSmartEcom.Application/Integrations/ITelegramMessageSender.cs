namespace TTSmartEcom.Application.Integrations;

public interface ITelegramMessageSender
{
    Task<bool> SendAsync(string chatId, string message, CancellationToken cancellationToken);
}
