namespace Frongle.Api;

/// <summary>A caller for work that nobody started, such as building the schema when the API starts. It carries no tenant, user, or name.</summary>
public sealed class NoCaller : ICaller
{
    /// <inheritdoc />
    public string? TenantId => null;

    /// <inheritdoc />
    public string? UserId => null;

    /// <inheritdoc />
    public string? Name => null;
}
