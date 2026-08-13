using System.Text.Json;
using TTSmartEcom.Application.Abstractions.Catalog;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Domain.Catalog;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.UnitTests.Products;

public sealed class ProductInvoiceMatchingServiceTests
{
    [Fact]
    public async Task MatchAsync_ShouldMatchExactNormalizedCodeAndFillLegacyMetadata()
    {
        ProductRecord product = new(
            "product-1", "PLC", "PLC S7-1200", null, true, "S7-1200", "10%", true, "Siemens", null,
            null, [], null, [], 0, [], 0, 0, 0, null, null, null, null, null, null, null, null, null, true);
        ProductInvoiceMatchingService service = new(
            new FakeProducts([product]),
            new FakeCatalog([new BrandRecord("brand-1", "Siemens")]));
        using JsonDocument json = JsonDocument.Parse("""
            [{"stt":"1","rawScannedName":"PLC S7-1200","code":"S7-1200","brand":"siemens","quantity":1,"price":1000}]
            """);

        ProductInvoiceMatchResult? result = await service.MatchAsync(json.RootElement, CancellationToken.None);

        IReadOnlyDictionary<string, object?> item = Assert.Single(result!.Items);
        Assert.Equal("MATCHED", item["matchStatus"]);
        Assert.Equal("product-1", item["matchedProductId"]);
        Assert.Equal("Siemens", item["brand"]);
        Assert.Equal(false, item["brandIsNew"]);
        Assert.Equal("10%", item["vat"]);
    }

    [Fact]
    public async Task MatchAsync_ShouldRejectExcessiveProviderCollection()
    {
        ProductInvoiceMatchingService service = new(new FakeProducts([]), new FakeCatalog([]));
        string payload = "[" + string.Join(',', Enumerable.Repeat("{}", 501)) + "]";
        using JsonDocument json = JsonDocument.Parse(payload);

        Assert.Null(await service.MatchAsync(json.RootElement, CancellationToken.None));
    }

    [Fact]
    public async Task MatchAsync_ShouldNormalizeRepeatedPurchaseOrderPrefix()
    {
        ProductInvoiceMatchingService service = new(new FakeProducts([]), new FakeCatalog([]));
        using JsonDocument json = JsonDocument.Parse("""
            [
              {"rawScannedName":"Relay RXM2AB2BD","code":"PO123456 RXM2AB2BD"},
              {"rawScannedName":"Contactor 3RT2026-1BB40","code":"PO123456 3RT2026-1BB40"}
            ]
            """);

        ProductInvoiceMatchResult? result = await service.MatchAsync(json.RootElement, CancellationToken.None);

        Assert.Equal(["RXM2AB2BD", "3RT2026-1BB40"],
            result!.Items.Select(item => Assert.IsType<string>(item["rawScannedCode"])).ToArray());
    }

    [Fact]
    public async Task MatchAsync_ShouldAutoSelectSafeShortCodeAndRequireReview()
    {
        ProductRecord product = Product("short-code", "Khởi động từ 3RT2026-1BB40 220V", "3RT2026-1BB40", "Siemens");
        ProductInvoiceMatchingService service = new(
            new FakeProducts([product]),
            new FakeCatalog([new BrandRecord("brand-1", "Siemens")]));
        using JsonDocument json = JsonDocument.Parse("""
            [{"rawScannedName":"Khởi động từ 3RT2026-1BB40 220V","code":"3RT2026-1BB40 220V","brand":"Siemens","confidence":"high"}]
            """);

        ProductInvoiceMatchResult? result = await service.MatchAsync(json.RootElement, CancellationToken.None);

        IReadOnlyDictionary<string, object?> item = Assert.Single(result!.Items);
        Assert.Equal("POSSIBLE_MATCH", item["matchStatus"]);
        Assert.Equal("short-code", item["matchedProductId"]);
        Assert.Equal(["short-code"], Assert.IsType<string[]>(item["candidateProductIds"]));
        Assert.Equal(true, item["autoSelected"]);
        Assert.Equal(true, item["requiresReview"]);
        Assert.Equal("medium", item["confidence"]);
    }

