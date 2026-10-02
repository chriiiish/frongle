using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frongle.Domain;

namespace Frongle.Api.Tests;

[Collection(PostgresTests.Name)]
public sealed class EventEndpointTests(PostgresFixture database) : IDisposable
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

    private sealed record AssetView(Guid Id, string FriendlyId, string Status);
    private sealed record EventView(Guid Id, Guid AssetId, EventType Type, string Title, string? Notes, DateTimeOffset OccurredAt, uint Version);

    private async Task<Guid> GivenAnAsset(string? tenant = null)
    {
        await As(Manager, tenant).PostAsJsonAsync("/api/areas", new { code = "MN", name = "Manukau", boundary = Shapes.Square(174.85, -37.05) });
        var response = await As(WorkTeam, tenant).PostAsJsonAsync("/api/assets", new { type = "LightPost", latitude = -37.045, longitude = 174.855 });
        return (await response.Content.ReadFromJsonAsync<AssetView>(Json))!.Id;
    }

    private static object NewEvent(string type = "Installed", string title = "Installed new post", string? notes = null, DateTimeOffset? at = null) =>
        new { type, title, notes, occurredAt = at ?? DateTimeOffset.UtcNow.AddHours(-1) };

    private static Task<HttpResponseMessage> AddEvent(HttpClient client, Guid assetId, object body) =>
        client.PostAsJsonAsync($"/api/assets/{assetId}/events", body);

    private static async Task<EventView> AddEventOk(HttpClient client, Guid assetId, object body)
    {
        var response = await AddEvent(client, assetId, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EventView>(Json))!;
    }

    private static async Task<string> StatusOf(HttpClient client, Guid assetId) =>
        (await client.GetFromJsonAsync<AssetView>($"/api/assets/{assetId}", Json))!.Status;

    [Fact]
    public async Task An_asset_with_no_events_is_pending_installation_and_the_installed_event_puts_it_in_service()
    {
        var assetId = await GivenAnAsset();
        Assert.Equal("PendingInstallation", await StatusOf(As(Manager), assetId));

        var added = await AddEventOk(As(WorkTeam), assetId, NewEvent(notes: "Concrete base poured"));

        Assert.Equal(("Installed new post", "Concrete base poured", EventType.Installed), (added.Title, added.Notes, added.Type));
        Assert.Equal("InService", await StatusOf(As(Manager), assetId));
    }

    [Fact]
    public async Task Events_are_listed_newest_first_by_when_they_happened()
    {
        var assetId = await GivenAnAsset();
        var now = DateTimeOffset.UtcNow;
        await AddEventOk(As(WorkTeam), assetId, NewEvent("Installed", "Installed", at: now.AddDays(-30)));
        await AddEventOk(As(WorkTeam), assetId, NewEvent("Repaired", "Repaired lamp", at: now.AddDays(-2)));
        await AddEventOk(As(WorkTeam), assetId, NewEvent("Checked", "Checked", at: now.AddDays(-10)));

        var events = await As(Manager).GetFromJsonAsync<List<EventView>>($"/api/assets/{assetId}/events", Json);

        Assert.Equal(["Repaired lamp", "Checked", "Installed"], events!.Select(e => e.Title));
    }

    [Fact]
    public async Task An_event_cannot_be_in_the_future_and_needs_a_title()
    {
        var assetId = await GivenAnAsset();

        var future = await AddEvent(As(WorkTeam), assetId, NewEvent(at: DateTimeOffset.UtcNow.AddDays(1)));
        var untitled = await AddEvent(As(WorkTeam), assetId, NewEvent(title: " "));
        var unknown = await AddEvent(As(WorkTeam), assetId, NewEvent(type: "Painted"));

        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, untitled.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task A_removed_asset_takes_no_events_until_it_is_installed_again()
    {
        var assetId = await GivenAnAsset();
        var now = DateTimeOffset.UtcNow;
        await AddEventOk(As(WorkTeam), assetId, NewEvent("Installed", at: now.AddDays(-5)));
        await AddEventOk(As(WorkTeam), assetId, NewEvent("Removed", "Knocked down", at: now.AddDays(-4)));
        Assert.Equal("Removed", await StatusOf(As(Manager), assetId));

        var refused = await AddEvent(As(WorkTeam), assetId, NewEvent("Checked", "Checked", at: now.AddDays(-3)));
        await AddEventOk(As(Manager), assetId, NewEvent("Installed", "Installed again", at: now.AddDays(-2)));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("InService", await StatusOf(As(Manager), assetId));
    }

    [Fact]
    public async Task Events_of_an_asset_in_another_tenant_or_an_unknown_asset_find_nothing()
    {
        var assetId = await GivenAnAsset();
        var stranger = As(Manager, $"tenant-{Guid.NewGuid():N}");

        var add = await AddEvent(stranger, assetId, NewEvent());
        var list = await stranger.GetAsync($"/api/assets/{assetId}/events");
        var unknown = await AddEvent(As(Manager), Guid.NewGuid(), NewEvent());

        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound], [add.StatusCode, list.StatusCode, unknown.StatusCode]);
    }

    [Fact]
    public async Task An_event_can_be_corrected_and_the_correction_is_audited()
    {
        var assetId = await GivenAnAsset();
        var original = await AddEventOk(As(WorkTeam), assetId, NewEvent("Checked", "Chekced pole", at: DateTimeOffset.UtcNow.AddDays(-3)));
        var editor = As(Manager);
        editor.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "user-2");
        editor.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Grace Hopper");
        var correctedAt = DateTimeOffset.UtcNow.AddDays(-4);

        var response = await editor.PutAsJsonAsync($"/api/assets/{assetId}/events/{original.Id}",
            new { type = "Maintained", title = "Checked pole", notes = "Tightened bolts", occurredAt = correctedAt, version = original.Version });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<EventView>(Json))!;
        Assert.Equal((EventType.Maintained, "Checked pole", "Tightened bolts"), (updated.Type, updated.Title, updated.Notes));
        Assert.NotEqual(original.Version, updated.Version);

        var audit = (await As(Manager).GetFromJsonAsync<List<AuditView>>("/probe/audit", Json))!
            .Where(a => a.EntityId == original.Id.ToString()).ToList();
        var title = Assert.Single(audit, a => a.Field == "Title" && a.Operation == "Updated");
        Assert.Equal(("Chekced pole", "Checked pole", "user-2", "Grace Hopper"), (title.OldValue, title.NewValue, title.ChangedBy, title.ChangedByName));
        var movedTo = Assert.Single(audit, a => a.Field == "OccurredAt" && a.Operation == "Updated").NewValue;
        var milliseconds = DateTimeOffset.Parse(movedTo!, CultureInfo.InvariantCulture).ToUnixTimeMilliseconds();
        Assert.InRange(milliseconds - correctedAt.ToUnixTimeMilliseconds(), -1, 1);
        Assert.DoesNotContain(audit, a => a.Field == "Version");
    }

    private sealed record AuditView(string EntityId, string Operation, string Field, string? OldValue, string? NewValue, string ChangedBy, string? ChangedByName);

    [Fact]
    public async Task Saving_an_event_that_someone_else_changed_in_the_meantime_is_refused()
    {
        var assetId = await GivenAnAsset();
        var original = await AddEventOk(As(WorkTeam), assetId, NewEvent("Checked", "Checked"));
        object Edit(string title) => new { type = "Checked", title, notes = (string?)null, occurredAt = original.OccurredAt, version = original.Version };

        var first = await As(WorkTeam).PutAsJsonAsync($"/api/assets/{assetId}/events/{original.Id}", Edit("First edit"));
        var second = await As(Manager).PutAsJsonAsync($"/api/assets/{assetId}/events/{original.Id}", Edit("Second edit"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task An_event_edit_cannot_move_it_to_another_asset_or_find_an_event_of_another_tenant()
    {
        var assetId = await GivenAnAsset();
        var original = await AddEventOk(As(WorkTeam), assetId, NewEvent());
        var body = new { type = "Installed", title = "Mine", notes = (string?)null, occurredAt = original.OccurredAt, version = original.Version };

        var stranger = await As(Manager, $"tenant-{Guid.NewGuid():N}").PutAsJsonAsync($"/api/assets/{assetId}/events/{original.Id}", body);
        var wrongAsset = await As(Manager).PutAsJsonAsync($"/api/assets/{Guid.NewGuid()}/events/{original.Id}", body);

        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrongAsset.StatusCode);
    }

    [Fact]
    public async Task The_map_list_shows_the_status_of_each_asset()
    {
        var assetId = await GivenAnAsset();
        await AddEventOk(As(WorkTeam), assetId, NewEvent());

        var assets = await As(Manager).GetFromJsonAsync<List<AssetView>>("/api/assets?west=174.85&south=-37.05&east=174.86&north=-37.04", Json);

        Assert.Equal("InService", Assert.Single(assets!).Status);
    }
}
