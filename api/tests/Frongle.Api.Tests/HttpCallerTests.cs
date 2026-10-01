using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Frongle.Api.Tests;

public sealed class HttpCallerTests
{
    private static HttpCaller CallerWith(params Claim[] claims) => new(new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) },
    });

    [Fact]
    public void The_user_id_is_the_sub_claim_of_a_token_that_keeps_its_original_claim_names()
    {
        var caller = CallerWith(new Claim("sub", "keycloak-user-1"), new Claim("tenant_id", "acme"));

        Assert.Equal(("keycloak-user-1", "acme"), (caller.UserId, caller.TenantId));
    }

    [Fact]
    public void The_user_id_is_the_name_identifier_claim_of_a_token_whose_claims_were_renamed()
    {
        var caller = CallerWith(new Claim(ClaimTypes.NameIdentifier, "keycloak-user-2"));

        Assert.Equal("keycloak-user-2", caller.UserId);
    }

    [Fact]
    public void Nobody_is_signed_in_without_an_http_request()
    {
        var caller = new HttpCaller(new HttpContextAccessor());

        Assert.Equal((null, null, null), (caller.UserId, caller.TenantId, caller.Name));
    }
}
