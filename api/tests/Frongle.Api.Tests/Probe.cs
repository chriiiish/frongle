using Frongle.Api.Data;
using Frongle.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frongle.Api.Tests;

/// <summary>A tenant-owned row that only the tests use, until Assets give the API a real one.</summary>
public class ProbeNote : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "";
    public string Text { get; set; } = "";
}

public class ProbeDbContext(DbContextOptions<ProbeDbContext> options, ICaller caller)
    : FrongleDbContext(options, caller)
{
    public DbSet<ProbeNote> Notes => Set<ProbeNote>();

    public const string CreateTable = """
        CREATE TABLE probe_notes (
            id uuid PRIMARY KEY,
            tenant_id text NOT NULL,
            text text NOT NULL
        );
        """;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProbeNote>(note =>
        {
            note.ToTable("probe_notes");
            note.Property(n => n.Id).HasColumnName("id");
            note.Property(n => n.Text).HasColumnName("text");
        });
        base.OnModelCreating(modelBuilder);
    }
}

public sealed record NoteRequest(string Text);

/// <summary>Adds the probe endpoints to the API under test. They behave as real tenant-owned endpoints would.</summary>
public class ProbeEndpointsStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/probe/notes", async (NoteRequest request, ProbeDbContext db) =>
            {
                var note = new ProbeNote { Text = request.Text };
                db.Notes.Add(note);
                await db.SaveChangesAsync();
                return Results.Ok(note.Id);
            });
            endpoints.MapGet("/probe/notes", async (ProbeDbContext db) =>
                await db.Notes.OrderBy(n => n.Text).Select(n => n.Text).ToListAsync());
            endpoints.MapGet("/probe/audit", async (ProbeDbContext db) =>
                await db.AuditRecords.OrderBy(a => a.ChangedAt).ThenBy(a => a.Field).ToListAsync());
            endpoints.MapDelete("/probe/notes/{id:guid}", async (Guid id, ProbeDbContext db) =>
            {
                db.Notes.Remove(await db.Notes.SingleAsync(n => n.Id == id));
                await db.SaveChangesAsync();
                return Results.NoContent();
            });
            endpoints.MapPatch("/probe/notes/{id:guid}", async (Guid id, NoteRequest request, ProbeDbContext db) =>
            {
                var note = await db.Notes.SingleAsync(n => n.Id == id);
                note.Text = request.Text;
                await db.SaveChangesAsync();
                return Results.NoContent();
            });
        });
    };
}
