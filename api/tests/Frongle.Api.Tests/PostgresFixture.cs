using Frongle.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Frongle.Api.Tests;

/// <summary>
/// A PostGIS database in a container. The API connects as <c>frongle_app</c>, which is not a superuser,
/// because Postgres does not apply row-level security to superusers.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string AppRole = "frongle_app";
    private const string AppPassword = "frongle_app";
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("imresamu/postgis:17-3.5-bookworm").Build();

    public string AppConnectionString => new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
    {
        Username = AppRole,
        Password = AppPassword,
    }.ConnectionString;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<FrongleDbContext>();
        options.UseFrongleNpgsql(_container.GetConnectionString());
        await using var owner = new FrongleDbContext(options.Options, new NoCaller());
        await owner.Database.MigrateAsync();
        await owner.Database.ExecuteSqlRawAsync(ProbeDbContext.CreateTable);
        await owner.Database.ExecuteSqlRawAsync(TenantSecurity.EnableRowLevelSecurity("probe_notes"));
        await owner.Database.ExecuteSqlRawAsync($"""
            CREATE ROLE {AppRole} LOGIN PASSWORD '{AppPassword}' NOSUPERUSER NOBYPASSRLS;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {AppRole};
            """);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private sealed class NoCaller : ICaller
    {
        public string? TenantId => null;
        public string? UserId => null;
        public string? Name => null;
    }
}

[CollectionDefinition(Name)]
public class PostgresTests : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
