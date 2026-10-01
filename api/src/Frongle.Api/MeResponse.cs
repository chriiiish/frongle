namespace Frongle.Api;

/// <summary>Who the caller is and which tenant they belong to, as the API read it from the caller's token.</summary>
/// <param name="Name">The caller's full name from the <c>name</c> claim, or the username when the token has no name.</param>
/// <param name="Tenant">The caller's tenant from the <c>tenant_id</c> claim, which is the organization whose data the caller can see.</param>
public sealed record MeResponse(string? Name, string? Tenant);
