namespace Frongle.Domain;

/// <summary>Where an Asset is in its life. It is worked out from the Asset's Events and never stored.</summary>
public enum AssetStatus
{
    /// <summary>The Asset is on the map but has no Events yet, so nobody has installed it.</summary>
    PendingInstallation,

    /// <summary>The Asset is in the street. Its latest Event is not a removal.</summary>
    InService,

    /// <summary>The Asset was taken away. Its latest Event is a removal.</summary>
    Removed,
}

/// <summary>Works out the <see cref="AssetStatus"/> of an Asset.</summary>
public static class AssetStatuses
{
    /// <summary>Gives the status that follows from the latest Event of an Asset.</summary>
    /// <param name="latestEvent">The type of the Asset's latest Event, or <see langword="null"/> when the Asset has no Events.</param>
    /// <returns>The status of the Asset.</returns>
    public static AssetStatus From(EventType? latestEvent) => latestEvent switch
    {
        null => AssetStatus.PendingInstallation,
        EventType.Removed => AssetStatus.Removed,
        _ => AssetStatus.InService,
    };
}
