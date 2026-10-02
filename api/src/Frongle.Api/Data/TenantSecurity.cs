namespace Frongle.Api.Data;

/// <summary>The Postgres side of tenant isolation, which stops one tenant reading or writing another tenant's rows.</summary>
public static class TenantSecurity
{
    /// <summary>The Postgres session setting that holds the caller's tenant. The API sets it each time it opens a connection.</summary>
    public const string SettingName = "app.tenant_id";

    /// <summary>
    /// Builds the SQL that limits a table to the rows of the tenant in <see cref="SettingName"/>.
    /// The policy also applies to the table owner, and it matches no rows when the setting is empty or missing, because an empty setting counts as no tenant.
    /// </summary>
    /// <param name="table">The name of a table that has a <c>tenant_id</c> column.</param>
    /// <returns>SQL for a migration to run once for the table.</returns>
    public static string EnableRowLevelSecurity(string table) => $"""
        ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
        ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON {table}
            USING (tenant_id = NULLIF(current_setting('{SettingName}', true), ''))
            WITH CHECK (tenant_id = NULLIF(current_setting('{SettingName}', true), ''));
        """;
}
