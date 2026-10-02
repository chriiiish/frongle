using System.Text.RegularExpressions;
using Frongle.Api.Auth;
using Frongle.Api.Data;
using Frongle.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Frongle.Api.Areas;

/// <summary>The Area the caller asked to save.</summary>
/// <param name="Code">Two capital letters, unique within the tenant, for example <c>MN</c>.</param>
/// <param name="Name">The name that people use for the Area.</param>
/// <param name="Boundary">The outline of the Area on the map.</param>
public sealed record AreaRequest(string Code, string Name, GeoJsonPolygon Boundary);

/// <summary>The new name and outline for an Area. The code never changes.</summary>
/// <param name="Name">The name that people use for the Area.</param>
/// <param name="Boundary">The outline of the Area on the map.</param>
public sealed record AreaChange(string Name, GeoJsonPolygon Boundary);

/// <summary>An Area as the API describes it.</summary>
/// <param name="Id">The identifier to use when the caller changes or deletes the Area.</param>
/// <param name="Code">The two capital letters in the Friendly Id of each Asset in the Area.</param>
/// <param name="Name">The name that people use for the Area.</param>
/// <param name="Boundary">The outline of the Area on the map.</param>
public sealed record AreaResponse(Guid Id, string Code, string Name, GeoJsonPolygon Boundary)
{
    /// <summary>Describes an Area for the caller.</summary>
    /// <param name="area">The Area to describe.</param>
    /// <returns>The Area as the API sends it.</returns>
    public static AreaResponse From(Area area) => new(area.Id, area.Code, area.Name, GeoJsonPolygon.From(area.Boundary));
}

/// <summary>The endpoints that list and manage Areas.</summary>
public static partial class AreaEndpoints
{
    /// <summary>Matches a valid Area code.</summary>
    /// <returns>A pattern that accepts exactly two capital letters.</returns>
    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex TwoCapitalLetters();

    /// <summary>Adds the Area endpoints to the API.</summary>
    /// <param name="app">The API to add the endpoints to.</param>
    /// <returns>The same API, so that calls can be chained.</returns>
    public static IEndpointRouteBuilder MapAreaEndpoints(this IEndpointRouteBuilder app)
    {
        var areas = app.MapGroup("/api/areas").WithTags("Areas");

        areas.MapGet("/", async (FrongleDbContext db) =>
                (await db.Areas.OrderBy(a => a.Code).ToListAsync()).Select(AreaResponse.From))
            .RequireAuthorization(Policies.Reader)
            .WithSummary("List the Areas of your tenant")
            .WithDescription("Returns every Area with its code, name, and boundary. Maintenance Managers and Work Teams can both read it.")
            .Produces<IEnumerable<AreaResponse>>()
            .Produces(StatusCodes.Status403Forbidden);

        areas.MapPost("/", CreateArea)
            .RequireAuthorization(Policies.Manager)
            .WithSummary("Add an Area")
            .WithDescription("Maintenance Managers only. The code must be two capital letters that no other Area of the tenant uses. The boundary must be a valid GeoJSON Polygon that does not overlap another Area. Sharing an edge is allowed.")
            .Produces<AreaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden);

