using System.Net;
using System.Net.Http.Json;

namespace Frongle.Api.Tests;

[Collection(PostgresTests.Name)]
public sealed class AreaEndpointTests(PostgresFixture database) : IDisposable
{
    private const string Manager = "maintenance-manager";
    private const string WorkTeam = "work-team";

    private readonly PostgresApiFactory _factory = new(database);
    private readonly string _tenant = $"tenant-{Guid.NewGuid():N}";

    public void Dispose() => _factory.Dispose();

    private HttpClient As(string role, string? tenant = null) => _factory.CreateClientFor(tenant ?? _tenant, role);

    // A square near Auckland, in longitude and latitude, with its first point repeated at the end as GeoJSON requires.
    private static object Square(double west, double south, double size = 0.01) => new
    {
        type = "Polygon",
        coordinates = new[]
        {
            new[]
            {
                new[] { west, south },
                new[] { west + size, south },
                new[] { west + size, south + size },
                new[] { west, south + size },
                new[] { west, south },
            },
        },
    };

    private static Task<HttpResponseMessage> CreateArea(HttpClient client, string code, string name, object boundary) =>
        client.PostAsJsonAsync("/api/areas", new { code, name, boundary });

    private sealed record AreaView(Guid Id, string Code, string Name, BoundaryView Boundary);
    private sealed record BoundaryView(string Type, double[][][] Coordinates);

    private static async Task<List<AreaView>> ListAreas(HttpClient client) =>
        (await client.GetFromJsonAsync<List<AreaView>>("/api/areas"))!;

    [Fact]
    public async Task A_maintenance_manager_creates_an_area_and_everyone_in_the_tenant_can_list_it()
    {
        var created = await CreateArea(As(Manager), "MN", "Manukau", Square(174.85, -37.05));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var area = Assert.Single(await ListAreas(As(WorkTeam)));
        Assert.Equal(("MN", "Manukau", "Polygon"), (area.Code, area.Name, area.Boundary.Type));
        Assert.Equal([174.85, -37.05], area.Boundary.Coordinates[0][0]);
        Assert.Equal(area.Boundary.Coordinates[0][0], area.Boundary.Coordinates[0][^1]);
    }

