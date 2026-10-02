using Frongle.Api.Auth;
using Frongle.Api.Data;
using Frongle.Domain;
using Microsoft.EntityFrameworkCore;

namespace Frongle.Api.Assets;

/// <summary>The image that the caller wants to attach to an Event.</summary>
/// <param name="ContentType">The media type of the file: <c>image/jpeg</c>, <c>image/png</c>, or <c>image/webp</c>.</param>
/// <param name="SizeBytes">The size of the file in bytes, from 1 to 10 MB.</param>
public sealed record ImageRequest(string ContentType, long SizeBytes);

/// <summary>Where the caller uploads the file.</summary>
/// <param name="Id">The identifier of the image.</param>
/// <param name="UploadUrl">A short-lived URL. The caller sends the file to it with HTTP PUT and the same Content-Type as the request.</param>
/// <param name="ContentType">The Content-Type that the upload must use.</param>
public sealed record UploadResponse(Guid Id, string UploadUrl, string ContentType);

/// <summary>An image of an Event as the API describes it.</summary>
/// <param name="Id">The identifier of the image.</param>
/// <param name="ContentType">The media type of the file.</param>
/// <param name="SizeBytes">The size of the file in bytes.</param>
/// <param name="ReadUrl">A short-lived URL that shows the file.</param>
public sealed record ImageResponse(Guid Id, string ContentType, long SizeBytes, string ReadUrl)
{
    /// <summary>Describes an image for the caller.</summary>
    /// <param name="image">The image to describe.</param>
    /// <param name="storage">Makes the link to the file.</param>
    /// <returns>The image as the API sends it.</returns>
    public static ImageResponse From(EventImage image, IImageStorage storage) =>
        new(image.Id, image.ContentType, image.SizeBytes, storage.CreateReadUrl(image.StorageKey));
}

/// <summary>The endpoints that attach photos to Events.</summary>
public static class ImageEndpoints
{
    private const long MaxSizeBytes = 10 * 1024 * 1024;
    private const int MaxImagesPerEvent = 5;
    private static readonly string[] ContentTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Adds the image endpoints to the API.</summary>
    /// <param name="app">The API to add the endpoints to.</param>
    /// <returns>The same API, so that calls can be chained.</returns>
    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        var images = app.MapGroup("/api/assets/{assetId:guid}/events/{eventId:guid}/images").WithTags("Images").RequireAuthorization(Policies.Reader);

