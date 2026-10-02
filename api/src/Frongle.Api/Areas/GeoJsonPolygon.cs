using NetTopologySuite.Geometries;

namespace Frongle.Api.Areas;

/// <summary>A polygon as GeoJSON, which the map draws and sends. Positions are longitude first, then latitude.</summary>
/// <param name="Type">Must be <c>Polygon</c>.</param>
/// <param name="Coordinates">The rings of the polygon. The first ring is the outline. Each ring starts and ends at the same position.</param>
public sealed record GeoJsonPolygon(string Type, double[][][] Coordinates)
{
    private const int Wgs84 = 4326;
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), Wgs84);

    /// <summary>Describes a polygon in the GeoJSON form.</summary>
    /// <param name="polygon">The polygon to describe.</param>
    /// <returns>The same polygon as GeoJSON, with its rings in the order that they were saved.</returns>
    public static GeoJsonPolygon From(Polygon polygon) => new("Polygon",
        [.. new[] { polygon.ExteriorRing }.Concat(polygon.InteriorRings)
            .Select(ring => ring.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray())]);

    /// <summary>Builds a polygon that can be saved, and says why it cannot when the GeoJSON is not a usable boundary.</summary>
    /// <param name="polygon">The polygon that was built, when the GeoJSON is a valid boundary.</param>
    /// <param name="problem">What is wrong with the GeoJSON, when it is not a valid boundary.</param>
    /// <returns><see langword="true"/> when <paramref name="polygon"/> is set.</returns>
    public bool TryToPolygon(out Polygon polygon, out string problem)
    {
        polygon = null!;
        problem = "";

        if (Type != "Polygon") return Fail("The boundary must be a GeoJSON Polygon.", out problem);
        if (Coordinates is not { Length: > 0 } || Coordinates.Any(ring => ring is not { Length: > 0 }))
            return Fail("The boundary needs at least one ring, and no ring can be empty.", out problem);
        if (Coordinates.SelectMany(ring => ring).Any(IsOffTheEarth))
            return Fail("Each position must be longitude (-180 to 180) then latitude (-90 to 90).", out problem);

        try
        {
            var rings = Coordinates.Select(ring => Factory.CreateLinearRing([.. ring.Select(p => new Coordinate(p[0], p[1]))])).ToArray();
            polygon = Factory.CreatePolygon(rings[0], rings[1..]);
        }
        catch (ArgumentException)
        {
            return Fail("Each ring must start and end at the same position and have at least four positions.", out problem);
        }

        return polygon.IsValid ? true : Fail("The boundary must not cross itself.", out problem);
    }

    /// <summary>Finds out whether a position is not a usable longitude and latitude.</summary>
    /// <param name="position">The position to test, as longitude then latitude.</param>
    /// <returns><see langword="true"/> when the position is missing, not a number, or outside the range of the Earth.</returns>
    private static bool IsOffTheEarth(double[] position) =>
        position is not { Length: >= 2 } || !double.IsFinite(position[0]) || !double.IsFinite(position[1])
        || Math.Abs(position[0]) > 180 || Math.Abs(position[1]) > 90;

    /// <summary>Reports a problem and a failed result in one step, so that a check reads as one line.</summary>
    /// <param name="message">What is wrong with the GeoJSON.</param>
    /// <param name="problem">Set to <paramref name="message"/>.</param>
    /// <returns>Always <see langword="false"/>.</returns>
    private static bool Fail(string message, out string problem)
    {
        problem = message;
        return false;
    }
}
