using System.Net;
using System.Net.Http.Json;
using Npgsql;

namespace Frongle.Api.Tests;

[Collection(PostgresTests.Name)]
public sealed class StartupMigrationTests(PostgresFixture database)
{
    private static readonly object Square = new
    {
        type = "Polygon",
        coordinates = new[] { new[] { new[] { 174.85, -37.05 }, new[] { 174.86, -37.05 }, new[] { 174.86, -37.04 }, new[] { 174.85, -37.05 } } },
    };

    [Fact]
    public async Task The_api_can_save_an_area_right_after_it_created_the_postgis_extension_on_startup()
    {
        var name = $"fresh_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(database.OwnerConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
            await create.ExecuteNonQueryAsync();
        }
        var fresh = new NpgsqlConnectionStringBuilder(database.OwnerConnectionString) { Database = name }.ConnectionString;
        NpgsqlConnection.ClearAllPools();

        // The first connection of this API runs the migrations, which create PostGIS. It has no geometry type until it reloads its types.
        using var factory = new PostgresApiFactory(database, fresh, migrateOnStartup: true);
        var response = await factory.CreateClientFor("acme", "maintenance-manager")
            .PostAsJsonAsync("/api/areas", new { code = "MN", name = "Manukau", boundary = Square });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
