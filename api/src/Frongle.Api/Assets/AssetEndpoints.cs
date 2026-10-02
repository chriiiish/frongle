using System.Text.Json.Serialization;
using Frongle.Api.Auth;
using Frongle.Api.Data;
using Frongle.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Frongle.Api.Assets;

/// <summary>The Asset that the caller wants to add to the map.</summary>
/// <param name="Type">The kind of Asset.</param>
/// <param name="Latitude">Where the Asset stands, in degrees from -90 to 90.</param>
/// <param name="Longitude">Where the Asset stands, in degrees from -180 to 180.</param>
public sealed record AssetRequest(
    [property: JsonRequired] AssetType Type,
    [property: JsonRequired] double Latitude,
    [property: JsonRequired] double Longitude);

/// <summary>The new place for an Asset.</summary>
/// <param name="Latitude">Where the Asset stands now, in degrees from -90 to 90.</param>
/// <param name="Longitude">Where the Asset stands now, in degrees from -180 to 180.</param>
/// <param name="Version">The version of the Asset that the caller read. The API refuses the move when someone else changed the Asset since then.</param>
public sealed record MoveRequest(
    [property: JsonRequired] double Latitude,
    [property: JsonRequired] double Longitude,
    [property: JsonRequired] uint Version);

/// <summary>An Asset as the API describes it.</summary>
/// <param name="Id">The Internal Id, which the caller uses to ask for the Asset again.</param>
/// <param name="FriendlyId">The tag on the Asset, for example <c>MN-LP-02213</c>.</param>
/// <param name="Type">The kind of Asset.</param>
/// <param name="AreaCode">The code of the Area that contains the Asset.</param>
/// <param name="Latitude">Where the Asset stands, in degrees.</param>
/// <param name="Longitude">Where the Asset stands, in degrees.</param>
/// <param name="Status">Where the Asset is in its life, worked out from its latest Event.</param>
/// <param name="NeedsRetag"><see langword="true"/> when the Asset has a new Friendly Id and nobody has recorded that the new tag is fitted.</param>
/// <param name="FormerFriendlyIds">The Friendly Ids that the Asset carried before a retag.</param>
/// <param name="Version">The version to send back when moving the Asset.</param>
public sealed record AssetResponse(
    Guid Id,
    string FriendlyId,
    AssetType Type,
    string AreaCode,
    double Latitude,
    double Longitude,
    AssetStatus Status,
    bool NeedsRetag,
    IReadOnlyList<string> FormerFriendlyIds,
    uint Version)
{
    /// <summary>Describes an Asset for the caller.</summary>
    /// <param name="asset">The Asset to describe.</param>
    /// <param name="status">The status of the Asset, which the Events of the Asset decide.</param>
    /// <param name="formerFriendlyIds">The Friendly Ids that the Asset carried before a retag.</param>
    /// <returns>The Asset as the API sends it.</returns>
    public static AssetResponse From(Asset asset, AssetStatus status, IReadOnlyList<string> formerFriendlyIds) => new(
        asset.Id, asset.FriendlyId, asset.Type, asset.AreaCode, asset.Location.Y, asset.Location.X,
        status, asset.NeedsRetag, formerFriendlyIds, asset.Version);
}

/// <summary>One change in the history of an Asset, its Events, or the images of its Events.</summary>
/// <param name="EntityType">What changed: <c>Asset</c>, <c>AssetEvent</c>, or <c>EventImage</c>.</param>
/// <param name="EntityId">The identifier of the record that changed.</param>
/// <param name="Operation">Whether the record was created, updated, or deleted.</param>
/// <param name="Field">The field that changed.</param>
/// <param name="OldValue">The value before the change, or <see langword="null"/> when there was none.</param>
/// <param name="NewValue">The value after the change, or <see langword="null"/> when there is none.</param>
/// <param name="ChangedBy">The identifier of the user who made the change.</param>
/// <param name="ChangedByName">The name of the user at the time of the change.</param>
/// <param name="ChangedAt">When the change was saved.</param>
public sealed record HistoryResponse(
    string EntityType, string EntityId, AuditOperation Operation, string Field,
    string? OldValue, string? NewValue, string ChangedBy, string? ChangedByName, DateTimeOffset ChangedAt)
{
    /// <summary>Describes an audit record for the caller.</summary>
    /// <param name="audit">The audit record to describe.</param>
    /// <returns>The change as the API sends it.</returns>
    public static HistoryResponse From(AuditRecord audit) => new(
        audit.EntityType, audit.EntityId, audit.Operation, audit.Field,
        audit.OldValue, audit.NewValue, audit.ChangedBy, audit.ChangedByName, audit.ChangedAt);
}

