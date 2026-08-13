using TTSmartEcom.Application.Voice;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Domain.Voice;

namespace TTSmartEcom.UnitTests.Voice;

public sealed class VoiceVocabularyServiceTests
{
    [Fact]
    public async Task CreateSimpleValue_ShouldPersistWithOptimisticVersion()
    {
        FakeRepository repository = new();
        RecordingRuntime runtime = new();
        VoiceVocabularyService service = new(repository, runtime);

        VoiceVocabulary result = await service.CreateAsync("brands", new VoiceVocabularyMutation(Value: "Acme"), CancellationToken.None);

        Assert.Contains("Acme", result.Brands);
        Assert.Equal(2, result.Version);
        Assert.Equal(1, repository.LastExpectedVersion);
        Assert.Same(result, runtime.LastValue);
    }

    [Fact]
    public async Task CreateDuplicateSimpleValue_ShouldReject()
    {
        FakeRepository repository = new(new VoiceVocabulary([], ["Acme"], [], [], [], [], [], 4));
        VoiceVocabularyService service = new(repository, new RecordingRuntime());

        TTSmartEcom.Application.Common.Errors.ApplicationException error = await Assert.ThrowsAsync<TTSmartEcom.Application.Common.Errors.ApplicationException>(
            () => service.CreateAsync("brands", new VoiceVocabularyMutation(Value: " acme "), CancellationToken.None));

        Assert.Equal(400, error.Error.HttpStatus);
    }

    [Fact]
    public async Task GetAsync_WhenMissing_ShouldSeedExactLegacyDefaults()
    {
        FakeRepository repository = new(hasDocument: false);
        RecordingRuntime runtime = new();
        VoiceVocabularyService service = new(repository, runtime);

        VoiceVocabulary result = await service.GetAsync(CancellationToken.None);

        Assert.Equal(24, result.Brands.Count);
        Assert.Equal(24, result.Types.Count);
        Assert.Equal(5, result.IntentAliases.Count);
        Assert.Contains(result.Stopwords, value => value == "kiem");
        Assert.Equal("Xuất Excel lịch sử", result.IntentAliases.Single(x => x.Intent == "export_history").Label);
        Assert.Equal(0, result.Version);
        Assert.Null(runtime.LastValue);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task InitializeAsync_WhenLegacyDocumentHasNoIntents_ShouldBackfillOnlyIntentAliasesAndRefresh()
    {
        VoiceVocabulary existing = new(
            ["custom-stop"], ["Custom brand"], ["Custom type"], [], [], [], [], 7);
        FakeRepository repository = new(existing);
        RecordingRuntime runtime = new();
        VoiceVocabularyService service = new(repository, runtime);

        VoiceVocabulary result = await service.InitializeAsync(CancellationToken.None);

        Assert.Equal(["custom-stop"], result.Stopwords);
        Assert.Equal(["Custom brand"], result.Brands);
        Assert.Equal(["Custom type"], result.Types);
        Assert.Equal(5, result.IntentAliases.Count);
        Assert.Equal(8, result.Version);
        Assert.Same(result, runtime.LastValue);
        Assert.Equal(7, repository.LastExpectedVersion);
    }

    [Fact]
    public async Task InitializeAsync_ShouldPassCancellationToRepository()
    {
        using CancellationTokenSource source = new();
        await source.CancelAsync();
        FakeRepository repository = new();
        VoiceVocabularyService service = new(repository, new RecordingRuntime());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.InitializeAsync(source.Token));
    }

    private sealed class FakeRepository : IVoiceVocabularyRepository
    {
        private VoiceVocabulary? value;

        public FakeRepository(VoiceVocabulary? initial = null, bool hasDocument = true)
        {
            value = hasDocument ? initial ?? new VoiceVocabulary([], [], [], [], [], [], [], 0) : null;
        }

        public int LastExpectedVersion { get; private set; } = -1;
        public int SaveCalls { get; private set; }

        public Task<VoiceVocabulary?> FindAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(value);
        }

        public Task<VoiceVocabulary?> SaveAsync(VoiceVocabulary vocabulary, int expectedVersion, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCalls++;
            LastExpectedVersion = expectedVersion;
            if (value is null)
            {
                if (expectedVersion != 0) return Task.FromResult<VoiceVocabulary?>(null);
                value = vocabulary with { Version = 0 };
                return Task.FromResult<VoiceVocabulary?>(value);
            }
            if (expectedVersion != value.Version) return Task.FromResult<VoiceVocabulary?>(null);
            value = vocabulary with { Version = checked(expectedVersion + 1) };
            return Task.FromResult<VoiceVocabulary?>(value);
        }
    }

    private sealed class RecordingRuntime : IVoiceVocabularyRuntime
    {
        public VoiceVocabulary? LastValue { get; private set; }
        public void Refresh(VoiceVocabulary vocabulary) => LastValue = vocabulary;
    }
}
