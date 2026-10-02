using Frongle.Api.Data;
using Frongle.Domain;
using Microsoft.EntityFrameworkCore;

namespace Frongle.Api.Assets;

/// <summary>Hands out the numbers in Friendly Ids. A number is never handed out twice.</summary>
public static class AssetNumbers
{
    /// <summary>
    /// Takes the next number for a tenant, Area, and type. The counter row stays locked until the caller's
    /// transaction ends, so two callers cannot take the same number.
    /// </summary>
    /// <param name="db">The database, which must have a transaction open.</param>
    /// <param name="tenantId">The tenant that owns the Asset.</param>
    /// <param name="areaId">The Area that contains the Asset.</param>
    /// <param name="type">The type of the Asset.</param>
    /// <returns>The next number, starting at 1.</returns>
    public static async Task<int> TakeNext(FrongleDbContext db, string tenantId, Guid areaId, AssetType type)
    {
        var typeName = type.ToString();
        var numbers = await db.Database.SqlQuery<int>($"""
            INSERT INTO asset_number_counters (tenant_id, area_id, asset_type, last_number)
            VALUES ({tenantId}, {areaId}, {typeName}, 1)
            ON CONFLICT (tenant_id, area_id, asset_type)
            DO UPDATE SET last_number = asset_number_counters.last_number + 1
            RETURNING last_number AS "Value"
            """).ToListAsync();
        return numbers.Single();
    }
}
