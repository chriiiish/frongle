using System.Net.Http.Json;
using Npgsql;

namespace Frongle.Api.Tests;

[Collection(PostgresTests.Name)]
public sealed class TenantIsolationTests(PostgresFixture database) : IDisposable
{
    private readonly PostgresApiFactory _factory = new(database);

    public void Dispose() => _factory.Dispose();

    private async Task<List<string>> NotesSeenBy(string tenant)
    {
        var client = _factory.CreateClientFor(tenant, "work-team");
        return (await client.GetFromJsonAsync<List<string>>("/probe/notes"))!;
    }

    private async Task AddNote(string tenant, string text)
    {
        var response = await _factory.CreateClientFor(tenant, "work-team").PostAsJsonAsync("/probe/notes", new NoteRequest(text));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_tenant_sees_the_rows_it_wrote_and_no_other_tenants_rows()
    {
        var (acme, globex) = (NewTenant(), NewTenant());
        await AddNote(acme, "acme pole");
        await AddNote(globex, "globex pole");

        Assert.Equal(["acme pole"], await NotesSeenBy(acme));
        Assert.Equal(["globex pole"], await NotesSeenBy(globex));
    }

    [Fact]
    public async Task Postgres_hides_other_tenants_rows_even_from_a_query_with_no_filter()
    {
        var (acme, globex) = (NewTenant(), NewTenant());
        await using var connection = await OpenAs(acme);
        await Execute(connection, $"INSERT INTO probe_notes VALUES (gen_random_uuid(), '{acme}', 'acme pole')");

        Assert.Equal(1L, await Count(connection));
        await SetTenant(connection, globex);
        Assert.Equal(0L, await Count(connection));
        await SetTenant(connection, "");
        Assert.Equal(0L, await Count(connection));
    }

    [Fact]
    public async Task Postgres_refuses_to_write_a_row_for_another_tenant()
    {
        var (acme, globex) = (NewTenant(), NewTenant());
        await using var connection = await OpenAs(acme);

        var refused = await Assert.ThrowsAsync<PostgresException>(() =>
            Execute(connection, $"INSERT INTO probe_notes VALUES (gen_random_uuid(), '{globex}', 'globex pole')"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [Fact]
    public async Task Postgres_refuses_to_write_a_row_when_the_caller_has_no_tenant()
    {
        await using var connection = await OpenAs("");

        var refused = await Assert.ThrowsAsync<PostgresException>(() =>
            Execute(connection, "INSERT INTO probe_notes VALUES (gen_random_uuid(), '', 'nobody pole')"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    private static string NewTenant() => $"tenant-{Guid.NewGuid():N}";

    private async Task<NpgsqlConnection> OpenAs(string tenant)
    {
        var connection = new NpgsqlConnection(database.AppConnectionString);
        await connection.OpenAsync();
        await SetTenant(connection, tenant);
        return connection;
    }

    private static Task SetTenant(NpgsqlConnection connection, string tenant) =>
        Execute(connection, $"SELECT set_config('app.tenant_id', '{tenant}', false)");

    private static async Task Execute(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> Count(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT count(*) FROM probe_notes", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
