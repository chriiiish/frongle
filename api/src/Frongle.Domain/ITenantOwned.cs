namespace Frongle.Domain;

/// <summary>
/// Marks a record that belongs to one tenant. The database and the API both hide it from every other tenant.
/// </summary>
public interface ITenantOwned
{
    /// <summary>
    /// The tenant that owns the record. The API sets it from the caller's token when the record is first saved,
    /// so callers never choose it.
    /// </summary>
    string TenantId { get; set; }
}
