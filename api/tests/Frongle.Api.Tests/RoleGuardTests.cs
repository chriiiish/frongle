using System.Net;
using System.Net.Http.Json;

namespace Frongle.Api.Tests;

/// <summary>A token with a tenant but without a Maintenance Manager or Work Team role must reach no Asset data.</summary>
[Collection(PostgresTests.Name)]
public sealed class RoleGuardTests(PostgresFixture database) : IDisposable
{
    private readonly PostgresApiFactory _factory = new(database);
    private readonly string _tenant = $"tenant-{Guid.NewGuid():N}";

    public void Dispose() => _factory.Dispose();

    public static TheoryData<string, string> Routes()
    {
        var any = Guid.NewGuid();
        var asset = $"/api/assets/{any}";
        var ev = $"{asset}/events";
        var image = $"{ev}/{any}/images";
        return new TheoryData<string, string>
        {
            { "GET", "/api/assets?west=174&south=-38&east=175&north=-37" },
            { "GET", "/api/assets/search?q=MN" },
            { "GET", asset },
            { "POST", "/api/assets" },
            { "PUT", $"{asset}/location" },
            { "POST", $"{asset}/retagged" },
            { "GET", $"{asset}/history" },
            { "GET", ev },
            { "POST", ev },
            { "PUT", $"{ev}/{any}" },
            { "GET", image },
            { "POST", image },
            { "DELETE", $"{image}/{any}" },
        };
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_user_without_a_frongle_role_is_refused(string method, string path)
    {
        var client = _factory.CreateClientFor(_tenant, "some-other-realm-role");
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
