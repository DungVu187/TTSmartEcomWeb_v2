using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Integrations;
using TTSmartEcom.Application.Products;

namespace TTSmartEcom.UnitTests.Integrations;

public sealed class GeminiProductAiProviderTests
{
    [Fact]
    public async Task AnalyzeInvoiceAsync_WhenModelsFail_ShouldUseLegacyFallbackOrderAndPromptRules()
    {
        CapturingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        GeminiProductAiProvider provider = Create(handler);

        ProductAiResult result = await provider.AnalyzeInvoiceAsync(
            "image/webp",
            new byte[] { 0x52, 0x49, 0x46, 0x46 },
            CancellationToken.None);

        Assert.Equal(ProductAiStatus.Unavailable, result.Status);
        Assert.Equal(
            [
                "gemini-3.5-flash",
                "gemini-2.5-flash",
                "gemini-3.1-flash-lite",
                "gemini-2.5-flash-lite",
                "gemini-3-flash-preview",
            ],
            handler.Requests.Select(static request => request.Model));

        string prompt = Assert.Single(handler.Requests.Select(static request => request.Prompt).Distinct());
        Assert.Contains("NHIỀU HÓA ĐƠN TRONG 1 ẢNH", prompt, StringComparison.Ordinal);
        Assert.Contains("BẮT BUỘC ĐỌC ĐỦ MÃ HÀNG TỪNG DÒNG", prompt, StringComparison.Ordinal);
        Assert.Contains("SẢN PHẨM VIẾT CHEN VÀO Ô/DÒNG \"CỘNG\"", prompt, StringComparison.Ordinal);
        Assert.Contains("KIỂM TRA PHÉP NHÂN TOÁN HỌC", prompt, StringComparison.Ordinal);
        Assert.Contains("ĐỐI CHIẾU TỔNG TIỀN TOÀN HÓA ĐƠN", prompt, StringComparison.Ordinal);
        Assert.Contains("mảng JSON trực tiếp", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeVoiceAsync_WhenModelsFail_ShouldUseEveryLegacyFallbackModel()
    {
        CapturingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        GeminiProductAiProvider provider = Create(handler);

        ProductAiResult result = await provider.AnalyzeVoiceAsync(
            "audio/webm",
            new byte[] { 0x1A, 0x45, 0xDF, 0xA3 },
            CancellationToken.None);

        Assert.Equal(ProductAiStatus.Unavailable, result.Status);
        Assert.Equal(
            [
                "gemini-2.5-pro",
                "gemini-2.5-flash",
                "gemini-2.5-flash-lite",
                "gemini-2.0-flash",
                "gemini-2.0-flash-lite",
                "gemini-flash-latest",
                "gemini-flash-lite-latest",
            ],
            handler.Requests.Select(static request => request.Model));
    }

    [Fact]
    public async Task AnalyzeVoiceAsync_ShouldSendFullLegacyPromptUsingRuntimeVocabulary()
    {
        CapturingHandler handler = new(_ => GeminiResponse("""
            {"transcript":"tìm plc","keyword":"PLC","intent":"search_product","filters":{"brand":null,"type":"PLC","code":null}}
            """));
        GeminiProductAiProvider provider = Create(handler);

        ProductAiResult result = await provider.AnalyzeVoiceAsync(
            "audio/webm",
            new byte[] { 0x1A, 0x45, 0xDF, 0xA3 },
            CancellationToken.None);

        Assert.Equal(ProductAiStatus.Success, result.Status);
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Contains("CƠ SỞ DỮ LIỆU ĐANG CÓ SẴN", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("BẢNG ÁNH XẠ CÁCH ĐỌC LÓNG", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("Giữ lại thông số kỹ thuật chi tiết", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("15. Người dùng nói", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("export_history", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("Siemens", request.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeVoiceAsync_WhenProviderReturnsPlainText_ShouldReturnBoundedLegacyFallback()
    {
        const string transcript = "tìm PLC Siemens";
        CapturingHandler handler = new(_ => GeminiResponse(transcript));
        GeminiProductAiProvider provider = Create(handler);

        ProductAiResult result = await provider.AnalyzeVoiceAsync(
            "audio/webm",
            new byte[] { 0x1A, 0x45, 0xDF, 0xA3 },
            CancellationToken.None);

        Assert.Equal(ProductAiStatus.Success, result.Status);
        Assert.Equal(transcript, result.Payload.GetProperty("transcript").GetString());
        Assert.Equal("PLC Siemens", result.Payload.GetProperty("keyword").GetString());
        JsonElement filters = result.Payload.GetProperty("filters");
        Assert.Equal(JsonValueKind.Null, filters.GetProperty("brand").ValueKind);
        Assert.Single(handler.Requests);
    }

    private static HttpResponseMessage GeminiResponse(string generatedText) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = generatedText } } } },
            },
        }),
    };

    private static GeminiProductAiProvider Create(HttpMessageHandler handler) => new(
        new FakeHttpClientFactory(new HttpClient(handler)),
        Options.Create(new ExternalServicesOptions { GeminiApiKey = "synthetic-test-key" }),
        NullLogger<GeminiProductAiProvider>.Instance);

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CapturingHandler(
        Func<CapturedRequest, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using JsonDocument document = JsonDocument.Parse(body);
            string prompt = document.RootElement
                .GetProperty("contents")[0]
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString()!;
            string model = ExtractModel(request.RequestUri!);
            CapturedRequest captured = new(model, prompt);
            Requests.Add(captured);
            HttpResponseMessage response = responder(captured);
            response.Content ??= new StringContent(string.Empty, Encoding.UTF8, "text/plain");
            return response;
        }

        private static string ExtractModel(Uri uri)
        {
            const string marker = "/models/";
            string path = uri.AbsolutePath;
            int start = path.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            int end = path.IndexOf(":generateContent", start, StringComparison.Ordinal);
            return Uri.UnescapeDataString(path[start..end]);
        }
    }

    private sealed record CapturedRequest(string Model, string Prompt);
}
