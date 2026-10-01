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
    public async Task The_openapi_spec_is_public_and_lists_the_me_endpoint()
    {
        var spec = await GetOpenApiSpec();

        Assert.True(spec.GetProperty("paths").TryGetProperty("/api/me", out _));
    }

    [Fact]
    public async Task The_openapi_spec_describes_signing_in_with_keycloak_using_the_authorization_code_flow()
    {
        var spec = await GetOpenApiSpec();

        var scheme = spec.GetProperty("components").GetProperty("securitySchemes").GetProperty("Keycloak");
        Assert.Equal("oauth2", scheme.GetProperty("type").GetString());
        var flow = scheme.GetProperty("flows").GetProperty("authorizationCode");
        Assert.Equal("http://keycloak.test/realms/frongle/protocol/openid-connect/auth", flow.GetProperty("authorizationUrl").GetString());
        Assert.Equal("http://keycloak.test/realms/frongle/protocol/openid-connect/token", flow.GetProperty("tokenUrl").GetString());
        Assert.True(flow.GetProperty("scopes").TryGetProperty("openid", out _));
    }

    [Fact]
    public async Task The_openapi_spec_requires_a_keycloak_sign_in_on_protected_endpoints()
    {
        var spec = await GetOpenApiSpec();

        var security = spec.GetProperty("paths").GetProperty("/api/me").GetProperty("get").GetProperty("security");
        Assert.True(security[0].TryGetProperty("Keycloak", out _));
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

    [Fact]
    public async Task The_openapi_spec_describes_the_api()
    {
        var info = (await GetOpenApiSpec()).GetProperty("info");

        Assert.Equal("Frongle API", info.GetProperty("title").GetString());
        Assert.Equal("v1", info.GetProperty("version").GetString());
        Assert.Contains("assets", info.GetProperty("description").GetString());
        Assert.Contains("Keycloak", info.GetProperty("description").GetString());
        Assert.Equal("https://github.com/chriiiish/frongle", info.GetProperty("contact").GetProperty("url").GetString());
    }

    [Fact]
    public async Task The_me_operation_is_documented_with_its_responses()
    {
        var spec = await GetOpenApiSpec();

        var me = spec.GetProperty("paths").GetProperty("/api/me").GetProperty("get");
        Assert.False(string.IsNullOrWhiteSpace(me.GetProperty("summary").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(me.GetProperty("description").GetString()));
        Assert.Equal("Me", me.GetProperty("tags")[0].GetString());
        var responses = me.GetProperty("responses");
        Assert.True(responses.TryGetProperty("200", out var ok));
        Assert.True(responses.TryGetProperty("401", out _));
        Assert.True(responses.TryGetProperty("403", out _));
        Assert.EndsWith("/MeResponse", ok.GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task The_health_operation_is_documented()
    {
        var spec = await GetOpenApiSpec();

        var health = spec.GetProperty("paths").GetProperty("/health").GetProperty("get");
        Assert.False(string.IsNullOrWhiteSpace(health.GetProperty("summary").GetString()));
        Assert.Equal("Health", health.GetProperty("tags")[0].GetString());
    }

    [Fact]
    public async Task The_swagger_page_signs_in_with_the_web_clients_keycloak_client_using_pkce()
    {
        var script = await factory.CreateClient().GetStringAsync("/api/swagger/index.js");

        Assert.Contains("\"clientId\":\"frongle-web\"", script);
        Assert.Contains("\"usePkceWithAuthorizationCodeGrant\":true", script);
    }
}
