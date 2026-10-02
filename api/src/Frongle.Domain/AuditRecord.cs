namespace Frongle.Domain;

/// <summary>
/// A permanent entry that says who changed one field of one record, from what value to what value, and when.
/// The database refuses to update or delete it.
/// </summary>
public class AuditRecord : ITenantOwned
{
    /// <summary>The identifier of the audit entry itself.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public string TenantId { get; set; } = "";

    /// <summary>The type of the record that changed, for example <c>Asset</c>.</summary>
    public string EntityType { get; set; } = "";

    /// <summary>The key of the record that changed, so the entry can be matched to that record.</summary>
    public string EntityId { get; set; } = "";

    /// <summary>Whether the record was created, updated, or deleted.</summary>
    public AuditOperation Operation { get; set; }

    /// <summary>The name of the field that changed.</summary>
    public string Field { get; set; } = "";

    /// <summary>The value before the change, or <see langword="null"/> when the field had no value.</summary>
    public string? OldValue { get; set; }

    /// <summary>The value after the change, or <see langword="null"/> when the field now has no value.</summary>
    public string? NewValue { get; set; }

    /// <summary>The identifier of the user who made the change, taken from the <c>sub</c> claim of the user's token.</summary>
    public string ChangedBy { get; set; } = "";

    /// <summary>The name of the user at the time of the change, kept because the user account can change later.</summary>
    public string? ChangedByName { get; set; }

    /// <summary>When the change was saved.</summary>
    public DateTimeOffset ChangedAt { get; set; }
}