    [Theory]
    [InlineData("low", "Siemens")]
    [InlineData("high", "Schneider Electric")]
    public async Task MatchAsync_ShouldNotAutoSelectUnsafeCoreCandidate(string confidence, string brand)
    {
        ProductRecord product = Product("review-only", "Khởi động từ 3RT2026-1BB40 220V", "3RT2026-1BB40", "Siemens");
        ProductInvoiceMatchingService service = new(
            new FakeProducts([product]),
            new FakeCatalog([new BrandRecord("brand-1", "Siemens"), new BrandRecord("brand-2", "Schneider Electric")]));
        using JsonDocument json = JsonDocument.Parse($$"""
            [{"rawScannedName":"Khởi động từ 3RT2026-1BB40 220V","code":"3RT2026-1BB40 220V","brand":"{{brand}}","confidence":"{{confidence}}"}]
            """);

        IReadOnlyDictionary<string, object?> item = Assert.Single(
            (await service.MatchAsync(json.RootElement, CancellationToken.None))!.Items);

        Assert.Null(item["matchedProductId"]);
        Assert.Equal(["review-only"], Assert.IsType<string[]>(item["candidateProductIds"]));
        Assert.Equal(false, item["autoSelected"]);
        Assert.Equal("low", item["confidence"]);
    }

    [Fact]
    public async Task MatchAsync_ShouldExplainConflictingCoreSpecifications()
    {
        ProductRecord product = Product("wrong-voltage", "Khởi động từ 3RT2026-1BB40 110V", "3RT2026-1BB40", "Siemens");
        ProductInvoiceMatchingService service = new(new FakeProducts([product]), new FakeCatalog([]));
        using JsonDocument json = JsonDocument.Parse("""
            [{"rawScannedName":"Khởi động từ 3RT2026-1BB40 220V","code":"3RT2026-1BB40 220V","brand":"Siemens","confidence":"high"}]
            """);

        IReadOnlyDictionary<string, object?> item = Assert.Single(
            (await service.MatchAsync(json.RootElement, CancellationToken.None))!.Items);

        Assert.Null(item["matchedProductId"]);
        Assert.Contains("thông số DB khác", Assert.IsType<string>(item["matchReason"]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MatchAsync_ShouldUseTypeSynonymsAndExactSpecificationSetForFallback()
    {
        ProductRecord[] products =
        [
            Product("relay-220", "Relay bảo vệ 220V", string.Empty, "Siemens"),
            Product("relay-24", "Relay bảo vệ 24V", string.Empty, "Siemens"),
            Product("valve-220", "Van điện từ 220V", string.Empty, "Siemens"),
        ];
        ProductInvoiceMatchingService service = new(new FakeProducts(products), new FakeCatalog([]));
        using JsonDocument json = JsonDocument.Parse("""
            [{"rawScannedName":"Rơ le bảo vệ 220V","code":""}]
            """);

        IReadOnlyDictionary<string, object?> item = Assert.Single(
            (await service.MatchAsync(json.RootElement, CancellationToken.None))!.Items);

        Assert.Equal("POSSIBLE_MATCH", item["matchStatus"]);
        Assert.Equal(["relay-220"], Assert.IsType<string[]>(item["candidateProductIds"]));
        Assert.Equal("low", item["confidence"]);
    }

    private static ProductRecord Product(string id, string name, string code, string brand) => new(
        id, "Thiết bị", name, null, true, code, "10%", true, brand, null,
        null, [], null, [], 0, [], 0, 0, 0, null, null, null, null, null, null, null, null, null, true);

    private sealed class FakeProducts(IReadOnlyList<ProductRecord> values) : IProductCatalogRepository
    {
        public Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken cancellationToken) => Task.FromResult(new ProductPage(values.Count, 1, 10_000, values));
        public Task<ProductRecord?> FindByIdAsync(string id, bool includePrivate, CancellationToken cancellationToken) => Task.FromResult<ProductRecord?>(null);
        public Task<IReadOnlyList<ProductRecord>> FindByIdsAsync(IReadOnlyCollection<string> ids, bool includePrivate, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductRecord>>([]);
        public Task<IReadOnlyList<ProductTypeRecord>> ListTypesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductTypeRecord>>([]);
    }

    private sealed class FakeCatalog(IReadOnlyList<BrandRecord> brands) : ICatalogRepository
    {
        public Task<IReadOnlyList<BrandRecord>> ListBrandsAsync(CancellationToken cancellationToken) => Task.FromResult(brands);
        public Task<IReadOnlyList<string>> ListSectionNamesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<SectionDocumentRecord?> GetSectionDocumentAsync(CancellationToken cancellationToken) => Task.FromResult<SectionDocumentRecord?>(null);
        public Task<IReadOnlyList<string>?> GetSectionValuesAsync(string sectionName, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>?>(null);
        public Task<IReadOnlyDictionary<string, string?>> GetSectionImagesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>());
        public Task<ManageRecord?> GetManageAsync(CancellationToken cancellationToken) => Task.FromResult<ManageRecord?>(null);
        public Task<IReadOnlyList<ManagePolicyRecord>> GetPoliciesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ManagePolicyRecord>>([]);
    }
}
