namespace Frongle.Domain;

/// <summary>A dated record in the history of an Asset of one thing that happened to it.</summary>
public class AssetEvent : ITenantOwned
{
    /// <summary>The identifier of the Event.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public string TenantId { get; set; } = "";

    /// <summary>The Asset that the Event happened to. It never changes.</summary>
    public required Guid AssetId { get; set; }

    /// <summary>The kind of Event.</summary>
    public required EventType Type { get; set; }

    /// <summary>A short line that says what happened.</summary>
    public required string Title { get; set; }

    /// <summary>More detail about what happened, if the person who wrote the Event had more to say.</summary>
    public string? Notes { get; set; }

    /// <summary>When the Event happened, which can be earlier than when someone recorded it.</summary>
    public required DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// Changes every time the database saves the Event. A caller that edits the Event sends back the version that it read,
    /// and the save fails when someone else saved the Event in the meantime.
    /// </summary>
    public uint Version { get; set; }
}
