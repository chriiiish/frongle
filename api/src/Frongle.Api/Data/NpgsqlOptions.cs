using Microsoft.EntityFrameworkCore;

namespace Frongle.Api.Data;

/// <summary>Database connection settings that the API and its tests share.</summary>
public static class NpgsqlOptions
{
    /// <summary>Connects to Postgres with support for PostGIS geometry types.</summary>
    /// <param name="options">The options of the context that needs the connection.</param>
    /// <param name="connectionString">How to reach the database.</param>
    /// <returns>The same options, so that calls can be chained.</returns>
    public static DbContextOptionsBuilder UseFrongleNpgsql(this DbContextOptionsBuilder options, string? connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite());
}
