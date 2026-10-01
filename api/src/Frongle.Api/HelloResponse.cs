namespace Frongle.Api;

/// <summary>The greeting, plus the tenant and roles that the API read from the caller's token.</summary>
/// <param name="Greeting">A fixed greeting.</param>
/// <param name="TenantId">The organization that the caller belongs to, from the <c>tenant_id</c> claim.</param>
/// <param name="Roles">The caller's roles: <c>maintenance-manager</c> or <c>work-team</c>.</param>
public sealed record HelloResponse(string Greeting, string? TenantId, IReadOnlyList<string> Roles);
