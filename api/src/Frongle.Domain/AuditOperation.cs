namespace Frongle.Domain;

/// <summary>What happened to a record when an <see cref="AuditRecord"/> was written.</summary>
public enum AuditOperation
{
    /// <summary>The record was added. The audit row holds a field's first value.</summary>
    Created,

    /// <summary>A field of the record changed. The audit row holds the old and the new value.</summary>
    Updated,

    /// <summary>The record was deleted. The audit row holds a field's last value.</summary>
    Deleted,
}
