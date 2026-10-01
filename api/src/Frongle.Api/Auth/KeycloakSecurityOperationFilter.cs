using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Frongle.Api.Auth;

/// <summary>
/// Asks for the Keycloak sign-in on every operation except the anonymous ones. The fallback authorization policy
/// protects every endpoint that does not allow anonymous access, so the spec must say the same.
/// </summary>
public sealed class KeycloakSecurityOperationFilter : IOperationFilter
{
    public const string SchemeName = "Keycloak";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            return;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = ["openid"],
        });
    }
}
