using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frongle.Domain;
using Npgsql;

namespace Frongle.Api.Tests;

[Collection(PostgresTests.Name)]
public sealed class AuditTests(PostgresFixture database) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgresApiFactory _factory = new(database);
    private readonly string _tenant = $"tenant-{Guid.NewGuid():N}";

    public void Dispose() => _factory.Dispose();

    private HttpClient SignedInAs(string subject, string name, string? tenant = null)
    {
        var client = _factory.CreateClientFor(tenant ?? _tenant, "work-team");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, subject);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, name);
        return client;
    }

    private static async Task<Guid> AddNote(HttpClient client, string text)
    {
        var response = await client.PostAsJsonAsync("/probe/notes", new NoteRequest(text));
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<List<AuditRecord>> AuditSeenBy(HttpClient client) =>
        (await client.GetFromJsonAsync<List<AuditRecord>>("/probe/audit", Json))!;

    [Fact]
    public async Task Creating_a_record_audits_each_field_with_who_and_when()
    {
        var before = DateTimeOffset.UtcNow;
        var client = SignedInAs("user-1", "Ada Lovelace");
        var id = await AddNote(client, "leaning pole");

        var audit = Assert.Single(await AuditSeenBy(client));
        Assert.Equal(("ProbeNote", id.ToString(), AuditOperation.Created), (audit.EntityType, audit.EntityId, audit.Operation));
        Assert.Equal(("Text", null, "leaning pole"), (audit.Field, audit.OldValue, audit.NewValue));
        Assert.Equal(("user-1", "Ada Lovelace", _tenant), (audit.ChangedBy, audit.ChangedByName, audit.TenantId));
        Assert.InRange(audit.ChangedAt, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Updating_a_record_audits_the_old_and_the_new_value_of_the_field_that_changed()
    {
        var author = SignedInAs("user-1", "Ada Lovelace");
        var id = await AddNote(author, "leaning pole");

        var editor = SignedInAs("user-2", "Grace Hopper");
        await editor.PatchAsJsonAsync($"/probe/notes/{id}", new NoteRequest("straight pole"));

        var update = Assert.Single(await AuditSeenBy(author), a => a.Operation == AuditOperation.Updated);
        Assert.Equal(("Text", "leaning pole", "straight pole"), (update.Field, update.OldValue, update.NewValue));
        Assert.Equal(("user-2", "Grace Hopper"), (update.ChangedBy, update.ChangedByName));
    }

    [Fact]
    public async Task Saving_a_record_without_a_change_writes_no_audit_record()
    {
        var client = SignedInAs("user-1", "Ada Lovelace");
        var id = await AddNote(client, "leaning pole");

        await client.PatchAsJsonAsync($"/probe/notes/{id}", new NoteRequest("leaning pole"));

        Assert.DoesNotContain(await AuditSeenBy(client), a => a.Operation == AuditOperation.Updated);
    }

    [Fact]
    public async Task A_tenant_cannot_see_the_audit_records_of_another_tenant()
    {
        await AddNote(SignedInAs("user-1", "Ada Lovelace"), "leaning pole");

        var other = SignedInAs("user-9", "Alan Turing", $"tenant-{Guid.NewGuid():N}");

        Assert.Empty(await AuditSeenBy(other));
    }

    [Theory]
    [InlineData("UPDATE audit_records SET new_value = 'forged'")]
    [InlineData("DELETE FROM audit_records")]
    public async Task Postgres_refuses_to_change_or_delete_an_audit_record(string sql)
    {
        await AddNote(SignedInAs("user-1", "Ada Lovelace"), "leaning pole");
        await using var connection = new NpgsqlConnection(database.AppConnectionString);
        await connection.OpenAsync();
        await using (var tenant = new NpgsqlCommand($"SELECT set_config('app.tenant_id', '{_tenant}', false)", connection))
            await tenant.ExecuteNonQueryAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        var refused = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal("audit_records is append-only", refused.MessageText);
    }
}
