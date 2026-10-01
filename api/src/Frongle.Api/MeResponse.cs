namespace Frongle.Api;

/// <summary>Who the caller is, as the API read it from the caller's token.</summary>
/// <param name="Name">The caller's full name from the <c>name</c> claim, or the username when the token has no name.</param>
public sealed record MeResponse(string? Name);
