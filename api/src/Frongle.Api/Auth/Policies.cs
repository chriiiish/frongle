namespace Frongle.Api.Auth;

/// <summary>The names of the authorization policies that endpoints require.</summary>
public static class Policies
{
    /// <summary>Only Maintenance Managers meet this policy.</summary>
    public const string Manager = "MaintenanceManager";

    /// <summary>Maintenance Managers and Work Teams meet this policy. A user with neither role does not.</summary>
    public const string Reader = "Reader";
}
