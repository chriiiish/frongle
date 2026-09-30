using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Frongle.Api.Auth;

/// <summary>Keycloak puts roles in a JSON "realm_access" claim. This copies them into standard role claims.</summary>
public class KeycloakRolesClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (realmAccess is null || principal.Identity is not ClaimsIdentity identity)
            return Task.FromResult(principal);

        using var document = JsonDocument.Parse(realmAccess);
        if (document.RootElement.TryGetProperty("roles", out var roles))
        {
            foreach (var role in roles.EnumerateArray())
                identity.AddClaim(new Claim(identity.RoleClaimType, role.GetString()!));
        }

        return Task.FromResult(principal);
    }
}
