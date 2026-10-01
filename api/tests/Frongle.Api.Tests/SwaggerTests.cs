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
    public async Task The_openapi_spec_requires_the_bearer_token_on_protected_endpoints()
    {
        var spec = await GetOpenApiSpec();

        var security = spec.GetProperty("paths").GetProperty("/api/hello").GetProperty("get").GetProperty("security");
        Assert.True(security[0].TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task The_openapi_spec_does_not_ask_for_a_token_on_anonymous_endpoints()
    {
        var spec = await GetOpenApiSpec();

        // A document-level requirement would apply to every operation, including this one.
        Assert.False(spec.TryGetProperty("security", out _));
        Assert.False(spec.GetProperty("paths").GetProperty("/health").GetProperty("get").TryGetProperty("security", out _));
    }

    [Fact]
    public async Task The_swagger_page_is_public()
    {
        var response = await factory.CreateClient().GetAsync("/api/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_swagger_page_uses_the_frongle_title_and_stylesheet()
    {
        var page = await factory.CreateClient().GetStringAsync("/api/swagger/index.html");

        Assert.Contains("<title>Frongle API</title>", page);
        Assert.Contains("/api/swagger/frongle.css", page);
    }

    [Fact]
    public async Task The_frongle_stylesheet_is_public_and_uses_the_brand_colors()
    {
        var response = await factory.CreateClient().GetAsync("/api/swagger/frongle.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
        var css = await response.Content.ReadAsStringAsync();
        Assert.Contains("#ea4e2d", css);
        Assert.Contains("#efe4b4", css);
        Assert.Contains("#191919", css);
    }

    [Fact]
    public async Task The_frongle_logo_is_public()
    {
        var response = await factory.CreateClient().GetAsync("/api/swagger/logo.svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
    }
}
