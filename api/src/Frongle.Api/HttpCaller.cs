using System.Security.Claims;

namespace Frongle.Api;

/// <summary>Reads the caller from the user of the current HTTP request.</summary>
/// <param name="accessor">Gives the current HTTP request, whose user the API read from the token.</param>
public sealed class HttpCaller(IHttpContextAccessor accessor) : ICaller
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    /// <inheritdoc />
    public string? TenantId => User?.FindFirstValue("tenant_id");

    /// <inheritdoc />
    // Whether the token handler renames "sub" to a name identifier claim depends on its settings, so accept both.
    public string? UserId => User?.FindFirstValue("sub") ?? User?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <inheritdoc />
    public string? Name => User?.FindFirstValue("name") ?? User?.FindFirstValue("preferred_username");
}
