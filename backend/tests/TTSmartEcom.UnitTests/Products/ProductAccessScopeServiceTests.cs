using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Domain.Products;
using TTSmartEcom.Domain.Stations;

namespace TTSmartEcom.UnitTests.Products;

public sealed class ProductAccessScopeServiceTests
{
    private const string AllowedProductId = "507f191e810c19729de860ea";
    private const string DeniedProductId = "507f191e810c19729de860eb";
    private const string StationId = "507f191e810c19729de860ec";
    private const string SecondStationId = "507f191e810c19729de860ed";

    [Fact]
    public async Task ListAsync_WhenCustomerHasAssignedStation_PassesStationProductAllowlist()
    {
        FakeProductRepository products = new();
        ProductCatalogReadService service = Service(products);

        await service.ListAsync(
            new Dictionary<string, string?>(),
            new ProductViewer("customer", [StationId]),
            CancellationToken.None);

        ProductListQuery query = Assert.IsType<ProductListQuery>(products.LastQuery);
        Assert.Equal([AllowedProductId], query.AllowedProductIds);
        Assert.False(query.IncludePrivate);
        Assert.True(query.Display);
    }

    [Fact]
    public async Task ListAsync_WhenCustomerSelectsAssignedStation_UsesOnlyThatStation()
    {
        FakeProductRepository products = new();
        ProductCatalogReadService service = Service(products);

        await service.ListAsync(
            new Dictionary<string, string?> { ["stationId"] = StationId },
            new ProductViewer("customer", [StationId, SecondStationId]),
            CancellationToken.None);

        ProductListQuery query = Assert.IsType<ProductListQuery>(products.LastQuery);
        Assert.Equal([AllowedProductId], query.AllowedProductIds);
    }

    [Fact]
    public async Task ListAsync_WhenCustomerSelectsUnassignedStation_ReturnsLegacyForbiddenError()
    {
        FakeProductRepository products = new();
        ProductCatalogReadService service = Service(products);

        TTSmartEcom.Application.Common.Errors.ApplicationException error = await Assert.ThrowsAsync<
            TTSmartEcom.Application.Common.Errors.ApplicationException>(() => service.ListAsync(
                new Dictionary<string, string?> { ["stationId"] = SecondStationId },
                new ProductViewer("customer", [StationId]),
                CancellationToken.None));

        Assert.Equal(403, error.Error.HttpStatus);
        Assert.Equal("Bạn không có quyền truy cập trạm này.", error.Error.ClientMessage);
        Assert.Null(products.LastQuery);
    }

