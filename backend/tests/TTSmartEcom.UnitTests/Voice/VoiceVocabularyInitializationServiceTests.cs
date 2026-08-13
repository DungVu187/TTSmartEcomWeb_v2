using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TTSmartEcom.Api.Voice;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Application.Voice;
using TTSmartEcom.Domain.Voice;

namespace TTSmartEcom.UnitTests.Voice;

public sealed class VoiceVocabularyInitializationServiceTests
{
    [Fact]
    public async Task StartAsync_WhenRepositoryFails_ShouldKeepHostStartingAndLogOnlyErrorType()
    {
        ListLogger logger = new();
        await using ServiceProvider provider = BuildProvider(new FailingRepository());
        VoiceVocabularyInitializationService service = new(provider.GetRequiredService<IServiceScopeFactory>(), logger);

        await service.StartAsync(CancellationToken.None);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(nameof(InvalidOperationException), entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(FailingRepository.SensitiveMessage, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WhenHostCancels_ShouldPropagateCancellation()
    {
        await using ServiceProvider provider = BuildProvider(new CancelingRepository());
        VoiceVocabularyInitializationService service = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new ListLogger());
        using CancellationTokenSource source = new();
        await source.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.StartAsync(source.Token));
    }

    private static ServiceProvider BuildProvider(IVoiceVocabularyRepository repository)
    {
        ServiceCollection services = new();
        services.AddSingleton(repository);
        services.AddSingleton<IVoiceVocabularyRuntime, VoiceVocabularyRuntime>();
        services.AddScoped<VoiceVocabularyService>();
        return services.BuildServiceProvider();
    }

    private sealed class FailingRepository : IVoiceVocabularyRepository
    {
        public const string SensitiveMessage = "credential-like-provider-value";
        public Task<VoiceVocabulary?> FindAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException(SensitiveMessage);
        public Task<VoiceVocabulary?> SaveAsync(VoiceVocabulary vocabulary, int expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CancelingRepository : IVoiceVocabularyRepository
    {
        public Task<VoiceVocabulary?> FindAsync(CancellationToken cancellationToken) =>
            Task.FromCanceled<VoiceVocabulary?>(cancellationToken);
        public Task<VoiceVocabulary?> SaveAsync(VoiceVocabulary vocabulary, int expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ListLogger : ILogger<VoiceVocabularyInitializationService>
    {
        public List<LogEntry> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);
}
