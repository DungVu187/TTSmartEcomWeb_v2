using System.Text.Json;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Domain.Voice;

namespace TTSmartEcom.UnitTests.Products;

public sealed class ProductVoiceQueryServiceTests
{
    [Fact]
    public void FromText_ShouldPreserveLegacySearchEnvelope()
    {
        VoiceQueryResult result = ProductVoiceQueryService.FromText("tìm plc siemens nhé");

        Assert.Equal("tìm plc siemens nhé", result.Transcript);
        Assert.Equal("PLC", result.Keyword);
        Assert.Equal("search_product", result.Intent);
        Assert.Equal("Siemens", result.Filters.Brand);
        Assert.Equal("PLC", result.Filters.Type);
        Assert.Null(result.Filters.Code);
    }

    [Fact]
    public void FromText_ShouldRecognizeHistoryExportDateRange()
    {
        VoiceQueryResult result = ProductVoiceQueryService.FromText(
            "xuất excel lịch sử xuất kho từ ngày 01/08/2026 tới ngày 05/08/2026");

        Assert.Equal("export_history", result.Intent);
        Assert.Equal("export", result.HistoryExport?.Direction);
        Assert.Equal("custom", result.HistoryExport?.DatePreset);
        Assert.Equal("2026-08-01", result.HistoryExport?.StartDate);
        Assert.Equal("2026-08-05", result.HistoryExport?.EndDate);
    }

    [Fact]
    public void FromProvider_ShouldRemoveHallucinatedBrand()
    {
        using JsonDocument json = JsonDocument.Parse("""
            {
              "transcript": "tìm plc",
              "keyword": "PLC Siemens",
              "intent": "search_product",
              "filters": { "brand": "Siemens", "type": "PLC", "code": null }
            }
            """);

        VoiceQueryResult result = ProductVoiceQueryService.FromProvider(json.RootElement);

        Assert.Null(result.Filters.Brand);
        Assert.Equal("PLC", result.Filters.Type);
    }

    [Fact]
    public void RuntimeRefresh_ShouldImmediatelyAffectTextNormalizationAndPrompt()
    {
        VoiceVocabularyRuntime runtime = new();
        VoiceVocabulary custom = VoiceVocabularyDefaults.Create() with
        {
            Stopwords = ["hay"],
            Brands = ["CustomBrand"],
            Types = ["CustomType"],
            BrandAliases = [new VoiceBrandAlias("CustomBrand", ["thuong hieu rieng"])],
            TypeAliases = [new VoiceTypeAlias("CustomType", "từ khóa riêng", ["loai rieng"])],
            IntentAliases = [new VoiceIntentAlias("add_to_cart", "Thêm", ["gom vao gio"])],
            CodeMap = [new VoiceCodeMap("ZZ99", "thiết bị ZZ99", "CustomBrand", "CustomType", [@"\bzz\s*99\b"], "zz99")],
        };

        try
        {
            runtime.Refresh(custom);

            VoiceQueryResult aliasResult = ProductVoiceQueryService.FromText("gom vao gio loai rieng thuong hieu rieng hay");
            VoiceQueryResult codeResult = ProductVoiceQueryService.FromText("zz 99 hay");
            string prompt = ProductVoiceQueryService.BuildAudioPrompt();

            Assert.Equal("add_to_cart", aliasResult.Intent);
            Assert.Equal("CustomBrand", aliasResult.Filters.Brand);
            Assert.Equal("CustomType", aliasResult.Filters.Type);
            Assert.Equal("ZZ99", codeResult.Filters.Code);
            Assert.Equal("thiết bị ZZ99", codeResult.Keyword);
            Assert.Contains("CustomBrand", prompt, StringComparison.Ordinal);
            Assert.Contains("thuong hieu rieng", prompt, StringComparison.Ordinal);
        }
        finally
        {
            runtime.Refresh(VoiceVocabularyDefaults.Create());
        }
    }

    [Fact]
    public async Task RuntimeRefresh_ShouldPublishWholeImmutableSnapshotsUnderConcurrency()
    {
        VoiceVocabularyRuntime runtime = new();
        VoiceVocabulary first = Vocabulary("BrandA", "alpha brand");
        VoiceVocabulary second = Vocabulary("BrandB", "beta brand");
        runtime.Refresh(first);

        try
        {
            Task writer = Task.Run(() =>
            {
                for (int index = 0; index < 100; index++) runtime.Refresh(index % 2 == 0 ? first : second);
            });
            Task[] readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                for (int index = 0; index < 100; index++)
                {
                    VoiceQueryResult a = ProductVoiceQueryService.FromText("alpha brand");
                    VoiceQueryResult b = ProductVoiceQueryService.FromText("beta brand");
                    Assert.True(a.Filters.Brand is null or "BrandA");
                    Assert.True(b.Filters.Brand is null or "BrandB");
                }
            })).ToArray();

            await Task.WhenAll(readers.Append(writer));
        }
        finally
        {
            runtime.Refresh(VoiceVocabularyDefaults.Create());
        }
    }

    private static VoiceVocabulary Vocabulary(string brand, string alias) => VoiceVocabularyDefaults.Create() with
    {
        Brands = [brand],
        BrandAliases = [new VoiceBrandAlias(brand, [alias])],
    };
}