    [Fact]
    public async Task ListAsync_WhenAssignedStationIdIsInvalid_ReturnsLegacyBadRequestError()
    {
        FakeProductRepository products = new();
        ProductCatalogReadService service = Service(products);

        TTSmartEcom.Application.Common.Errors.ApplicationException error = await Assert.ThrowsAsync<
            TTSmartEcom.Application.Common.Errors.ApplicationException>(() => service.ListAsync(
                new Dictionary<string, string?> { ["stationId"] = "assigned-but-invalid" },
                new ProductViewer("customer", ["assigned-but-invalid"]),
                CancellationToken.None));

        Assert.Equal(400, error.Error.HttpStatus);
        Assert.Equal("Mã trạm không hợp lệ.", error.Error.ClientMessage);
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductIsOutsideAssignedStations_DoesNotQueryProductStore()
    {
        FakeProductRepository products = new();
        ProductCatalogReadService service = Service(products);

        ProductRecord? result = await service.GetByIdAsync(
            DeniedProductId,
            new ProductViewer("customer", [StationId]),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, products.FindByIdCalls);
    }

    [Fact]
    public async Task FetchByIdsAsync_WhenCustomerHasAssignedStation_RemovesDisallowedIds()
    {
        FakeProductRepository products = new();
        ProductCatalogReadService service = Service(products);

        (bool valid, IReadOnlyList<ProductRecord> values) = await service.FetchByIdsAsync(
            [AllowedProductId, DeniedProductId],
            new ProductViewer("customer", [StationId]),
            CancellationToken.None);

        Assert.True(valid);
        Assert.Equal([AllowedProductId], products.LastIds);
        Assert.Equal(AllowedProductId, Assert.Single(values).Id);
    }

    [Fact]
    public async Task PublicProductReads_RedactPrivatePricingEvenForPrivilegedViewer()
    {
        FakeProductRepository products = new();
        ProductCatalogReadService service = Service(products);
        ProductViewer admin = new("admin");

        await service.GetByIdAsync(AllowedProductId, admin, CancellationToken.None);
        Assert.False(products.LastIncludePrivate);

        await service.FetchByIdsAsync([AllowedProductId], admin, CancellationToken.None);
        Assert.False(products.LastIncludePrivate);

        await service.GetByIdAsync(
            AllowedProductId, admin, CancellationToken.None, includePrivate: true);
        Assert.True(products.LastIncludePrivate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("customer")]
    public async Task ListAsync_WhenViewerHasNoAssignedStation_KeepsLegacyPublicScope(string? role)
    {
        FakeProductRepository products = new();
        ProductCatalogReadService service = Service(products);

        await service.ListAsync(
            new Dictionary<string, string?>(),
            role is null ? null : new ProductViewer(role, []),
            CancellationToken.None);

        ProductListQuery query = Assert.IsType<ProductListQuery>(products.LastQuery);
        Assert.Null(query.AllowedProductIds);
        Assert.False(query.IncludePrivate);
        Assert.True(query.Display);
    }

    private static ProductCatalogReadService Service(FakeProductRepository products) =>
        new(products, new ProductAccessScopeService(new FakeStationRepository()));

    private sealed class FakeProductRepository : IProductCatalogRepository
    {
        public ProductListQuery? LastQuery { get; private set; }
        public IReadOnlyCollection<string>? LastIds { get; private set; }
        public int FindByIdCalls { get; private set; }
        public bool? LastIncludePrivate { get; private set; }

        public Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            ProductRecord[] values = query.AllowedProductIds is null
                ? [Product(AllowedProductId), Product(DeniedProductId)]
                : query.AllowedProductIds.Select(Product).ToArray();
            return Task.FromResult(new ProductPage(values.Length, query.Page, query.Limit, values));
        }

        public Task<ProductRecord?> FindByIdAsync(
            string id,
            bool includePrivate,
            CancellationToken cancellationToken)
        {
            FindByIdCalls++;
            LastIncludePrivate = includePrivate;
            return Task.FromResult<ProductRecord?>(Product(id));
        }

        public Task<IReadOnlyList<ProductRecord>> FindByIdsAsync(
            IReadOnlyCollection<string> ids,
            bool includePrivate,
            CancellationToken cancellationToken)
        {
            LastIds = ids.ToArray();
            LastIncludePrivate = includePrivate;
            return Task.FromResult<IReadOnlyList<ProductRecord>>(ids.Select(Product).ToArray());
        }

        public Task<IReadOnlyList<ProductTypeRecord>> ListTypesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProductTypeRecord>>([]);

        private static ProductRecord Product(string id) => new(
            id,
            null,
            "Sản phẩm kiểm thử",
            null,
            true,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            null,
            [],
            0,
            [],
            0,
            0,
            0,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false);
    }

    private sealed class FakeStationRepository : IStationRepository
    {
        public Task<IReadOnlyList<Station>> FindByIdsAsync(
            IReadOnlyList<string> ids,
            bool publicProjection,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Station>>(ids.Contains(StationId, StringComparer.Ordinal)
                ? [new Station(StationId, "Trạm", null, "TRAM-01", true, null, [AllowedProductId])]
                : ids.Contains(SecondStationId, StringComparer.Ordinal)
                    ? [new Station(SecondStationId, "Trạm 2", null, "TRAM-02", true, null, [DeniedProductId])]
                    : []);

        public Task<StationPage> ListAsync(int page, int limit, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> FindByIdAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> FindByCodeAsync(string code, bool publicProjection, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Station>> FindByCodesAsync(IReadOnlyList<string> codes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> CreateAsync(NewStationData station, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> UpdateAsync(string id, UpdateStationData station, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> UpdateProductsAsync(string id, IReadOnlyList<string> productIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> UpdateImageAsync(string id, string imageUrl, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> RemoveImageAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
