using System.Security.Claims;
using Frongle.Api.Auth;

namespace Frongle.Api.Tests;

public class KeycloakRolesTests
{
    [Fact]
    public async Task Realm_roles_in_the_token_become_role_claims()
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim("realm_access", """{"roles":["work-team","offline_access"]}"""));

        var principal = await new KeycloakRolesClaimsTransformation()
            .TransformAsync(new ClaimsPrincipal(identity));

        Assert.True(principal.IsInRole("work-team"));
        Assert.True(principal.IsInRole("offline_access"));
    }

    [Fact]
    public async Task A_token_without_realm_access_gets_no_roles()
    {
        var principal = await new KeycloakRolesClaimsTransformation()
            .TransformAsync(new ClaimsPrincipal(new ClaimsIdentity("test")));

        Assert.Empty(principal.FindAll(ClaimTypes.Role));
    }
}
