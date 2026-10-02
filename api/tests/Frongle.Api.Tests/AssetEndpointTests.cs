using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Frongle.Api.Tests;

[Collection(PostgresTests.Name)]
public sealed class AssetEndpointTests(PostgresFixture database) : IDisposable
{
    private const string Manager = "maintenance-manager";
    private const string WorkTeam = "work-team";

    private readonly PostgresApiFactory _factory = new(database);
    private readonly string _tenant = $"tenant-{Guid.NewGuid():N}";

    public void Dispose() => _factory.Dispose();

    private HttpClient As(string role, string? tenant = null) => _factory.CreateClientFor(tenant ?? _tenant, role);

    // Manukau covers longitude 174.85 to 174.86 and latitude -37.05 to -37.04. Otara is the square to its east.
    private async Task GivenAreas(string? tenant = null)
    {
        await As(Manager, tenant).PostAsJsonAsync("/api/areas", new { code = "MN", name = "Manukau", boundary = Shapes.Square(174.85, -37.05) });
        await As(Manager, tenant).PostAsJsonAsync("/api/areas", new { code = "OT", name = "Otara", boundary = Shapes.Square(174.86, -37.05) });
    }

    private const double InManukauLongitude = 174.855;
    private const double InOtaraLongitude = 174.865;
    private const double Latitude = -37.045;

    private sealed record AssetView(Guid Id, string FriendlyId, string Type, string AreaCode, double Latitude, double Longitude);

    private static Task<HttpResponseMessage> CreateAsset(HttpClient client, string type, double longitude, double latitude) =>
        client.PostAsJsonAsync("/api/assets", new { type, latitude, longitude });

