namespace Frongle.Api;

/// <summary>The signed-in user who makes the current request, as read from the validated token.</summary>
public interface ICaller
{
    /// <summary>The caller's tenant from the <c>tenant_id</c> claim, or <see langword="null"/> when nobody is signed in.</summary>
    string? TenantId { get; }

    /// <summary>The caller's user identifier from the <c>sub</c> claim, or <see langword="null"/> when nobody is signed in.</summary>
    string? UserId { get; }

    /// <summary>The caller's full name, or the username when the token has no name.</summary>
    string? Name { get; }
}