        images.MapGet("/", ListImages)
            .WithSummary("List the images of an Event")
            .WithDescription("Returns the images that nobody removed, each with a short-lived link to the file.")
            .Produces<IEnumerable<ImageResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        images.MapPost("/", RequestUpload)
            .WithSummary("Ask for a place to upload an image")
            .WithDescription("Maintenance Managers and Work Teams. The API answers with a URL. The caller then sends the file to that URL with HTTP PUT, and the same Content-Type as in this request. An Event holds five images at most, each up to 10 MB, as JPEG, PNG, or WebP.")
            .Produces<UploadResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        images.MapDelete("/{id:guid}", RemoveImage)
            .WithSummary("Remove an image from an Event")
            .WithDescription("Hides the image. The file stays in storage and the audit trail records who removed it.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>Finds the images of Events that nobody removed.</summary>
    /// <param name="db">The database to read the images from.</param>
    /// <param name="storage">Makes the links to the files.</param>
    /// <param name="eventIds">The Events to find images for.</param>
    /// <returns>The images, grouped by Event, oldest first.</returns>
    public static async Task<ILookup<Guid, ImageResponse>> ImagesOf(FrongleDbContext db, IImageStorage storage, IReadOnlyCollection<Guid> eventIds)
    {
        var images = await db.EventImages
            .Where(i => eventIds.Contains(i.EventId) && i.RemovedAt == null)
            .OrderBy(i => i.Id)
            .ToListAsync();
        return images.ToLookup(i => i.EventId, i => ImageResponse.From(i, storage));
    }

    /// <summary>Lists the images of an Event.</summary>
    /// <param name="assetId">The Internal Id of the Asset that the Event belongs to.</param>
    /// <param name="eventId">The Event whose images to list.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <param name="storage">Makes the links to the files.</param>
    /// <returns>200 with the images, or 404 when the tenant has no such Event.</returns>
    private static async Task<IResult> ListImages(Guid assetId, Guid eventId, FrongleDbContext db, IImageStorage storage)
    {
        if (!await EventExists(db, assetId, eventId)) return Results.NotFound();

        return Results.Ok((await ImagesOf(db, storage, [eventId]))[eventId]);
    }

    /// <summary>Records an image for an Event and makes the link that the caller uploads the file to.</summary>
    /// <param name="assetId">The Internal Id of the Asset that the Event belongs to.</param>
    /// <param name="eventId">The Event to attach the image to.</param>
    /// <param name="request">The type and size of the file.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <param name="storage">Makes the link to upload to.</param>
    /// <param name="caller">The signed-in user, whose tenant starts the key of the file.</param>
    /// <returns>201 with the upload link, 400 when the file is not allowed, 404 when the tenant has no such Event, or 409 when the Event holds the most images already.</returns>
    private static async Task<IResult> RequestUpload(Guid assetId, Guid eventId, ImageRequest request, FrongleDbContext db, IImageStorage storage, ICaller caller)
    {
        var problems = new Dictionary<string, string[]>();
        if (!ContentTypes.Contains(request.ContentType))
            problems["contentType"] = ["The image must be image/jpeg, image/png, or image/webp."];
        if (request.SizeBytes is < 1 or > MaxSizeBytes)
            problems["sizeBytes"] = ["The image must be from 1 byte to 10 MB."];
        if (problems.Count > 0) return Results.ValidationProblem(problems);

        // Two uploads asked for at once must not both see room for the last image.
        return await AdvisoryLock.Run<IResult>(db, $"event-images:{eventId}", async () =>
        {
            if (!await EventExists(db, assetId, eventId)) return Results.NotFound();
            if (await db.EventImages.CountAsync(i => i.EventId == eventId && i.RemovedAt == null) >= MaxImagesPerEvent)
                return Results.Conflict(new { title = $"An Event holds {MaxImagesPerEvent} images at most. Remove one first." });

            var image = new EventImage
            {
                EventId = eventId,
                StorageKey = "",
                ContentType = request.ContentType,
                SizeBytes = request.SizeBytes,
            };
            // The key starts with the tenant, so a bucket policy can keep one tenant's files apart from another's.
            image.StorageKey = $"{caller.TenantId}/{assetId}/{eventId}/{image.Id}";
            db.EventImages.Add(image);
            await db.SaveChangesAsync();

            return Results.Created(
                $"/api/assets/{assetId}/events/{eventId}/images/{image.Id}",
                new UploadResponse(image.Id, storage.CreateUploadUrl(image.StorageKey, image.ContentType), image.ContentType));
        });
    }

    /// <summary>Hides an image of an Event.</summary>
    /// <param name="assetId">The Internal Id of the Asset that the Event belongs to.</param>
    /// <param name="eventId">The Event that the image belongs to.</param>
    /// <param name="id">The image to hide.</param>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <returns>204 when the image is hidden, or 404 when the tenant has no such image.</returns>
    private static async Task<IResult> RemoveImage(Guid assetId, Guid eventId, Guid id, FrongleDbContext db)
    {
        if (!await EventExists(db, assetId, eventId)) return Results.NotFound();
        var image = await db.EventImages.SingleOrDefaultAsync(i => i.Id == id && i.EventId == eventId && i.RemovedAt == null);
        if (image is null) return Results.NotFound();

        image.RemovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    /// <summary>Finds out whether an Event belongs to an Asset of the caller's tenant.</summary>
    /// <param name="db">The database of the caller's tenant.</param>
    /// <param name="assetId">The Internal Id of the Asset.</param>
    /// <param name="eventId">The Event to look for.</param>
    /// <returns><see langword="true"/> when the Asset has that Event.</returns>
    private static Task<bool> EventExists(FrongleDbContext db, Guid assetId, Guid eventId) =>
        db.Events.AnyAsync(e => e.Id == eventId && e.AssetId == assetId);
}
