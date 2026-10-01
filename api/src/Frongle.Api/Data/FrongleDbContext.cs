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
