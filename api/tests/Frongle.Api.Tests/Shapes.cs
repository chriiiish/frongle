namespace Frongle.Api.Tests;

/// <summary>GeoJSON shapes for tests, placed near Auckland.</summary>
public static class Shapes
{
    /// <summary>A square in longitude and latitude, with its first position repeated at the end as GeoJSON requires.</summary>
    public static object Square(double west, double south, double size = 0.01) => new
    {
        type = "Polygon",
        coordinates = new[]
        {
            new[]
            {
                new[] { west, south },
                new[] { west + size, south },
                new[] { west + size, south + size },
                new[] { west, south + size },
                new[] { west, south },
            },
        },
    };
}
