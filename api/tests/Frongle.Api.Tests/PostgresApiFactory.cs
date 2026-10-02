using Frongle.Api.Assets;
using Frongle.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Frongle.Api.Tests;

/// <summary>The API on a real PostGIS database, with the probe endpoints and test sign-in.</summary>
public class PostgresApiFactory(PostgresFixture database, string? connectionString = null, bool migrateOnStartup = false) : FrongleApiFactory
{
    private readonly string _connectionString = connectionString ?? database.AppConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Frongle", _connectionString);
        builder.UseSetting("Database:MigrateOnStartup", migrateOnStartup.ToString());
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<FrongleDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<FrongleDbContext>>();
            services.AddDbContext<FrongleDbContext>(UsePostgres);
            services.AddDbContext<ProbeDbContext>(UsePostgres);
            services.RemoveAll<IImageStorage>();
            services.AddSingleton<IImageStorage, FakeImageStorage>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.AspNetCore.Hosting.IStartupFilter, ProbeEndpointsStartupFilter>());
        });
    }

    private void UsePostgres(IServiceProvider provider, DbContextOptionsBuilder options) => options
        .UseFrongleNpgsql(_connectionString)
        .AddInterceptors(provider.GetRequiredService<TenantConnectionInterceptor>());
}
