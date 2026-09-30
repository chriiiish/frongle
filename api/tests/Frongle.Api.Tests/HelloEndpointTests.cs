using System.Net;
using System.Net.Http.Json;

namespace Frongle.Api.Tests;

public class HelloEndpointTests(FrongleApiFactory factory) : IClassFixture<FrongleApiFactory>
{
    private sealed record Hello(string Greeting, string TenantId, string[] Roles);

    [Fact]
    public async Task Get_hello_without_a_token_is_unauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/hello");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_hello_without_a_tenant_claim_is_forbidden()
    {
        var client = factory.CreateClientFor(tenantId: null, "maintenance-manager");

        var response = await client.GetAsync("/api/hello");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_hello_returns_the_tenant_and_roles_from_the_token()
    {
        var client = factory.CreateClientFor("acme", "maintenance-manager");

        var hello = await client.GetFromJsonAsync<Hello>("/api/hello");

        Assert.Equal("Hello from Frongle", hello!.Greeting);
        Assert.Equal("acme", hello.TenantId);
        Assert.Equal(["maintenance-manager"], hello.Roles);
    }
}