        areas.MapPut("/{id:guid}", ChangeArea)
            .RequireAuthorization(Policies.Manager)
            .WithSummary("Change the name and boundary of an Area")
            .WithDescription("Maintenance Managers only. The code cannot change. The new boundary must not overlap another Area and must keep every Asset of the Area inside it.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden);

        areas.MapDelete("/{id:guid}", DeleteArea)
            .RequireAuthorization(Policies.Manager)
            .WithSummary("Delete an Area")
            .WithDescription("Maintenance Managers only. An Area that has Assets cannot be deleted.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    /// <summary>Saves a new Area when its code and boundary are free.</summary>
    /// <param name="request">The Area the caller asked to save.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <param name="caller">The signed-in user, whose tenant the write is serialized for.</param>
    /// <returns>201 with the new Area, 400 when the request is invalid, or 409 when the code or boundary clashes with another Area.</returns>
    private static async Task<IResult> CreateArea(AreaRequest request, FrongleDbContext db, ICaller caller)
    {
        var code = request.Code ?? "";
        var problems = Validate(request.Name, request.Boundary, out var boundary);
        if (!TwoCapitalLetters().IsMatch(code))
            problems["code"] = ["The code must be two capital letters, for example MN."];
        if (problems.Count > 0) return Results.ValidationProblem(problems);

        return await AdvisoryLock.Run(db, caller.TenantId!, async () =>
        {
            if (await db.Areas.AnyAsync(a => a.Code == code))
                return Results.Conflict(new { title = $"Another Area already uses the code {code}." });
            if (await Overlaps(db, boundary, ignoring: null))
                return Results.Conflict(new { title = "The boundary overlaps another Area." });

            var area = new Area { Code = code, Name = request.Name.Trim(), Boundary = boundary };
            db.Areas.Add(area);
            await db.SaveChangesAsync();
            return Results.Created($"/api/areas/{area.Id}", AreaResponse.From(area));
        });
    }

    /// <summary>Changes the name and boundary of an existing Area.</summary>
    /// <param name="id">The Area to change.</param>
    /// <param name="change">The new name and boundary.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <param name="caller">The signed-in user, whose tenant the write is serialized for.</param>
    /// <returns>204 when the Area changed, 400 when the request is invalid, 404 when the tenant has no such Area, or 409 when the boundary overlaps another Area or would leave an Asset of the Area outside it.</returns>
    private static async Task<IResult> ChangeArea(Guid id, AreaChange change, FrongleDbContext db, ICaller caller)
    {
        var problems = Validate(change.Name, change.Boundary, out var boundary);
        if (problems.Count > 0) return Results.ValidationProblem(problems);

        return await AdvisoryLock.Run(db, caller.TenantId!, async () =>
        {
            var area = await db.Areas.SingleOrDefaultAsync(a => a.Id == id);
            if (area is null) return Results.NotFound();
            if (await Overlaps(db, boundary, ignoring: id))
                return Results.Conflict(new { title = "The boundary overlaps another Area." });
            var stranded = await AssetsOutside(db, id, boundary);
            if (stranded.Count > 0)
                return Results.Conflict(new { title = $"The new boundary would leave these Assets outside the Area: {string.Join(", ", stranded)}." });

            area.Name = change.Name.Trim();
            area.Boundary = boundary;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// <summary>Deletes an Area.</summary>
    /// <param name="id">The Area to delete.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <param name="caller">The signed-in user, whose tenant the write is serialized for.</param>
    /// <returns>204 when the Area was deleted, 404 when the tenant has no such Area, or 409 when the Area still has Assets.</returns>
    private static Task<IResult> DeleteArea(Guid id, FrongleDbContext db, ICaller caller) =>
        // An Asset that is added at the same moment must not slip in between the check and the delete.
        AdvisoryLock.Run(db, caller.TenantId!, async () =>
        {
            var area = await db.Areas.SingleOrDefaultAsync(a => a.Id == id);
            if (area is null) return Results.NotFound();
            if (await db.Assets.AnyAsync(a => a.AreaId == id))
                return Results.Conflict(new { title = "The Area has Assets, and their Friendly Ids use its code, so it cannot be deleted." });

            db.Areas.Remove(area);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

    /// <summary>Checks the parts of a request that both creating and changing an Area share.</summary>
    /// <param name="name">The name the caller gave the Area.</param>
    /// <param name="boundary">The outline the caller drew, as GeoJSON.</param>
    /// <param name="polygon">The outline as a polygon that can be saved, when it is valid.</param>
    /// <returns>What is wrong, by field name, or an empty set when the request is valid.</returns>
    private static Dictionary<string, string[]> Validate(string name, GeoJsonPolygon boundary, out Polygon polygon)
    {
        var problems = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name))
            problems["name"] = ["The Area needs a name."];

        polygon = null!;
        if (boundary is null)
            problems["boundary"] = ["The Area needs a boundary."];
        else if (!boundary.TryToPolygon(out polygon, out var problem))
            problems["boundary"] = [problem];
        return problems;
    }

    /// <summary>Lists the Assets that a new boundary would leave outside their Area. Friendly Ids name the Area, so a boundary change must not move an Asset into another Area.</summary>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <param name="areaId">The Area whose boundary would change.</param>
    /// <param name="boundary">The new outline of the Area.</param>
    /// <returns>The Friendly Ids of the first 20 Assets that the boundary would not cover, which is enough to act on.</returns>
    private static Task<List<string>> AssetsOutside(FrongleDbContext db, Guid areaId, Polygon boundary) =>
        db.Assets
            .FromSql($"SELECT * FROM assets WHERE area_id = {areaId} AND NOT ST_Covers({boundary}::geography, location)")
            .OrderBy(a => a.FriendlyId)
            .Select(a => a.FriendlyId)
            .Take(20)
            .ToListAsync();

    /// <summary>Finds out whether a boundary overlaps any other Area. Areas that only share an edge do not overlap.</summary>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <param name="boundary">The outline to test.</param>
    /// <param name="ignoring">An Area to leave out, so that an Area does not clash with its own old outline.</param>
    /// <returns><see langword="true"/> when another Area covers some of the same ground.</returns>
    // The pattern T******** means that the insides of the two shapes meet.
    private static Task<bool> Overlaps(FrongleDbContext db, Polygon boundary, Guid? ignoring) =>
        db.Areas
            .FromSql($"SELECT * FROM areas WHERE ST_Relate(boundary::geometry, {boundary}::geometry, 'T********')")
            .AnyAsync(a => a.Id != ignoring);
}
