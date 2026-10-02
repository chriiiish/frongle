using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frongle.Api.Tests;

[Collection(PostgresTests.Name)]
public sealed class AssetRetagTests(PostgresFixture database) : IDisposable
{
    private const string Manager = "maintenance-manager";
    private const string WorkTeam = "work-team";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgresApiFactory _factory = new(database);
    private readonly string _tenant = $"tenant-{Guid.NewGuid():N}";

    public void Dispose() => _factory.Dispose();

    private HttpClient As(string role, string? tenant = null) => _factory.CreateClientFor(tenant ?? _tenant, role);

    private sealed record AssetView(
        Guid Id, string FriendlyId, string AreaCode, double Latitude, double Longitude, bool NeedsRetag, string[] FormerFriendlyIds, uint Version);

    private sealed record HistoryView(string EntityType, string EntityId, string Operation, string Field, string? OldValue, string? NewValue, string ChangedBy, string? ChangedByName, DateTimeOffset ChangedAt);

    // Manukau is longitude 174.85 to 174.86, and Otara is the square to its east.
    private const double InManukau = 174.855;
    private const double InOtara = 174.865;
    private const double Latitude = -37.045;

    private async Task GivenAreas(string? tenant = null)
    {
        await As(Manager, tenant).PostAsJsonAsync("/api/areas", new { code = "MN", name = "Manukau", boundary = Shapes.Square(174.85, -37.05) });
        await As(Manager, tenant).PostAsJsonAsync("/api/areas", new { code = "OT", name = "Otara", boundary = Shapes.Square(174.86, -37.05) });
    }

    private async Task<AssetView> GivenAnAsset(string? tenant = null)
    {
        var response = await As(WorkTeam, tenant).PostAsJsonAsync("/api/assets", new { type = "LightPost", latitude = Latitude, longitude = InManukau });
        return (await response.Content.ReadFromJsonAsync<AssetView>(Json))!;
    }

    private static Task<HttpResponseMessage> Move(HttpClient client, AssetView asset, double longitude, double latitude, uint? version = null) =>
        client.PutAsJsonAsync($"/api/assets/{asset.Id}/location", new { latitude, longitude, version = version ?? asset.Version });

