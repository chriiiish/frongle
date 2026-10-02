using NetTopologySuite.Geometries;

namespace Frongle.Domain;

/// <summary>A physical item in the street, such as a light-post, with a type and a location.</summary>
public class Asset : ITenantOwned
{
    /// <summary>The Internal Id. Only Frongle uses it, and people see the <see cref="FriendlyId"/> instead.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public string TenantId { get; set; } = "";

    /// <summary>The kind of Asset. It never changes, because it is part of the <see cref="FriendlyId"/>.</summary>
    public required AssetType Type { get; set; }

    /// <summary>The Area that contained the location when the Asset was created.</summary>
    public required Guid AreaId { get; set; }

    /// <summary>The Area part of the <see cref="FriendlyId"/>, kept with the Asset so that the Asset does not need its Area to be loaded.</summary>
    public required string AreaCode { get; set; }

    /// <summary>The number part of the <see cref="FriendlyId"/>. It is unique within the tenant, Area, and type, and it is never reused.</summary>
    public required int Number { get; set; }

    /// <summary>The tag on the Asset, for example <c>MN-LP-02213</c>.</summary>
    public required string FriendlyId { get; set; }

    /// <summary>Where the Asset stands, in longitude and latitude (SRID 4326).</summary>
    public required Point Location { get; set; }

    /// <summary>Builds the Friendly Id of an Asset.</summary>
    /// <param name="areaCode">The two-letter code of the Area that contains the Asset.</param>
    /// <param name="type">The type of the Asset.</param>
    /// <param name="number">The number that the Asset has within its tenant, Area, and type.</param>
    /// <returns>The Friendly Id, for example <c>MN-LP-02213</c>.</returns>
    public static string BuildFriendlyId(string areaCode, AssetType type, int number) =>
        $"{areaCode}-{type.Code()}-{number:D5}";
}
