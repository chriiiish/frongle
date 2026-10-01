using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Frongle.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Frongle.Api.Data;

/// <summary>
/// The Frongle database. It hides other tenants' rows, stamps new rows with the caller's tenant,
/// and writes an <see cref="AuditRecord"/> for every field that a save changes.
/// </summary>
public class FrongleDbContext : DbContext
{
    private readonly ICaller _caller;

    /// <summary>Creates the context for one request.</summary>
    /// <param name="options">The database connection settings.</param>
    /// <param name="caller">The signed-in user, whose tenant filters the rows and whose identity goes in audit records.</param>
    public FrongleDbContext(DbContextOptions<FrongleDbContext> options, ICaller caller)
        : this((DbContextOptions)options, caller)
    {
    }

    /// <summary>Creates a context of a derived type, for the same purpose as the public constructor.</summary>
    /// <param name="options">The database connection settings.</param>
    /// <param name="caller">The signed-in user, whose tenant filters the rows and whose identity goes in audit records.</param>
    protected FrongleDbContext(DbContextOptions options, ICaller caller) : base(options)
    {
        _caller = caller;
    }

    /// <summary>The Areas of the caller's tenant.</summary>
    public DbSet<Area> Areas => Set<Area>();

    /// <summary>The Assets of the caller's tenant.</summary>
    public DbSet<Asset> Assets => Set<Asset>();

    /// <summary>Every recorded change, for the caller's tenant only.</summary>
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    // Read by the query filters of every tenant-owned type. EF Core reads it again on each query.
    private string? CurrentTenantId => _caller.TenantId;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditRecord>(audit =>
        {
            audit.ToTable("audit_records");
            audit.Property(a => a.Id).HasColumnName("id");
            audit.Property(a => a.EntityType).HasColumnName("entity_type");
            audit.Property(a => a.EntityId).HasColumnName("entity_id");
            audit.Property(a => a.Operation).HasColumnName("operation").HasConversion<string>();
            audit.Property(a => a.Field).HasColumnName("field");
            audit.Property(a => a.OldValue).HasColumnName("old_value");
            audit.Property(a => a.NewValue).HasColumnName("new_value");
            audit.Property(a => a.ChangedBy).HasColumnName("changed_by");
            audit.Property(a => a.ChangedByName).HasColumnName("changed_by_name");
            audit.Property(a => a.ChangedAt).HasColumnName("changed_at");
        });

        modelBuilder.Entity<Area>(area =>
        {
            area.ToTable("areas");
            area.Property(a => a.Id).HasColumnName("id");
            area.Property(a => a.Code).HasColumnName("code");
            area.Property(a => a.Name).HasColumnName("name");
            area.Property(a => a.Boundary).HasColumnName("boundary").HasColumnType("geography(Polygon, 4326)");
            area.HasIndex(a => new { a.TenantId, a.Code }).IsUnique();
            area.HasIndex(a => a.Boundary).HasMethod("gist");
        });

        modelBuilder.Entity<Asset>(asset =>
        {
            asset.ToTable("assets");
            asset.Property(a => a.Id).HasColumnName("id");
            asset.Property(a => a.Type).HasColumnName("type").HasConversion<string>();
            asset.Property(a => a.AreaId).HasColumnName("area_id");
            asset.Property(a => a.AreaCode).HasColumnName("area_code");
            asset.Property(a => a.Number).HasColumnName("number");
            asset.Property(a => a.FriendlyId).HasColumnName("friendly_id");
            asset.Property(a => a.Location).HasColumnName("location").HasColumnType("geography(Point, 4326)");
            asset.HasOne<Area>().WithMany().HasForeignKey(a => a.AreaId).OnDelete(DeleteBehavior.Restrict);
            asset.HasIndex(a => new { a.TenantId, a.FriendlyId }).IsUnique();
            asset.HasIndex(a => new { a.TenantId, a.AreaId, a.Type, a.Number }).IsUnique();
            asset.HasIndex(a => a.Location).HasMethod("gist");
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType)))
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
            entity.Property(nameof(ITenantOwned.TenantId)).HasColumnName("tenant_id");
            entity.HasQueryFilter(BelongsToCurrentTenant(entityType.ClrType));
        }
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RecordChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RecordChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private LambdaExpression BelongsToCurrentTenant(Type entityType)
    {
        var entity = Expression.Parameter(entityType, "e");
        var tenantOfEntity = Expression.Property(entity, nameof(ITenantOwned.TenantId));
        var tenantOfCaller = Expression.Property(
            Expression.Constant(this, typeof(FrongleDbContext)),
            typeof(FrongleDbContext).GetProperty(nameof(CurrentTenantId), BindingFlags.Instance | BindingFlags.NonPublic)!);
        return Expression.Lambda(Expression.Equal(tenantOfEntity, tenantOfCaller), entity);
    }

    private void RecordChanges()
    {
        ChangeTracker.DetectChanges();
        var changed = ChangeTracker.Entries<ITenantOwned>()
            .Where(e => e.Entity is not AuditRecord && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (changed.Count == 0) return;

        var tenantId = _caller.TenantId ?? throw new InvalidOperationException("Nobody is signed in with a tenant, so the API cannot save tenant-owned records.");
        var userId = _caller.UserId ?? throw new InvalidOperationException("Nobody is signed in, so the API cannot record who made the change.");
        var changedAt = DateTimeOffset.UtcNow;

        foreach (var entry in changed)
            AuditRecords.AddRange(AuditRecordsFor(entry, userId, changedAt));

        foreach (var added in ChangeTracker.Entries<ITenantOwned>().Where(e => e.State == EntityState.Added))
            added.Entity.TenantId = tenantId;
    }

    private IEnumerable<AuditRecord> AuditRecordsFor(EntityEntry<ITenantOwned> entry, string userId, DateTimeOffset changedAt)
    {
        var operation = entry.State switch
        {
            EntityState.Added => AuditOperation.Created,
            EntityState.Deleted => AuditOperation.Deleted,
            _ => AuditOperation.Updated,
        };
        var entityId = string.Join(',', entry.Metadata.FindPrimaryKey()!.Properties.Select(p => entry.Property(p.Name).CurrentValue));

        return entry.Properties
            .Where(p => !p.Metadata.IsPrimaryKey() && p.Metadata.Name != nameof(ITenantOwned.TenantId))
            .Select(p => (p.Metadata.Name, Old: operation == AuditOperation.Created ? null : p.OriginalValue, New: operation == AuditOperation.Deleted ? null : p.CurrentValue))
            .Where(change => !Equals(change.Old, change.New))
            .Select(change => new AuditRecord
            {
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entityId,
                Operation = operation,
                Field = change.Name,
                OldValue = AsText(change.Old),
                NewValue = AsText(change.New),
                ChangedBy = userId,
                ChangedByName = _caller.Name,
                ChangedAt = changedAt,
            });
    }

    private static string? AsText(object? value) => value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
}