/// <summary>The endpoints that add Assets to the map, move them, and find them again.</summary>
public static class AssetEndpoints
{
    private const int Wgs84 = 4326;
    private const int MaxSearchResults = 20;
    private const int MaxHistoryRows = 500;
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), Wgs84);

    /// <summary>Adds the Asset endpoints to the API.</summary>
    /// <param name="app">The API to add the endpoints to.</param>
    /// <returns>The same API, so that calls can be chained.</returns>
    public static IEndpointRouteBuilder MapAssetEndpoints(this IEndpointRouteBuilder app)
    {
        var assets = app.MapGroup("/api/assets").WithTags("Assets").RequireAuthorization(Policies.Reader);

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

        assets.MapGet("/search", SearchAssets)
            .WithSummary("Find Assets by Friendly Id")
            .WithDescription("Matches part of a Friendly Id, in any letter case. It also matches Friendly Ids that an Asset carried before a retag, because a crew can still read the old tag off the pole. Returns 20 Assets at most.")
            .Produces<IEnumerable<AssetResponse>>()
            .ProducesValidationProblem();

        assets.MapGet("/{id:guid}", GetAsset)
            .WithSummary("Show one Asset")
            .WithDescription("Finds the Asset by its Internal Id.")
            .Produces<AssetResponse>()
            .Produces(StatusCodes.Status404NotFound);

        assets.MapPut("/{id:guid}/location", MoveAsset)
            .WithSummary("Move an Asset")
            .WithDescription("Maintenance Managers and Work Teams. When the new location is in another Area, the Asset gets a new Friendly Id, keeps the old one as a former Friendly Id, and is marked as needing a retag. The caller sends the version that it read.")
            .Produces<AssetResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        assets.MapPost("/{id:guid}/retagged", MarkRetagged)
            .WithSummary("Record that the new tag is fitted")
            .WithDescription("Clears the retag mark after someone fits the tag with the new Friendly Id to the Asset.")
            .Produces<AssetResponse>()
            .Produces(StatusCodes.Status404NotFound);

        assets.MapGet("/{id:guid}/history", ShowHistory)
            .WithSummary("Show who changed what, and when")
            .WithDescription("Returns the changes to the Asset, to its Events, and to the images of its Events, newest first, with the user who made each change. It returns 500 changes at most.")
            .Produces<IEnumerable<HistoryResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>Adds an Asset at a place on the map, in the Area that contains the place.</summary>
    /// <param name="request">The type and place of the Asset.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <param name="caller">The signed-in user, whose tenant the write is serialized for.</param>
    /// <returns>201 with the new Asset, 400 when the place is not on the Earth, or 422 when no Area contains the place.</returns>
    private static Task<IResult> CreateAsset(AssetRequest request, FrongleDbContext db, ICaller caller)
    {
        if (!IsOnTheEarth(request.Longitude, request.Latitude)) return Task.FromResult(LocationOffTheEarth());

        var location = Factory.CreatePoint(new Coordinate(request.Longitude, request.Latitude));
        // A boundary change or an Area delete takes the same lock, so the Area cannot shrink or vanish between the lookup and the insert.
        return AdvisoryLock.Run<IResult>(db, caller.TenantId!, async () =>
        {
            var area = await AreaAt(db, location);
            if (area is null) return OutsideEveryArea();

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
            return Results.Created($"/api/assets/{asset.Id}", AssetResponse.From(asset, AssetStatus.PendingInstallation, []));
        });
    }

    /// <summary>Shows one Asset.</summary>
    /// <param name="id">The Internal Id of the Asset.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <returns>200 with the Asset, or 404 when the tenant has no such Asset.</returns>
    private static async Task<IResult> GetAsset(Guid id, FrongleDbContext db)
    {
        var asset = await db.Assets.SingleOrDefaultAsync(a => a.Id == id);
        return asset is null ? Results.NotFound() : Results.Ok((await Describe(db, [asset]))[0]);
    }

    /// <summary>Lists the Assets in a box on the map.</summary>
    /// <param name="west">The western edge of the box, in degrees of longitude.</param>
    /// <param name="south">The southern edge of the box, in degrees of latitude.</param>
    /// <param name="east">The eastern edge of the box, in degrees of longitude.</param>
    /// <param name="north">The northern edge of the box, in degrees of latitude.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <returns>200 with the Assets in the box, or 400 when the box is not on the Earth or is upside down.</returns>
    private static async Task<IResult> ListAssets(double west, double south, double east, double north, FrongleDbContext db)
    {
        if (!IsOnTheEarth(west, south) || !IsOnTheEarth(east, north) || west >= east || south >= north)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["box"] = ["Give a box on the earth with west less than east and south less than north."],
            });

        var assets = await db.Assets
            .FromSql($"SELECT *, xmin FROM assets WHERE ST_Intersects(location, ST_MakeEnvelope({west}, {south}, {east}, {north}, 4326)::geography)")
            .OrderBy(a => a.FriendlyId)
            .ToListAsync();
        return Results.Ok(await Describe(db, assets));
    }

    /// <summary>Finds Assets by part of a current or former Friendly Id.</summary>
    /// <param name="q">The text to look for, in any letter case.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <returns>200 with up to 20 matching Assets, or 400 when there is nothing to look for.</returns>
    private static async Task<IResult> SearchAssets(string? q, FrongleDbContext db)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["q"] = ["Give some of a Friendly Id to look for."] });

        // The % and _ characters mean "anything" in a LIKE pattern, so they must lose that meaning.
        var pattern = "%" + q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        var assets = await db.Assets
            .Where(a => EF.Functions.ILike(a.FriendlyId, pattern)
                || db.FormerFriendlyIds.Any(f => f.AssetId == a.Id && EF.Functions.ILike(f.FriendlyId, pattern)))
            .OrderBy(a => a.FriendlyId)
            .Take(MaxSearchResults)
            .ToListAsync();
        return Results.Ok(await Describe(db, assets));
    }

    /// <summary>Moves an Asset, and gives it a new Friendly Id when the new place is in another Area.</summary>
    /// <param name="id">The Asset to move.</param>
    /// <param name="request">The new place and the version of the Asset that the caller read.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <param name="caller">The signed-in user, whose tenant the write is serialized for.</param>
    /// <returns>200 with the moved Asset, 400 when the place is not on the Earth, 404 when the tenant has no such Asset, 409 when someone else changed it first, or 422 when no Area contains the place.</returns>
    private static async Task<IResult> MoveAsset(Guid id, MoveRequest request, FrongleDbContext db, ICaller caller)
    {
        if (!IsOnTheEarth(request.Longitude, request.Latitude)) return LocationOffTheEarth();

        var location = Factory.CreatePoint(new Coordinate(request.Longitude, request.Latitude));
        try
        {
            // The same lock as in CreateAsset and in the Area endpoints. A stale move throws, which rolls the transaction back, so it burns no number.
            return await AdvisoryLock.Run(db, caller.TenantId!, async () =>
            {
                var asset = await db.Assets.SingleOrDefaultAsync(a => a.Id == id);
                if (asset is null) return Results.NotFound();
                if (request.Version != asset.Version) throw new DbUpdateConcurrencyException();

                var area = await AreaAt(db, location);
                if (area is null) return OutsideEveryArea();

                db.Entry(asset).Property(a => a.Version).OriginalValue = request.Version;
                asset.Location = location;
                if (area.Id != asset.AreaId) await Retag(db, asset, area, caller.TenantId!);
                await db.SaveChangesAsync();
                return Results.Ok((await Describe(db, [asset]))[0]);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return StaleAsset();
        }
    }

    /// <summary>Records that someone fitted the tag with the new Friendly Id to an Asset.</summary>
    /// <param name="id">The Asset that has its new tag.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <returns>200 with the Asset, 404 when the tenant has no such Asset, or 409 when someone else changed the Asset at the same moment.</returns>
    private static async Task<IResult> MarkRetagged(Guid id, FrongleDbContext db)
    {
        var asset = await db.Assets.SingleOrDefaultAsync(a => a.Id == id);
        if (asset is null) return Results.NotFound();

        asset.NeedsRetag = false;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return StaleAsset();
        }
        return Results.Ok((await Describe(db, [asset]))[0]);
    }

    /// <summary>Shows the changes to an Asset, its Events, and the images of its Events.</summary>
    /// <param name="id">The Internal Id of the Asset.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <returns>200 with up to 500 changes, newest first, or 404 when the tenant has no such Asset.</returns>
    private static async Task<IResult> ShowHistory(Guid id, FrongleDbContext db)
    {
        if (!await db.Assets.AnyAsync(a => a.Id == id)) return Results.NotFound();

        var eventIds = await db.Events.Where(e => e.AssetId == id).Select(e => e.Id).ToListAsync();
        var imageIds = await db.EventImages.Where(i => eventIds.Contains(i.EventId)).Select(i => i.Id).ToListAsync();
        var assetKey = new[] { id.ToString() };
        var eventKeys = eventIds.Select(e => e.ToString()).ToList();
        var imageKeys = imageIds.Select(i => i.ToString()).ToList();

        var changes = await db.AuditRecords
            .Where(a => (a.EntityType == nameof(Asset) && assetKey.Contains(a.EntityId))
                || (a.EntityType == nameof(AssetEvent) && eventKeys.Contains(a.EntityId))
                || (a.EntityType == nameof(EventImage) && imageKeys.Contains(a.EntityId)))
            .OrderByDescending(a => a.ChangedAt).ThenBy(a => a.Field)
            .Take(MaxHistoryRows)
            .ToListAsync();
        return Results.Ok(changes.Select(HistoryResponse.From));
    }

    /// <summary>Gives an Asset the Friendly Id of its new Area. The Asset keeps the old Friendly Id on record, so a crew that reads the old tag off the pole can still find it.</summary>
    /// <param name="db">The database, which must have a transaction open.</param>
    /// <param name="asset">The Asset that moved into another Area.</param>
    /// <param name="area">The Area that now contains the Asset.</param>
    /// <param name="tenantId">The tenant that owns the Asset.</param>
    private static async Task Retag(FrongleDbContext db, Asset asset, Area area, string tenantId)
    {
        db.FormerFriendlyIds.Add(new FormerFriendlyId { AssetId = asset.Id, FriendlyId = asset.FriendlyId, ReplacedAt = DateTimeOffset.UtcNow });
        asset.AreaId = area.Id;
        asset.AreaCode = area.Code;
        asset.Number = await AssetNumbers.TakeNext(db, tenantId, area.Id, asset.Type);
        asset.FriendlyId = Asset.BuildFriendlyId(area.Code, asset.Type, asset.Number);
        asset.NeedsRetag = true;
    }

    /// <summary>Finds the Area that contains a place. A place on the edge between two Areas belongs to both, and the lower code wins, so the answer is always the same.</summary>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <param name="location">The place to look up.</param>
    /// <returns>The Area, or <see langword="null"/> when no Area contains the place.</returns>
    private static Task<Area?> AreaAt(FrongleDbContext db, Point location) =>
        db.Areas
            .FromSql($"SELECT * FROM areas WHERE ST_Covers(boundary, {location}::geography)")
            .OrderBy(a => a.Code)
            .FirstOrDefaultAsync();

    /// <summary>Describes Assets for the caller, with their statuses and former Friendly Ids.</summary>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <param name="assets">The Assets to describe.</param>
    /// <returns>One description for each Asset, in the same order.</returns>
    private static async Task<List<AssetResponse>> Describe(FrongleDbContext db, IReadOnlyCollection<Asset> assets)
    {
        var ids = assets.Select(a => a.Id).ToList();
        var statuses = await AssetStatusLookup.For(db, ids);
        var formerIds = (await db.FormerFriendlyIds
                .Where(f => ids.Contains(f.AssetId))
                .OrderBy(f => f.ReplacedAt)
                .Select(f => new { f.AssetId, f.FriendlyId })
                .ToListAsync())
            .ToLookup(f => f.AssetId, f => f.FriendlyId);
        return [.. assets.Select(a => AssetResponse.From(a, statuses[a.Id], [.. formerIds[a.Id]]))];
    }

    /// <summary>Builds the answer for a place that no Area contains.</summary>
    /// <returns>A 422 answer that tells the user to ask a Maintenance Manager to draw an Area.</returns>
    private static IResult OutsideEveryArea() =>
        Results.UnprocessableEntity(new { title = "This spot is outside every area. Ask a Maintenance Manager to draw one." });

    /// <summary>Builds the answer for a stale change.</summary>
    /// <returns>A 409 answer that tells the user to read the Asset again and redo the change.</returns>
    private static IResult StaleAsset() =>
        Results.Conflict(new { title = "Someone else changed this Asset. Read it again and redo your change." });

    /// <summary>Builds the answer for a place that is not a longitude and latitude on the Earth.</summary>
    /// <returns>A 400 validation problem.</returns>
    private static IResult LocationOffTheEarth() =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["location"] = ["The longitude must be from -180 to 180 and the latitude from -90 to 90."],
        });

    /// <summary>Finds out whether a place is a usable longitude and latitude.</summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <returns><see langword="true"/> when both are numbers inside the range of the Earth.</returns>
    private static bool IsOnTheEarth(double longitude, double latitude) =>
        double.IsFinite(longitude) && double.IsFinite(latitude) && Math.Abs(longitude) <= 180 && Math.Abs(latitude) <= 90;
}
