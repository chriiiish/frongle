using Frongle.Api.Data;
using Frongle.Domain;
using Microsoft.EntityFrameworkCore;

namespace Frongle.Api.Assets;

/// <summary>Finds the <see cref="AssetStatus"/> of Assets from their Events.</summary>
public static class AssetStatusLookup
{
    /// <summary>Works out the status of each Asset in one query.</summary>
    /// <param name="db">The database to read the Events from.</param>
    /// <param name="assetIds">The Internal Ids of the Assets.</param>
    /// <returns>The status of every Asset in <paramref name="assetIds"/>, including Assets that have no Events.</returns>
    public static async Task<Dictionary<Guid, AssetStatus>> For(FrongleDbContext db, IReadOnlyCollection<Guid> assetIds)
    {
        var latest = await db.Events
            .Where(e => assetIds.Contains(e.AssetId))
            .GroupBy(e => e.AssetId)
            .Select(g => new
            {
                AssetId = g.Key,
                Type = g.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).Select(e => e.Type).First(),
            })
            .ToDictionaryAsync(e => e.AssetId, e => e.Type);

        return assetIds.ToDictionary(
            id => id,
            id => AssetStatuses.From(latest.TryGetValue(id, out var type) ? type : null));
    }
}
