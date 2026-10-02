namespace Frongle.Domain;

/// <summary>The kind of Asset that Frongle tracks.</summary>
public enum AssetType
{
    /// <summary>A light-post, which has the code LP.</summary>
    LightPost,

    /// <summary>A street sign, which has the code SS.</summary>
    StreetSign,

    /// <summary>A telephone pole, which has the code TP.</summary>
    TelephonePole,

    /// <summary>A traffic light, which has the code TL.</summary>
    TrafficLight,
}

/// <summary>Reads facts about an <see cref="AssetType"/>.</summary>
public static class AssetTypes
{
    /// <summary>Gives the two-letter code that stands for the type in a Friendly Id.</summary>
    /// <param name="type">The type of the Asset.</param>
    /// <returns>The code, for example <c>LP</c> for a light-post.</returns>
    public static string Code(this AssetType type) => type switch
    {
        AssetType.LightPost => "LP",
        AssetType.StreetSign => "SS",
        AssetType.TelephonePole => "TP",
        AssetType.TrafficLight => "TL",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown Asset type."),
    };
}