    private static async Task<AssetView> CreateAssetOk(HttpClient client, string type, double longitude, double latitude)
    {
        var response = await CreateAsset(client, type, longitude, latitude);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssetView>())!;
    }

    [Fact]
    public async Task Creating_an_asset_gives_it_a_friendly_id_from_its_area_type_and_the_next_number()
    {
        await GivenAreas();

        var first = await CreateAssetOk(As(WorkTeam), "LightPost", InManukauLongitude, Latitude);
        var second = await CreateAssetOk(As(Manager), "LightPost", InManukauLongitude, Latitude);
        var otherType = await CreateAssetOk(As(WorkTeam), "TelephonePole", InManukauLongitude, Latitude);
        var otherArea = await CreateAssetOk(As(WorkTeam), "LightPost", InOtaraLongitude, Latitude);

        Assert.Equal(["MN-LP-00001", "MN-LP-00002", "MN-TP-00001", "OT-LP-00001"], [first.FriendlyId, second.FriendlyId, otherType.FriendlyId, otherArea.FriendlyId]);
        Assert.Equal(("LightPost", "MN", InManukauLongitude, Latitude), (first.Type, first.AreaCode, first.Longitude, first.Latitude));
    }

    [Theory]
    [InlineData("LightPost", "LP")]
    [InlineData("StreetSign", "SS")]
    [InlineData("TelephonePole", "TP")]
    [InlineData("TrafficLight", "TL")]
    public async Task Each_asset_type_has_its_own_code(string type, string code)
    {
        await GivenAreas();

        var asset = await CreateAssetOk(As(WorkTeam), type, InManukauLongitude, Latitude);

        Assert.Equal($"MN-{code}-00001", asset.FriendlyId);
    }

    [Fact]
    public async Task Numbers_start_again_for_each_tenant()
    {
        await GivenAreas();
        var other = $"tenant-{Guid.NewGuid():N}";
        await GivenAreas(other);
        await CreateAssetOk(As(WorkTeam), "LightPost", InManukauLongitude, Latitude);

        var asset = await CreateAssetOk(As(WorkTeam, other), "LightPost", InManukauLongitude, Latitude);

        Assert.Equal("MN-LP-00001", asset.FriendlyId);
    }

    [Fact]
    public async Task Two_people_creating_assets_at_the_same_time_never_get_the_same_number()
    {
        await GivenAreas();

        var created = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => CreateAssetOk(As(WorkTeam), "LightPost", InManukauLongitude, Latitude)));

        Assert.Equal(Enumerable.Range(1, 12).Select(n => $"MN-LP-{n:D5}"), created.Select(a => a.FriendlyId).Order());
    }

    [Fact]
    public async Task An_asset_outside_every_area_is_refused()
    {
        await GivenAreas();

        var response = await CreateAsset(As(WorkTeam), "LightPost", 175.5, -36.5);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("outside every area", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Lamppost", InManukauLongitude, Latitude)]
    [InlineData("LightPost", 181.0, Latitude)]
    [InlineData("LightPost", InManukauLongitude, -91.0)]
    public async Task An_unknown_type_or_a_position_off_the_earth_is_refused(string type, double longitude, double latitude)
    {
        await GivenAreas();

        var response = await CreateAsset(As(WorkTeam), type, longitude, latitude);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_asset_can_be_read_by_its_internal_id_but_only_in_its_own_tenant()
    {
        await GivenAreas();
        var asset = await CreateAssetOk(As(WorkTeam), "LightPost", InManukauLongitude, Latitude);

        var found = await As(Manager).GetFromJsonAsync<AssetView>($"/api/assets/{asset.Id}");
        var stranger = await As(Manager, $"tenant-{Guid.NewGuid():N}").GetAsync($"/api/assets/{asset.Id}");

        Assert.Equal("MN-LP-00001", found!.FriendlyId);
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
    }

    [Fact]
    public async Task Assets_are_listed_for_the_part_of_the_map_that_the_user_looks_at()
    {
        await GivenAreas();
        await CreateAssetOk(As(WorkTeam), "LightPost", InManukauLongitude, Latitude);
        await CreateAssetOk(As(WorkTeam), "StreetSign", InOtaraLongitude, Latitude);

        var manukauOnly = await As(WorkTeam).GetFromJsonAsync<List<AssetView>>("/api/assets?west=174.85&south=-37.05&east=174.859&north=-37.04");
        var both = await As(WorkTeam).GetFromJsonAsync<List<AssetView>>("/api/assets?west=174.85&south=-37.05&east=174.87&north=-37.04");
        var other = await As(WorkTeam, $"tenant-{Guid.NewGuid():N}").GetFromJsonAsync<List<AssetView>>("/api/assets?west=174.85&south=-37.05&east=174.87&north=-37.04");

        Assert.Equal(["MN-LP-00001"], manukauOnly!.Select(a => a.FriendlyId));
        Assert.Equal(["MN-LP-00001", "OT-SS-00001"], both!.Select(a => a.FriendlyId).Order());
        Assert.Empty(other!);
    }

    [Fact]
    public async Task Listing_assets_needs_a_valid_map_view()
    {
        var response = await As(WorkTeam).GetAsync("/api/assets?west=174.9&south=-37.05&east=174.8&north=-37.04");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_area_that_has_assets_cannot_be_deleted()
    {
        await GivenAreas();
        await CreateAssetOk(As(WorkTeam), "LightPost", InManukauLongitude, Latitude);
        var areas = await As(Manager).GetFromJsonAsync<List<JsonElement>>("/api/areas");
        var manukau = areas!.Single(a => a.GetProperty("code").GetString() == "MN").GetProperty("id").GetGuid();
        var otara = areas!.Single(a => a.GetProperty("code").GetString() == "OT").GetProperty("id").GetGuid();

        var refused = await As(Manager).DeleteAsync($"/api/areas/{manukau}");
        var allowed = await As(Manager).DeleteAsync($"/api/areas/{otara}");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
    }

    [Fact]
    public async Task A_boundary_change_that_leaves_an_asset_outside_its_area_is_refused_and_names_the_asset()
    {
        await GivenAreas();
        await CreateAssetOk(As(WorkTeam), "LightPost", InManukauLongitude, Latitude);
        var areas = await As(Manager).GetFromJsonAsync<List<JsonElement>>("/api/areas");
        var manukau = areas!.Single(a => a.GetProperty("code").GetString() == "MN").GetProperty("id").GetGuid();

        var shrunk = await As(Manager).PutAsJsonAsync($"/api/areas/{manukau}", new { name = "Manukau", boundary = Shapes.Square(174.85, -37.05, 0.003) });

        Assert.Equal(HttpStatusCode.Conflict, shrunk.StatusCode);
        Assert.Contains("MN-LP-00001", await shrunk.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_boundary_change_that_keeps_every_asset_inside_is_allowed()
    {
        await GivenAreas();
        await CreateAssetOk(As(WorkTeam), "LightPost", InManukauLongitude, Latitude);
        var areas = await As(Manager).GetFromJsonAsync<List<JsonElement>>("/api/areas");
        var manukau = areas!.Single(a => a.GetProperty("code").GetString() == "MN").GetProperty("id").GetGuid();

        var response = await As(Manager).PutAsJsonAsync($"/api/areas/{manukau}", new { name = "Manukau", boundary = Shapes.Square(174.85, -37.05, 0.0095) });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
