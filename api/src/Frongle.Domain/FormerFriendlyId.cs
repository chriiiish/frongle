namespace Frongle.Domain;

/// <summary>A Friendly Id that an Asset carried before a retag. A search by it still finds the Asset.</summary>
public class FormerFriendlyId : ITenantOwned
{
    /// <summary>The identifier of this record.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public string TenantId { get; set; } = "";

    /// <summary>The Asset that carried the Friendly Id.</summary>
    public required Guid AssetId { get; set; }

    /// <summary>The Friendly Id that the Asset no longer has. No other Asset ever gets it, because numbers are never reused.</summary>
    public required string FriendlyId { get; set; }

    /// <summary>When the Asset lost the Friendly Id.</summary>
    public required DateTimeOffset ReplacedAt { get; set; }
}
