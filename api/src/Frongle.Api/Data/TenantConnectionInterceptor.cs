using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Frongle.Api.Data;

/// <summary>
/// Tells Postgres which tenant the caller belongs to each time a connection opens, so row-level security
/// can hide other tenants' rows even from a query that has no filter.
/// </summary>
/// <param name="caller">The signed-in user whose tenant Postgres is told about.</param>
public sealed class TenantConnectionInterceptor(ICaller caller) : DbConnectionInterceptor
{
    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateSetTenantCommand(connection);
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = CreateSetTenantCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // A pooled connection keeps its session settings, so every open sets the tenant, even to an empty one.
    private DbCommand CreateSetTenantCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT set_config('{TenantSecurity.SettingName}', @tenant, false)";
        var tenant = command.CreateParameter();
        tenant.ParameterName = "tenant";
        tenant.Value = caller.TenantId ?? "";
        command.Parameters.Add(tenant);
        return command;
    }
}
