using System.Net;
using System.Text.Json;

namespace Frongle.Api.Tests;

public class SwaggerTests(FrongleApiFactory factory) : IClassFixture<FrongleApiFactory>
{
    private async Task<JsonElement> GetOpenApiSpec()
    {
        var response = await factory.CreateClient().GetAsync("/api/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task The_openapi_spec_is_public_and_lists_the_hello_endpoint()
    {
        var spec = await GetOpenApiSpec();

        Assert.True(spec.GetProperty("paths").TryGetProperty("/api/hello", out _));
    }

    [Fact]
    public async Task The_openapi_spec_describes_the_bearer_token_that_the_api_needs()
    {
        var spec = await GetOpenApiSpec();

        var scheme = spec.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
    }

    [Fact]
    public async Task The_swagger_page_is_public()
    {
        var response = await factory.CreateClient().GetAsync("/api/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
