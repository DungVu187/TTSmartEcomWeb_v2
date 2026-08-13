using TTSmartEcom.Application.Voice;

namespace TTSmartEcom.Api.Voice;

/// <summary>
/// Nạp cache voice lúc host khởi động. Legacy coi lỗi nạp cache là best-effort,
/// vì vậy lỗi hạ tầng được log theo loại (không kèm message/payload) và không
/// ngăn API khởi động. Cancellation từ host luôn được tôn trọng.
/// </summary>
public sealed partial class VoiceVocabularyInitializationService(
    IServiceScopeFactory scopeFactory,
    ILogger<VoiceVocabularyInitializationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            VoiceVocabularyService service = scope.ServiceProvider.GetRequiredService<VoiceVocabularyService>();
            await service.InitializeAsync(cancellationToken);
            LogLoaded(logger);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogFailed(logger, exception.GetType().Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(EventId = 4810, Level = LogLevel.Information, Message = "Đã nạp cache từ vựng voice")]
    private static partial void LogLoaded(ILogger logger);

    [LoggerMessage(EventId = 4811, Level = LogLevel.Error, Message = "Không thể nạp cache từ vựng voice lúc khởi động; tiếp tục dùng defaults. Loại lỗi: {ErrorType}")]
    private static partial void LogFailed(ILogger logger, string errorType);
}
