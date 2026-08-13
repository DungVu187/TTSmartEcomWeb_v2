using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Contracts.Stations;
using TTSmartEcom.Api.Controllers.Stations;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Domain.Stations;

namespace TTSmartEcom.UnitTests.Stations;

public sealed class StationLookupProjectionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Code_ReturnsBoundedLegacyProjectionWithoutUnmappedFields()
    {
        var repository = new FakeStationRepository([Station("station-1", "S1")]);
        StationController controller = CreateController(repository);

        IActionResult action = await controller.Code("S1", CancellationToken.None);

        OkObjectResult result = Assert.IsType<OkObjectResult>(action);
        using JsonDocument document = Serialize(result.Value);
        JsonElement station = document.RootElement;
        Assert.Equal("station-1", station.GetProperty("_id").GetString());
        Assert.Equal("station-1", station.GetProperty("id").GetString());
        Assert.Equal("S1", station.GetProperty("stationCode").GetString());
        Assert.Equal("S1", station.GetProperty("inviteCode").GetString());
        Assert.Equal("https://static.example/station.webp", station.GetProperty("imgUrl").GetString());
        Assert.Equal("product-1", station.GetProperty("productId")[0].GetString());
        Assert.False(station.TryGetProperty("imageUrl", out _));
        Assert.False(station.TryGetProperty("inviteSecret", out _));
        Assert.False(station.TryGetProperty("__v", out _));
        Assert.False(repository.LastPublicProjection);
    }

    [Fact]
    public async Task ByCodes_BoundsInputsAndReturnsTheSameAllowlistedProjection()
    {
        var repository = new FakeStationRepository(
        [
            Station("station-1", "S1"),
            Station("station-2", "S2"),
        ]);
        StationController controller = CreateController(repository);
        string longCode = new('A', 110);

        IActionResult action = await controller.ByCodes(
            $"  {longCode}  ,, S2,",
            CancellationToken.None);

        OkObjectResult result = Assert.IsType<OkObjectResult>(action);
        Assert.Collection(
            repository.LastCodes,
            value => Assert.Equal(new string('A', 100), value),
            value => Assert.Equal("S2", value));
        using JsonDocument document = Serialize(result.Value);
        JsonElement stations = document.RootElement;
        Assert.Equal(2, stations.GetArrayLength());
        Assert.Equal("station-1", stations[0].GetProperty("_id").GetString());
        Assert.Equal("S1", stations[0].GetProperty("inviteCode").GetString());
        Assert.False(stations[0].TryGetProperty("inviteSecret", out _));
        Assert.False(stations[0].TryGetProperty("imageUrl", out _));
    }

    [Fact]
    public async Task PublicLookup_StillExcludesInviteCode()
    {
        var repository = new FakeStationRepository([Station("station-1", "S1")]);
        StationController controller = CreateController(repository);

        IActionResult action = await controller.Public("S1", CancellationToken.None);

        OkObjectResult result = Assert.IsType<OkObjectResult>(action);
        using JsonDocument document = Serialize(result.Value);
        Assert.False(document.RootElement.TryGetProperty("inviteCode", out _));
        Assert.True(repository.LastPublicProjection);
    }

    [Fact]
    public async Task Search_ForwardsBothLegacyExactFiltersAndReturnsPublicProjection()
    {
        var repository = new FakeStationRepository([Station("station-1", "S1")]);
        StationController controller = CreateController(repository);

        IActionResult action = await controller.Search("Trạm S1", "S1", CancellationToken.None);

        OkObjectResult result = Assert.IsType<OkObjectResult>(action);
        using JsonDocument document = Serialize(result.Value);
        Assert.Equal("Trạm S1", repository.LastExactName);
        Assert.Equal("S1", repository.LastExactCode);
        Assert.False(document.RootElement.GetProperty("stations")[0].TryGetProperty("inviteCode", out _));
    }

    [Fact]
    public async Task List_ReturnsRawLegacyArrayWithoutPaginationEnvelope()
    {
        var repository = new FakeStationRepository([Station("station-1", "S1")]);
        StationController controller = CreateController(repository);

        IActionResult action = await controller.List(CancellationToken.None);

        OkObjectResult result = Assert.IsType<OkObjectResult>(action);
        using JsonDocument document = Serialize(result.Value);
        JsonElement stations = document.RootElement;
        Assert.Equal(JsonValueKind.Array, stations.ValueKind);
        Assert.Equal(1, stations.GetArrayLength());
        Assert.Equal("station-1", stations[0].GetProperty("_id").GetString());
        Assert.Equal("https://static.example/station.webp", stations[0].GetProperty("imgUrl").GetString());
        Assert.False(stations[0].TryGetProperty("total", out _));
        Assert.False(stations[0].TryGetProperty("page", out _));
        Assert.Equal(1, repository.LastPage);
        Assert.Equal(10000, repository.LastLimit);
        Assert.Null(repository.LastSearch);
    }

    [Fact]
    public async Task ByIds_BindsLegacyRequestObjectAndReturnsLegacyProjection()
    {
        var repository = new FakeStationRepository([Station("station-1", "S1")]);
        StationController controller = CreateController(repository);

        IActionResult action = await controller.ByIds(
            new StationIdsRequest(["station-1"]),
            CancellationToken.None);

        OkObjectResult result = Assert.IsType<OkObjectResult>(action);
        using JsonDocument document = Serialize(result.Value);
        JsonElement stations = document.RootElement;
        Assert.Equal(JsonValueKind.Array, stations.ValueKind);
        Assert.Equal("station-1", stations[0].GetProperty("_id").GetString());
        Assert.Equal("station-1", stations[0].GetProperty("id").GetString());
        Assert.Equal("https://static.example/station.webp", stations[0].GetProperty("imgUrl").GetString());
        Assert.Equal("product-1", stations[0].GetProperty("productId")[0].GetString());
        Assert.Equal(["station-1"], repository.LastIds);
        Assert.True(repository.LastPublicProjection);
    }

    [Fact]
    public void ByIds_RequestContractBindsIdsFromJsonBody()
    {
        StationIdsRequest? request = JsonSerializer.Deserialize<StationIdsRequest>(
            """{"ids":["station-1"]}""",
            JsonOptions);
        MethodInfo method = typeof(StationController).GetMethod(nameof(StationController.ByIds))!;
        ParameterInfo requestParameter = method.GetParameters()[0];

        Assert.NotNull(request);
        Assert.Equal(["station-1"], request.Ids);
        Assert.Equal(typeof(StationIdsRequest), requestParameter.ParameterType);
        Assert.NotNull(requestParameter.GetCustomAttribute<FromBodyAttribute>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task ByIds_RejectsMissingOrEmptyIds(int? count)
    {
        var repository = new FakeStationRepository([Station("station-1", "S1")]);
        StationController controller = CreateController(repository);
        StationIdsRequest? request = count is null
            ? null
            : new StationIdsRequest([]);

        IActionResult action = await controller.ByIds(request, CancellationToken.None);

        BadRequestObjectResult result = Assert.IsType<BadRequestObjectResult>(action);
        using JsonDocument document = Serialize(result.Value);
        Assert.Equal("Thiếu hoặc sai định dạng danh sách _id", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void ByCodes_IsMountedAsGetOnly()
    {
        MethodInfo method = typeof(StationController).GetMethod(nameof(StationController.ByCodes))!;

        HttpGetAttribute attribute = Assert.Single(method.GetCustomAttributes<HttpGetAttribute>());
        Assert.Equal("by-codes", attribute.Template);
        Assert.Empty(method.GetCustomAttributes<HttpPostAttribute>());
    }

    private static StationController CreateController(IStationRepository repository) => new(
        repository,
        null!,
        Options.Create(new ExternalServicesOptions()),
        null!,
        NullLogger<StationController>.Instance);

    private static JsonDocument Serialize(object? value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));

    private static Station Station(string id, string code) => new(
        id,
        $"Trạm {code}",
        "https://static.example/station.webp",
        code,
        true,
        "Đà Nẵng",
        ["product-1"]);

    private sealed class FakeStationRepository(IReadOnlyList<Station> values) : IStationRepository
    {
        public IReadOnlyList<string> LastCodes { get; private set; } = [];
        public bool LastPublicProjection { get; private set; }
        public IReadOnlyList<string> LastIds { get; private set; } = [];
        public int LastPage { get; private set; }
        public int LastLimit { get; private set; }
        public string? LastSearch { get; private set; }
        public string? LastExactName { get; private set; }
        public string? LastExactCode { get; private set; }

        public Task<IReadOnlyList<Station>> SearchExactAsync(
            string? name,
            string? code,
            CancellationToken cancellationToken)
        {
            LastExactName = name;
            LastExactCode = code;
            return Task.FromResult(values);
        }

        public Task<Station?> FindByCodeAsync(
            string code,
            bool publicProjection,
            CancellationToken cancellationToken)
        {
            LastPublicProjection = publicProjection;
            return Task.FromResult(values.FirstOrDefault(station => station.StationCode == code));
        }

        public Task<IReadOnlyList<Station>> FindByCodesAsync(
            IReadOnlyList<string> codes,
            CancellationToken cancellationToken)
        {
            LastCodes = codes;
            return Task.FromResult(values);
        }

        public Task<StationPage> ListAsync(
            int page,
            int limit,
            string? search,
            CancellationToken cancellationToken)
        {
            LastPage = page;
            LastLimit = limit;
            LastSearch = search;
            return Task.FromResult(new StationPage(values.Count, page, limit, values));
        }

        public Task<Station?> FindByIdAsync(
            string id,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Station>> FindByIdsAsync(
            IReadOnlyList<string> ids,
            bool publicProjection,
            CancellationToken cancellationToken)
        {
            LastIds = ids;
            LastPublicProjection = publicProjection;
            return Task.FromResult<IReadOnlyList<Station>>(values);
        }

        public Task<Station?> CreateAsync(
            NewStationData station,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Station?> UpdateAsync(
            string id,
            UpdateStationData station,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Station?> UpdateProductsAsync(
            string id,
            IReadOnlyList<string> productIds,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            string id,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Station?> UpdateImageAsync(
            string id,
            string imageUrl,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Station?> RemoveImageAsync(
            string id,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
