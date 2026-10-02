using Microsoft.AspNetCore.Hosting;
using Npgsql;

namespace Frongle.Api.Tests;

[Collection(PostgresTests.Name)]
public sealed class MigrationTests(PostgresFixture database)
{
    [Fact]
    public async Task The_API_builds_the_schema_with_the_migrations_connection_when_the_app_connection_cannot_create_tables()
    {
        var owner = await database.CreateEmptyDatabase();
        var app = new NpgsqlConnectionStringBuilder(owner) { Username = "frongle_app", Password = "frongle_app" }.ConnectionString;
        using var factory = new MigratingApiFactory(app, owner);

        using var client = factory.CreateClient();

        await using var connection = new NpgsqlConnection(owner);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT to_regclass('audit_records') IS NOT NULL", connection);
        Assert.True((bool)(await command.ExecuteScalarAsync())!);
    }

    private sealed class MigratingApiFactory(string appConnection, string migrationsConnection) : FrongleApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("ConnectionStrings:Frongle", appConnection);
            builder.UseSetting("ConnectionStrings:Migrations", migrationsConnection);
            base.ConfigureWebHost(builder);
        }
    }
}
