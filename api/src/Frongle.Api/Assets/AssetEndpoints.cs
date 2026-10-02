using Frongle.Api.Data;
using Frongle.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Frongle.Api.Assets;

/// <summary>The Asset that the caller wants to add to the map.</summary>
/// <param name="Type">The kind of Asset.</param>
/// <param name="Latitude">Where the Asset stands, in degrees from -90 to 90.</param>
/// <param name="Longitude">Where the Asset stands, in degrees from -180 to 180.</param>
public sealed record AssetRequest(AssetType Type, double Latitude, double Longitude);

/// <summary>An Asset as the API describes it.</summary>
/// <param name="Id">The Internal Id, which the caller uses to ask for the Asset again.</param>
/// <param name="FriendlyId">The tag on the Asset, for example <c>MN-LP-02213</c>.</param>
/// <param name="Type">The kind of Asset.</param>
/// <param name="AreaCode">The code of the Area that contains the Asset.</param>
/// <param name="Latitude">Where the Asset stands, in degrees.</param>
/// <param name="Longitude">Where the Asset stands, in degrees.</param>
public sealed record AssetResponse(Guid Id, string FriendlyId, AssetType Type, string AreaCode, double Latitude, double Longitude)
{
    /// <summary>Describes an Asset for the caller.</summary>
    /// <param name="asset">The Asset to describe.</param>
    /// <returns>The Asset as the API sends it.</returns>
    public static AssetResponse From(Asset asset) =>
        new(asset.Id, asset.FriendlyId, asset.Type, asset.AreaCode, asset.Location.Y, asset.Location.X);
}

/// <summary>The endpoints that add Assets to the map and find them again.</summary>
public static class AssetEndpoints
{
    private const int Wgs84 = 4326;
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), Wgs84);

    /// <summary>Adds the Asset endpoints to the API.</summary>
    /// <param name="app">The API to add the endpoints to.</param>
    /// <returns>The same API, so that calls can be chained.</returns>
    public static IEndpointRouteBuilder MapAssetEndpoints(this IEndpointRouteBuilder app)
    {
        var assets = app.MapGroup("/api/assets").WithTags("Assets");

        assets.MapPost("/", CreateAsset)
            .WithSummary("Add an Asset to the map")
            .WithDescription("Maintenance Managers and Work Teams. The API finds the Area that contains the location and gives the Asset the next Friendly Id for that Area and type. It refuses a location that is outside every Area.")
            .Produces<AssetResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status422UnprocessableEntity);

        assets.MapGet("/", ListAssets)
            .WithSummary("List the Assets in a part of the map")
            .WithDescription("Returns the Assets of your tenant that are inside the box from west to east and from south to north, in degrees.")
            .Produces<IEnumerable<AssetResponse>>()
            .ProducesValidationProblem();

        assets.MapGet("/{id:guid}", async (Guid id, FrongleDbContext db) =>
                await db.Assets.SingleOrDefaultAsync(a => a.Id == id) is { } asset
                    ? Results.Ok(AssetResponse.From(asset))
                    : Results.NotFound())
            .WithSummary("Show one Asset")
            .WithDescription("Finds the Asset by its Internal Id.")
            .Produces<AssetResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateAsset(AssetRequest request, FrongleDbContext db, ICaller caller)
    {
        if (!IsOnTheEarth(request.Longitude, request.Latitude))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["location"] = ["The longitude must be from -180 to 180 and the latitude from -90 to 90."],
            });

        var location = Factory.CreatePoint(new Coordinate(request.Longitude, request.Latitude));
        // A point on the edge between two Areas belongs to both. The lower code wins, so the answer is always the same.
        var area = await db.Areas
            .FromSql($"SELECT * FROM areas WHERE ST_Covers(boundary, {location}::geography)")
            .OrderBy(a => a.Code)
            .FirstOrDefaultAsync();
        if (area is null)
            return Results.UnprocessableEntity(new { title = "This spot is outside every area. Ask a Maintenance Manager to draw one." });

        await using var transaction = await db.Database.BeginTransactionAsync();
        var number = await AssetNumbers.TakeNext(db, caller.TenantId!, area.Id, request.Type);
        var asset = new Asset
        {
            Type = request.Type,
            AreaId = area.Id,
            AreaCode = area.Code,
            Number = number,
            FriendlyId = Asset.BuildFriendlyId(area.Code, request.Type, number),
            Location = location,
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Created($"/api/assets/{asset.Id}", AssetResponse.From(asset));
    }

    private static async Task<IResult> ListAssets(double west, double south, double east, double north, FrongleDbContext db)
    {
        if (!IsOnTheEarth(west, south) || !IsOnTheEarth(east, north) || west >= east || south >= north)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["box"] = ["Give a box on the earth with west less than east and south less than north."],
            });

        var assets = await db.Assets
            .FromSql($"SELECT * FROM assets WHERE ST_Intersects(location, ST_MakeEnvelope({west}, {south}, {east}, {north}, 4326)::geography)")
            .OrderBy(a => a.FriendlyId)
            .ToListAsync();
        return Results.Ok(assets.Select(AssetResponse.From));
    }

    private static bool IsOnTheEarth(double longitude, double latitude) =>
        double.IsFinite(longitude) && double.IsFinite(latitude) && Math.Abs(longitude) <= 180 && Math.Abs(latitude) <= 90;
}
