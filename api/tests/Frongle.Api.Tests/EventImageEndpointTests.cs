using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frongle.Api.Tests;

[Collection(PostgresTests.Name)]
public sealed class EventImageEndpointTests(PostgresFixture database) : IDisposable
{
    private const string Manager = "maintenance-manager";
    private const string WorkTeam = "work-team";
    private const int TenMegabytes = 10 * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgresApiFactory _factory = new(database);
    private readonly string _tenant = $"tenant-{Guid.NewGuid():N}";

    public void Dispose() => _factory.Dispose();

    private HttpClient As(string role, string? tenant = null) => _factory.CreateClientFor(tenant ?? _tenant, role);

    private sealed record Ids(Guid AssetId, Guid EventId)
    {
        public string Path => $"/api/assets/{AssetId}/events/{EventId}/images";
    }

    private sealed record UploadView(Guid Id, string UploadUrl, string ContentType);
    private sealed record ImageView(Guid Id, string ContentType, long SizeBytes, string ReadUrl);
    private sealed record EventView(Guid Id, List<ImageView> Images);
    private sealed record AuditView(string EntityType, string EntityId, string Operation, string Field, string? NewValue, string ChangedBy);

    private async Task<Ids> GivenAnEvent()
    {
        await As(Manager).PostAsJsonAsync("/api/areas", new { code = "MN", name = "Manukau", boundary = Shapes.Square(174.85, -37.05) });
        var asset = await (await As(WorkTeam).PostAsJsonAsync("/api/assets", new { type = "LightPost", latitude = -37.045, longitude = 174.855 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var assetId = asset.GetProperty("id").GetGuid();
        var created = await (await As(WorkTeam).PostAsJsonAsync($"/api/assets/{assetId}/events",
            new { type = "Installed", title = "Installed", occurredAt = DateTimeOffset.UtcNow.AddHours(-1) })).Content.ReadFromJsonAsync<JsonElement>();
        return new Ids(assetId, created.GetProperty("id").GetGuid());
    }

    private static Task<HttpResponseMessage> RequestUpload(HttpClient client, Ids ids, string contentType = "image/jpeg", long sizeBytes = 1024) =>
        client.PostAsJsonAsync(ids.Path, new { contentType, sizeBytes });

    private static async Task<UploadView> RequestUploadOk(HttpClient client, Ids ids)
    {
        var response = await RequestUpload(client, ids);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UploadView>(Json))!;
    }

    private static async Task<List<ImageView>> ImagesOf(HttpClient client, Ids ids) =>
        (await client.GetFromJsonAsync<List<ImageView>>(ids.Path, Json))!;

    [Fact]
    public async Task A_user_asks_for_a_place_to_upload_an_image_and_the_image_is_then_listed_with_a_read_link()
    {
        var ids = await GivenAnEvent();

        var upload = await RequestUploadOk(As(WorkTeam), ids);

        var key = $"{_tenant}/{ids.AssetId}/{ids.EventId}/{upload.Id}";
        Assert.Equal($"https://storage.test/upload/{key}?type=image/jpeg", upload.UploadUrl);
        var image = Assert.Single(await ImagesOf(As(Manager), ids));
        Assert.Equal((upload.Id, "image/jpeg", 1024L, $"https://storage.test/read/{key}"), (image.Id, image.ContentType, image.SizeBytes, image.ReadUrl));
    }

    [Fact]
    public async Task The_events_of_an_asset_carry_their_images()
    {
        var ids = await GivenAnEvent();
        var upload = await RequestUploadOk(As(WorkTeam), ids);

        var events = await As(Manager).GetFromJsonAsync<List<EventView>>($"/api/assets/{ids.AssetId}/events", Json);

        Assert.Equal([upload.Id], Assert.Single(events!).Images.Select(i => i.Id));
    }

    [Theory]
    [InlineData("image/jpeg", 1)]
    [InlineData("image/png", 1)]
    [InlineData("image/webp", TenMegabytes)]
    public async Task Jpeg_png_and_webp_images_up_to_ten_megabytes_are_accepted(string contentType, long sizeBytes)
    {
        var response = await RequestUpload(As(WorkTeam), await GivenAnEvent(), contentType, sizeBytes);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("text/html", 1024)]
    [InlineData("image/gif", 1024)]
    [InlineData("image/jpeg", 0)]
    [InlineData("image/jpeg", TenMegabytes + 1)]
    public async Task Other_types_and_sizes_are_refused(string contentType, long sizeBytes)
    {
        var response = await RequestUpload(As(WorkTeam), await GivenAnEvent(), contentType, sizeBytes);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_event_holds_five_images_and_a_removed_image_frees_a_place()
    {
        var ids = await GivenAnEvent();
        var uploads = new List<UploadView>();
        for (var i = 0; i < 5; i++) uploads.Add(await RequestUploadOk(As(WorkTeam), ids));

        var sixth = await RequestUpload(As(WorkTeam), ids);
        await As(WorkTeam).DeleteAsync($"{ids.Path}/{uploads[0].Id}");
        var again = await RequestUpload(As(WorkTeam), ids);

        Assert.Equal(HttpStatusCode.Conflict, sixth.StatusCode);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Removing_an_image_hides_it_and_the_audit_trail_says_who_removed_it()
    {
        var ids = await GivenAnEvent();
        var upload = await RequestUploadOk(As(WorkTeam), ids);
        var remover = As(Manager);
        remover.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "user-2");

        var response = await remover.DeleteAsync($"{ids.Path}/{upload.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await ImagesOf(As(Manager), ids));
        var audit = (await As(Manager).GetFromJsonAsync<List<AuditView>>("/probe/audit", Json))!;
        var removal = Assert.Single(audit, a => a.EntityId == upload.Id.ToString() && a.Field == "RemovedAt");
        Assert.Equal(("EventImage", "Updated", "user-2"), (removal.EntityType, removal.Operation, removal.ChangedBy));
        Assert.NotNull(removal.NewValue);
    }

    [Fact]
    public async Task Images_of_an_event_in_another_tenant_or_an_unknown_event_find_nothing()
    {
        var ids = await GivenAnEvent();
        var upload = await RequestUploadOk(As(WorkTeam), ids);
        var stranger = As(Manager, $"tenant-{Guid.NewGuid():N}");

        var request = await RequestUpload(stranger, ids);
        var list = await stranger.GetAsync(ids.Path);
        var remove = await stranger.DeleteAsync($"{ids.Path}/{upload.Id}");
        var unknown = await As(Manager).DeleteAsync($"{ids.Path}/{Guid.NewGuid()}");

        Assert.All([request, list, remove, unknown], r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }
}
