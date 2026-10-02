using Frongle.Api.Data;
using Frongle.Domain;
using Microsoft.EntityFrameworkCore;

namespace Frongle.Api.Assets;

/// <summary>An Event that the caller wants to add to an Asset, or the corrected form of an Event that the Asset already has.</summary>
/// <param name="Type">The kind of Event.</param>
/// <param name="Title">A short line that says what happened. It cannot be blank.</param>
/// <param name="Notes">More detail, or <see langword="null"/> when there is none.</param>
/// <param name="OccurredAt">When it happened. It cannot be in the future.</param>
/// <param name="Version">When the caller corrects an Event, the version that the caller read. The API ignores it when the caller adds an Event.</param>
public sealed record EventRequest(EventType Type, string Title, string? Notes, DateTimeOffset OccurredAt, uint Version = 0);

/// <summary>An Event as the API describes it.</summary>
/// <param name="Id">The identifier of the Event.</param>
/// <param name="AssetId">The Internal Id of the Asset that the Event happened to.</param>
/// <param name="Type">The kind of Event.</param>
/// <param name="Title">A short line that says what happened.</param>
/// <param name="Notes">More detail, or <see langword="null"/> when there is none.</param>
/// <param name="OccurredAt">When it happened.</param>
/// <param name="Version">The version to send back when correcting the Event, so that the API can tell when someone else changed it first.</param>
/// <param name="Images">The photos of the Event that nobody removed.</param>
public sealed record EventResponse(
    Guid Id, Guid AssetId, EventType Type, string Title, string? Notes, DateTimeOffset OccurredAt, uint Version, IEnumerable<ImageResponse> Images)
{
    /// <summary>Describes an Event for the caller.</summary>
    /// <param name="ev">The Event to describe.</param>
    /// <param name="images">The photos of the Event that nobody removed.</param>
    /// <returns>The Event as the API sends it.</returns>
    public static EventResponse From(AssetEvent ev, IEnumerable<ImageResponse> images) =>
        new(ev.Id, ev.AssetId, ev.Type, ev.Title, ev.Notes, ev.OccurredAt, ev.Version, images);
}

/// <summary>The endpoints that record what happened to an Asset and show its history.</summary>
public static class EventEndpoints
{
    // Phones and servers disagree about the time by a little, so an Event a few minutes ahead is not a future Event.
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>Adds the Event endpoints to the API.</summary>
    /// <param name="app">The API to add the endpoints to.</param>
    /// <returns>The same API, so that calls can be chained.</returns>
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var events = app.MapGroup("/api/assets/{assetId:guid}/events").WithTags("Events");

        events.MapGet("/", ListEvents)
            .WithSummary("Show the history of an Asset")
            .WithDescription("Returns the Events of the Asset, newest first by when they happened.")
            .Produces<IEnumerable<EventResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        events.MapPost("/", AddEvent)
            .WithSummary("Record an Event for an Asset")
            .WithDescription("Maintenance Managers and Work Teams. An Asset that was removed takes no new Events until someone adds an Installed Event.")
            .Produces<EventResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        events.MapPut("/{id:guid}", ChangeEvent)
            .WithSummary("Correct an Event")
            .WithDescription("Send the version that you read. When someone else changed the Event since then, the API answers 409 and the caller must read the Event again. Events cannot be deleted.")
            .Produces<EventResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ListEvents(Guid assetId, FrongleDbContext db, IImageStorage storage)
    {
        if (!await db.Assets.AnyAsync(a => a.Id == assetId)) return Results.NotFound();

        var events = await db.Events
            .Where(e => e.AssetId == assetId)
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .ToListAsync();
        var images = await ImageEndpoints.ImagesOf(db, storage, [.. events.Select(e => e.Id)]);
        return Results.Ok(events.Select(e => EventResponse.From(e, images[e.Id])));
    }

    private static async Task<IResult> AddEvent(Guid assetId, EventRequest request, FrongleDbContext db)
    {
        if (Validate(request) is { } problems) return Results.ValidationProblem(problems);
        if (!await db.Assets.AnyAsync(a => a.Id == assetId)) return Results.NotFound();

        var status = (await AssetStatusLookup.For(db, [assetId]))[assetId];
        if (status == AssetStatus.Removed && request.Type != EventType.Installed)
            return Results.Conflict(new { title = "The Asset was removed. Add an Installed Event before any other Event." });

        var ev = new AssetEvent
        {
            AssetId = assetId,
            Type = request.Type,
            Title = request.Title.Trim(),
            Notes = request.Notes,
            OccurredAt = request.OccurredAt.ToUniversalTime(),
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return Results.Created($"/api/assets/{assetId}/events/{ev.Id}", EventResponse.From(ev, []));
    }

    private static async Task<IResult> ChangeEvent(Guid assetId, Guid id, EventRequest request, FrongleDbContext db, IImageStorage storage)
    {
        if (Validate(request) is { } problems) return Results.ValidationProblem(problems);
        var ev = await db.Events.SingleOrDefaultAsync(e => e.Id == id && e.AssetId == assetId);
        if (ev is null) return Results.NotFound();

        var stale = Results.Conflict(new { title = "Someone else changed this Event. Read it again and redo your change." });
        if (request.Version != ev.Version) return stale;

        db.Entry(ev).Property(e => e.Version).OriginalValue = request.Version;
        ev.Type = request.Type;
        ev.Title = request.Title.Trim();
        ev.Notes = request.Notes;
        ev.OccurredAt = request.OccurredAt.ToUniversalTime();
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return stale;
        }
        return Results.Ok(EventResponse.From(ev, (await ImageEndpoints.ImagesOf(db, storage, [id]))[id]));
    }

    private static Dictionary<string, string[]>? Validate(EventRequest request)
    {
        var problems = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title))
            problems["title"] = ["The Event needs a title."];
        if (request.OccurredAt > DateTimeOffset.UtcNow + ClockSkew)
            problems["occurredAt"] = ["An Event cannot be in the future."];
        return problems.Count > 0 ? problems : null;
    }
}
