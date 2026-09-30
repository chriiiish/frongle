using System.Net;

namespace Frongle.Api.Tests;

public class HealthEndpointTests(FrongleApiFactory factory) : IClassFixture<FrongleApiFactory>
{
    [Fact]
    public async Task Get_health_returns_ok()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ready_returns_ok_when_the_database_is_reachable()
    {
        var response = await factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