    private static async Task<AssetView> MoveOk(HttpClient client, AssetView asset, double longitude, double latitude)
    {
        var response = await Move(client, asset, longitude, latitude);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssetView>(Json))!;
    }

    [Fact]
    public async Task Moving_an_asset_within_its_area_keeps_its_friendly_id()
    {
        await GivenAreas();
        var asset = await GivenAnAsset();

        var moved = await MoveOk(As(WorkTeam), asset, 174.8551, -37.0451);

        Assert.Equal(("MN-LP-00001", false, 0), (moved.FriendlyId, moved.NeedsRetag, moved.FormerFriendlyIds.Length));
        Assert.Equal((-37.0451, 174.8551), (moved.Latitude, moved.Longitude));
        Assert.NotEqual(asset.Version, moved.Version);
    }

    [Fact]
    public async Task Moving_an_asset_into_another_area_gives_it_a_new_friendly_id_and_marks_it_for_retagging()
    {
        await GivenAreas();
        var asset = await GivenAnAsset();

        var moved = await MoveOk(As(Manager), asset, InOtara, Latitude);

        Assert.Equal(("OT-LP-00001", "OT", true), (moved.FriendlyId, moved.AreaCode, moved.NeedsRetag));
        Assert.Equal(["MN-LP-00001"], moved.FormerFriendlyIds);
    }

    [Fact]
    public async Task A_number_is_never_reused_when_an_asset_moves_back()
    {
        await GivenAreas();
        var asset = await GivenAnAsset();
        var inOtara = await MoveOk(As(Manager), asset, InOtara, Latitude);

        var back = await MoveOk(As(Manager), inOtara, InManukau, Latitude);

        Assert.Equal("MN-LP-00002", back.FriendlyId);
        Assert.Equal(["MN-LP-00001", "OT-LP-00001"], back.FormerFriendlyIds.Order());
    }

    [Fact]
    public async Task An_asset_cannot_move_outside_every_area_or_off_the_earth_or_on_a_stale_version()
    {
        await GivenAreas();
        var asset = await GivenAnAsset();

        var outside = await Move(As(Manager), asset, 175.5, -36.5);
        var offTheEarth = await Move(As(Manager), asset, 181, Latitude);
        await MoveOk(As(Manager), asset, 174.8551, Latitude);
        var stale = await Move(As(Manager), asset, 174.8552, Latitude);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, outside.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, offTheEarth.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task An_asset_of_another_tenant_cannot_be_moved_or_marked_as_retagged()
    {
        await GivenAreas();
        var asset = await GivenAnAsset();
        var stranger = As(Manager, $"tenant-{Guid.NewGuid():N}");

        var move = await Move(stranger, asset, 174.8551, Latitude);
        var retagged = await stranger.PostAsync($"/api/assets/{asset.Id}/retagged", null);

        Assert.Equal(HttpStatusCode.NotFound, move.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, retagged.StatusCode);
    }

    [Fact]
    public async Task Recording_that_the_new_tag_is_fitted_clears_the_retag_mark()
    {
        await GivenAreas();
        var moved = await MoveOk(As(Manager), await GivenAnAsset(), InOtara, Latitude);

        var response = await As(WorkTeam).PostAsync($"/api/assets/{moved.Id}/retagged", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var asset = (await response.Content.ReadFromJsonAsync<AssetView>(Json))!;
        Assert.Equal(("OT-LP-00001", false, "MN-LP-00001"), (asset.FriendlyId, asset.NeedsRetag, Assert.Single(asset.FormerFriendlyIds)));
    }

    [Fact]
    public async Task Search_finds_an_asset_by_part_of_its_friendly_id_or_by_a_former_one()
    {
        await GivenAreas();
        var moved = await MoveOk(As(Manager), await GivenAnAsset(), InOtara, Latitude);
        await GivenAnAsset();

        var byCurrent = await As(WorkTeam).GetFromJsonAsync<List<AssetView>>("/api/assets/search?q=ot-lp", Json);
        var byFormer = await As(WorkTeam).GetFromJsonAsync<List<AssetView>>("/api/assets/search?q=MN-LP-00001", Json);
        var other = await As(WorkTeam, $"tenant-{Guid.NewGuid():N}").GetFromJsonAsync<List<AssetView>>("/api/assets/search?q=LP", Json);

        Assert.Equal([moved.Id], byCurrent!.Select(a => a.Id));
        Assert.Equal([moved.Id], byFormer!.Select(a => a.Id));
        Assert.Empty(other!);
    }

    [Fact]
    public async Task Search_needs_some_text()
    {
        var response = await As(WorkTeam).GetAsync("/api/assets/search?q=%20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_history_of_an_asset_says_who_changed_what_and_when_for_the_asset_and_its_events()
    {
        await GivenAreas();
        var asset = await GivenAnAsset();
        var mover = As(Manager);
        mover.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "user-2");
        mover.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Grace Hopper");
        await MoveOk(mover, asset, InOtara, Latitude);
        await As(WorkTeam).PostAsJsonAsync($"/api/assets/{asset.Id}/events", new { type = "Installed", title = "Installed", occurredAt = DateTimeOffset.UtcNow.AddHours(-1) });

        var history = await As(Manager).GetFromJsonAsync<List<HistoryView>>($"/api/assets/{asset.Id}/history", Json);

        var friendlyId = Assert.Single(history!, h => h.Field == "FriendlyId" && h.Operation == "Updated");
        Assert.Equal(("Asset", "MN-LP-00001", "OT-LP-00001", "user-2", "Grace Hopper"), (friendlyId.EntityType, friendlyId.OldValue, friendlyId.NewValue, friendlyId.ChangedBy, friendlyId.ChangedByName));
        Assert.Contains(history!, h => h.EntityType == "AssetEvent" && h.Field == "Title" && h.NewValue == "Installed");
        Assert.Equal(history!.OrderByDescending(h => h.ChangedAt).Select(h => h.ChangedAt), history!.Select(h => h.ChangedAt));
    }

    [Fact]
    public async Task The_history_of_an_asset_of_another_tenant_finds_nothing()
    {
        await GivenAreas();
        var asset = await GivenAnAsset();

        var response = await As(Manager, $"tenant-{Guid.NewGuid():N}").GetAsync($"/api/assets/{asset.Id}/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
