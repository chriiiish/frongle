namespace Frongle.Domain;

/// <summary>The kind of thing that happened to an Asset.</summary>
public enum EventType
{
    /// <summary>The Asset was put in place, or put in place again after a removal.</summary>
    Installed,

    /// <summary>Someone looked at the Asset and found out its condition.</summary>
    Checked,

    /// <summary>Someone fixed a fault in the Asset.</summary>
    Repaired,

    /// <summary>Someone did planned upkeep on the Asset.</summary>
    Maintained,

    /// <summary>The Asset was taken away from the street.</summary>
    Removed,
}
