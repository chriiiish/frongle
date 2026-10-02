using NetTopologySuite.Geometries;

namespace Frongle.Domain;

/// <summary>
/// A named region of the map, drawn by a Maintenance Manager, with a two-letter code.
/// An Asset belongs to the Area that contains its location.
/// </summary>
public class Area : ITenantOwned
{
    /// <summary>The identifier of the Area. People see the <see cref="Code"/> instead.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public string TenantId { get; set; } = "";

    /// <summary>The two capital letters that start the Friendly Id of each Asset in the Area, for example <c>MN</c>. Unique within the tenant.</summary>
    public required string Code { get; set; }

    /// <summary>The name that people use for the Area, for example <c>Manukau</c>.</summary>
    public required string Name { get; set; }

    /// <summary>The outline of the Area in longitude and latitude (SRID 4326). It never overlaps another Area of the tenant.</summary>
    public required Polygon Boundary { get; set; }
}
