using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Domain.Products;
using TTSmartEcom.Domain.Stations;

namespace TTSmartEcom.UnitTests.Products;

public sealed class ProductCatalogReadServiceListingTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("TRUE", false)]
    [InlineData("invalid", false)]
    public async Task ListAsync_ParsesAdjustedUsingLegacyExactTrueRule(string raw, bool expected)
    {
        FakeProductRepository repository = new();
        ProductCatalogReadService service = CreateService(repository);

        await service.ListAsync(
            new Dictionary<string, string?> { ["adjusted"] = raw },
            viewer: null,
            CancellationToken.None);

        ProductListQuery query = Assert.IsType<ProductListQuery>(repository.LastQuery);
        Assert.Equal(expected, query.Adjusted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ListAsync_WhenAdjustedIsMissingOrEmpty_PassesNull(string? raw)
    {
        FakeProductRepository repository = new();
        ProductCatalogReadService service = CreateService(repository);
        Dictionary<string, string?> query = raw is null
            ? []
            : new() { ["adjusted"] = raw };

        await service.ListAsync(query, viewer: null, CancellationToken.None);

        ProductListQuery captured = Assert.IsType<ProductListQuery>(repository.LastQuery);
        Assert.Null(captured.Adjusted);
    }

    [Fact]
    public async Task ListAsync_WhenAdjustedContainsWhitespace_PassesFalseLikeLegacy()
    {
        FakeProductRepository repository = new();
        ProductCatalogReadService service = CreateService(repository);

        await service.ListAsync(
            new Dictionary<string, string?> { ["adjusted"] = "   " },
            viewer: null,
            CancellationToken.None);

        ProductListQuery captured = Assert.IsType<ProductListQuery>(repository.LastQuery);
        Assert.False(captured.Adjusted);
    }

    [Fact]
    public async Task ListAsync_StillAppliesPublicDisplayScopeAlongsideAdjustedFilter()
    {
        FakeProductRepository repository = new();
        ProductCatalogReadService service = CreateService(repository);

        await service.ListAsync(
            new Dictionary<string, string?> { ["adjusted"] = "false", ["display"] = "false" },
            viewer: null,
            CancellationToken.None);

        ProductListQuery query = Assert.IsType<ProductListQuery>(repository.LastQuery);
        Assert.False(query.Adjusted);
        Assert.True(query.Display);
        Assert.False(query.IncludePrivate);
    }

    private static ProductCatalogReadService CreateService(FakeProductRepository repository) =>
        new(repository, new ProductAccessScopeService(new UnsupportedStationRepository()));

    private sealed class FakeProductRepository : IProductCatalogRepository
    {
        public ProductListQuery? LastQuery { get; private set; }

        public Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(new ProductPage(0, query.Page, query.Limit, []));
        }

        public Task<ProductRecord?> FindByIdAsync(
            string id,
            bool includePrivate,
            CancellationToken cancellationToken) =>
            Task.FromResult<ProductRecord?>(null);

        public Task<IReadOnlyList<ProductRecord>> FindByIdsAsync(
            IReadOnlyCollection<string> ids,
            bool includePrivate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProductRecord>>([]);

        public Task<IReadOnlyList<ProductTypeRecord>> ListTypesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProductTypeRecord>>([]);
    }

    private sealed class UnsupportedStationRepository : IStationRepository
    {
        public Task<IReadOnlyList<Station>> FindByIdsAsync(
            IReadOnlyList<string> ids,
            bool publicProjection,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Station>>([]);

        public Task<StationPage> ListAsync(
            int page,
            int limit,
            string? search,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Station?> FindByIdAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Station?> FindByCodeAsync(
            string code,
            bool publicProjection,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Station>> FindByCodesAsync(
            IReadOnlyList<string> codes,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Station?> CreateAsync(NewStationData station, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Station?> UpdateAsync(
            string id,
            UpdateStationData station,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Station?> UpdateProductsAsync(
            string id,
            IReadOnlyList<string> productIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Station?> UpdateImageAsync(
            string id,
            string imageUrl,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Station?> RemoveImageAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
