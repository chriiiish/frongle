using Frongle.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Frongle.Api.Tests;

/// <summary>The API on a real PostGIS database, with the probe endpoints and test sign-in.</summary>
public class PostgresApiFactory(PostgresFixture database) : FrongleApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Frongle", database.AppConnectionString);
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<ProbeDbContext>((provider, options) => options
                .UseNpgsql(database.AppConnectionString)
                .AddInterceptors(provider.GetRequiredService<TenantConnectionInterceptor>()));
            services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.AspNetCore.Hosting.IStartupFilter, ProbeEndpointsStartupFilter>());
        });
    }
}
