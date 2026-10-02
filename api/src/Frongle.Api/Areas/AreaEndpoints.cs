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
    /// <summary>The name of the authorization policy that only Maintenance Managers meet.</summary>
    public const string ManagerPolicy = "MaintenanceManager";

    /// <summary>The name of the authorization policy that Maintenance Managers and Work Teams meet.</summary>
    public const string ReaderPolicy = "AreaReader";

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
            .RequireAuthorization(ReaderPolicy)
            .WithSummary("List the Areas of your tenant")
            .WithDescription("Returns every Area with its code, name, and boundary. Maintenance Managers and Work Teams can both read it.")
            .Produces<IEnumerable<AreaResponse>>()
            .Produces(StatusCodes.Status403Forbidden);

        areas.MapPost("/", CreateArea)
            .RequireAuthorization(ManagerPolicy)
            .WithSummary("Add an Area")
            .WithDescription("Maintenance Managers only. The code must be two capital letters that no other Area of the tenant uses. The boundary must be a valid GeoJSON Polygon that does not overlap another Area. Sharing an edge is allowed.")
            .Produces<AreaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden);

        areas.MapPut("/{id:guid}", ChangeArea)
            .RequireAuthorization(ManagerPolicy)
            .WithSummary("Change the name and boundary of an Area")
            .WithDescription("Maintenance Managers only. The code cannot change. The new boundary must not overlap another Area.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden);

        areas.MapDelete("/{id:guid}", DeleteArea)
            .RequireAuthorization(ManagerPolicy)
            .WithSummary("Delete an Area")
            .WithDescription("Maintenance Managers only.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
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

        return await SerializedPerTenant(db, caller, async () =>
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
    /// <returns>204 when the Area changed, 400 when the request is invalid, 404 when the tenant has no such Area, or 409 when the boundary overlaps another Area.</returns>
    private static async Task<IResult> ChangeArea(Guid id, AreaChange change, FrongleDbContext db, ICaller caller)
    {
        var problems = Validate(change.Name, change.Boundary, out var boundary);
        if (problems.Count > 0) return Results.ValidationProblem(problems);

        return await SerializedPerTenant(db, caller, async () =>
        {
            var area = await db.Areas.SingleOrDefaultAsync(a => a.Id == id);
            if (area is null) return Results.NotFound();
            if (await Overlaps(db, boundary, ignoring: id))
                return Results.Conflict(new { title = "The boundary overlaps another Area." });

            area.Name = change.Name.Trim();
            area.Boundary = boundary;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// <summary>Deletes an Area.</summary>
    /// <param name="id">The Area to delete.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <returns>204 when the Area was deleted, or 404 when the tenant has no such Area.</returns>
    private static async Task<IResult> DeleteArea(Guid id, FrongleDbContext db)
    {
        var area = await db.Areas.SingleOrDefaultAsync(a => a.Id == id);
        if (area is null) return Results.NotFound();

        db.Areas.Remove(area);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

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

    /// <summary>
    /// Runs a check-then-write for one tenant at a time. The overlap check reads other Areas, so without this lock
    /// two requests could both see no overlap and then both save. The lock ends with the transaction.
    /// </summary>
    /// <param name="db">The database to write to.</param>
    /// <param name="caller">The signed-in user, whose tenant is the lock key.</param>
    /// <param name="write">The check and the write, which run while the lock is held.</param>
    /// <returns>What <paramref name="write"/> returned, after the transaction committed.</returns>
    private static async Task<IResult> SerializedPerTenant(FrongleDbContext db, ICaller caller, Func<Task<IResult>> write)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Two tenants that hash to the same key only wait for each other, so a collision costs time and nothing else.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({caller.TenantId}))");
        var result = await write();
        await transaction.CommitAsync();
        return result;
    }

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