    [Fact]
    public async Task A_work_team_cannot_create_an_area()
    {
        var response = await CreateArea(As(WorkTeam), "MN", "Manukau", Square(174.85, -37.05));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Another_tenant_does_not_see_the_area_and_can_use_the_same_code_and_place()
    {
        await CreateArea(As(Manager), "MN", "Manukau", Square(174.85, -37.05));
        var other = $"tenant-{Guid.NewGuid():N}";

        Assert.Empty(await ListAreas(As(Manager, other)));
        var response = await CreateArea(As(Manager, other), "MN", "Manukau", Square(174.85, -37.05));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_code_is_unique_within_a_tenant()
    {
        await CreateArea(As(Manager), "MN", "Manukau", Square(174.85, -37.05));

        var response = await CreateArea(As(Manager), "MN", "Another Manukau", Square(175.5, -37.5));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("M")]
    [InlineData("MNK")]
    [InlineData("mn")]
    [InlineData("M1")]
    [InlineData("")]
    public async Task A_code_must_be_two_capital_letters(string code)
    {
        var response = await CreateArea(As(Manager), code, "Manukau", Square(174.85, -37.05));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_area_needs_a_name()
    {
        var response = await CreateArea(As(Manager), "MN", " ", Square(174.85, -37.05));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_boundary_must_be_a_valid_closed_polygon()
    {
        var bowtie = new
        {
            type = "Polygon",
            coordinates = new[] { new[] { new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 }, new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 }, new[] { 0.0, 0.0 } } },
        };
        var notClosed = new
        {
            type = "Polygon",
            coordinates = new[] { new[] { new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 }, new[] { 1.0, 1.0 }, new[] { 0.0, 1.0 } } },
        };
        var offTheEarth = new
        {
            type = "Polygon",
            coordinates = new[] { new[] { new[] { 0.0, 0.0 }, new[] { 200.0, 0.0 }, new[] { 200.0, 1.0 }, new[] { 0.0, 0.0 } } },
        };
        var point = new { type = "Point", coordinates = new[] { 174.85, -37.05 } };

        foreach (var boundary in new object[] { bowtie, notClosed, offTheEarth, point })
        {
            var response = await CreateArea(As(Manager), "MN", "Manukau", boundary);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task An_area_cannot_overlap_another_area_of_the_tenant_but_can_share_an_edge()
    {
        await CreateArea(As(Manager), "MN", "Manukau", Square(174.85, -37.05));

        var overlapping = await CreateArea(As(Manager), "PA", "Papatoetoe", Square(174.855, -37.045));
        var touching = await CreateArea(As(Manager), "OT", "Otara", Square(174.86, -37.05));

        Assert.Equal(HttpStatusCode.Conflict, overlapping.StatusCode);
        Assert.Equal(HttpStatusCode.Created, touching.StatusCode);
    }

    [Fact]
    public async Task A_user_without_a_frongle_role_cannot_list_areas()
    {
        var response = await As("some-other-realm-role").GetAsync("/api/areas");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_manager_whose_token_has_a_blank_tenant_is_refused()
    {
        var response = await CreateArea(As(Manager, " "), "MN", "Manukau", Square(174.85, -37.05));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_missing_code_is_a_validation_problem_not_a_server_error()
    {
        var withNull = await As(Manager).PostAsJsonAsync("/api/areas", new { code = (string?)null, name = "Manukau", boundary = Square(174.85, -37.05) });
        var omitted = await As(Manager).PostAsJsonAsync("/api/areas", new { name = "Manukau", boundary = Square(174.85, -37.05) });

        Assert.Equal(HttpStatusCode.BadRequest, withNull.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, omitted.StatusCode);
    }

    [Fact]
    public async Task A_boundary_with_an_empty_or_missing_ring_is_a_validation_problem()
    {
        var emptyRing = new { type = "Polygon", coordinates = new[] { Array.Empty<double[]>() } };
        var missingRing = new { type = "Polygon", coordinates = new double[]?[][] { null! } };

        foreach (var boundary in new object[] { emptyRing, missingRing })
        {
            var response = await CreateArea(As(Manager), "MN", "Manukau", boundary);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Two_managers_who_draw_overlapping_areas_at_once_save_only_one()
    {
        var attempts = Enumerable.Range(0, 6)
            .Select(i => CreateArea(As(Manager), $"A{(char)('A' + i)}", $"Area {i}", Square(174.85 + i * 0.001, -37.05)))
            .ToArray();

        var responses = await Task.WhenAll(attempts);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Two_managers_who_use_the_same_code_at_once_get_a_conflict_not_a_server_error()
    {
        var responses = await Task.WhenAll(
            CreateArea(As(Manager), "MN", "Manukau", Square(174.85, -37.05)),
            CreateArea(As(Manager), "MN", "Another", Square(175.5, -37.5)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task A_maintenance_manager_changes_the_name_and_boundary_of_an_area_but_not_its_code()
    {
        await CreateArea(As(Manager), "MN", "Manukau", Square(174.85, -37.05));
        var id = (await ListAreas(As(Manager))).Single().Id;

        var response = await As(Manager).PutAsJsonAsync($"/api/areas/{id}", new { name = "Manukau City", boundary = Square(174.85, -37.05, 0.02) });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var area = Assert.Single(await ListAreas(As(Manager)));
        Assert.Equal(("MN", "Manukau City"), (area.Code, area.Name));
        Assert.Equal(174.87, area.Boundary.Coordinates[0][1][0], 5);
    }

    [Fact]
    public async Task A_boundary_change_cannot_make_an_area_overlap_another_area()
    {
        await CreateArea(As(Manager), "MN", "Manukau", Square(174.85, -37.05));
        await CreateArea(As(Manager), "OT", "Otara", Square(174.86, -37.05));
        var manukau = (await ListAreas(As(Manager))).Single(a => a.Code == "MN");

        var response = await As(Manager).PutAsJsonAsync($"/api/areas/{manukau.Id}", new { name = "Manukau", boundary = Square(174.85, -37.05, 0.02) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_work_team_cannot_change_or_delete_an_area()
    {
        await CreateArea(As(Manager), "MN", "Manukau", Square(174.85, -37.05));
        var id = (await ListAreas(As(Manager))).Single().Id;

        var change = await As(WorkTeam).PutAsJsonAsync($"/api/areas/{id}", new { name = "Mine", boundary = Square(174.85, -37.05) });
        var delete = await As(WorkTeam).DeleteAsync($"/api/areas/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, change.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task A_maintenance_manager_deletes_an_area()
    {
        await CreateArea(As(Manager), "MN", "Manukau", Square(174.85, -37.05));
        var id = (await ListAreas(As(Manager))).Single().Id;

        var response = await As(Manager).DeleteAsync($"/api/areas/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await ListAreas(As(Manager)));
    }

    [Fact]
    public async Task Changing_or_deleting_an_area_of_another_tenant_finds_nothing()
    {
        await CreateArea(As(Manager), "MN", "Manukau", Square(174.85, -37.05));
        var id = (await ListAreas(As(Manager))).Single().Id;
        var intruder = As(Manager, $"tenant-{Guid.NewGuid():N}");

        var change = await intruder.PutAsJsonAsync($"/api/areas/{id}", new { name = "Mine", boundary = Square(174.85, -37.05) });
        var delete = await intruder.DeleteAsync($"/api/areas/{id}");

        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }
}
