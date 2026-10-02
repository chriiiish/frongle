namespace Frongle.Api.Auth;

/// <summary>The Keycloak roles that a user can have.</summary>
public static class Roles
{
    /// <summary>A user who sees Asset status, draws Areas, and sets Maintenance Schedules.</summary>
    public const string MaintenanceManager = "maintenance-manager";

    /// <summary>A user in a Work Team, who installs, repairs, removes, and replaces Assets.</summary>
    public const string WorkTeam = "work-team";
}
