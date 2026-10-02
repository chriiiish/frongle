namespace Frongle.Domain;

/// <summary>A photo that shows what an <see cref="AssetEvent"/> was about. The file lives in object storage, not in the database.</summary>
public class EventImage : ITenantOwned
{
    /// <summary>The identifier of the image.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public string TenantId { get; set; } = "";

    /// <summary>The Event that the image belongs to. It never changes.</summary>
    public required Guid EventId { get; set; }

    /// <summary>Where the file is in object storage.</summary>
    public required string StorageKey { get; set; }

    /// <summary>The media type of the file, for example <c>image/jpeg</c>.</summary>
    public required string ContentType { get; set; }

    /// <summary>The size of the file in bytes, as the uploader declared it.</summary>
    public required long SizeBytes { get; set; }

    /// <summary>
    /// When someone removed the image, or <see langword="null"/> while it is in use. A removed image stays in storage
    /// and in the database, and the audit trail says who removed it.
    /// </summary>
    public DateTimeOffset? RemovedAt { get; set; }
}
