using System.Net;
using System.Net.Http.Json;

namespace Frongle.Api.Tests;

public class MeEndpointTests(FrongleApiFactory factory) : IClassFixture<FrongleApiFactory>
{
    private sealed record Me(string Name);

    [Fact]
    public async Task Get_me_without_a_token_is_unauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_me_without_a_tenant_claim_is_forbidden()
    {
        var client = factory.CreateClientFor(tenantId: null, "maintenance-manager");

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_me_returns_the_name_from_the_token()
    {
        var client = factory.CreateClientFor("acme", "maintenance-manager");
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Ada Lovelace");

        var me = await client.GetFromJsonAsync<Me>("/api/me");

        Assert.Equal("Ada Lovelace", me!.Name);
    }

    [Fact]
    public async Task Get_me_returns_the_username_when_the_token_has_no_name()
    {
        var client = factory.CreateClientFor("acme", "work-team");
        client.DefaultRequestHeaders.Add(TestAuthHandler.UsernameHeader, "team@acme.test");

        var me = await client.GetFromJsonAsync<Me>("/api/me");

        Assert.Equal("team@acme.test", me!.Name);
    }
}
