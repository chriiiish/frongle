using Microsoft.EntityFrameworkCore;

namespace Frongle.Api.Data;

/// <summary>Makes a check-then-write run alone, so that two requests cannot both pass the check and then both write.</summary>
public static class AdvisoryLock
{
    /// <summary>Runs work in a transaction that holds a Postgres advisory lock until the transaction ends.</summary>
    /// <typeparam name="T">What the work returns.</typeparam>
    /// <param name="db">The database to write to.</param>
    /// <param name="key">What the lock protects. Requests with the same key wait for each other, for example a tenant or an Event.</param>
    /// <param name="work">The check and the write, which run while the lock is held.</param>
    /// <returns>What <paramref name="work"/> returned, after the transaction committed.</returns>
    public static async Task<T> Run<T>(FrongleDbContext db, string key, Func<Task<T>> work)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Two keys that hash to the same number only wait for each other, so a collision costs time and nothing else.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))");
        var result = await work();
        await transaction.CommitAsync();
        return result;
    }
}
