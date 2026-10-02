using System;
using Frongle.Api.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frongle.Api.Migrations
{
    /// <inheritdoc />
    public partial class TenantFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS postgis;");

            migrationBuilder.CreateTable(
                name: "audit_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    operation = table.Column<string>(type: "text", nullable: false),
                    field = table.Column<string>(type: "text", nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    changed_by = table.Column<string>(type: "text", nullable: false),
                    changed_by_name = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_records", x => x.id);
                });

            migrationBuilder.Sql(TenantSecurity.EnableRowLevelSecurity("audit_records"));
            migrationBuilder.Sql("""
                CREATE FUNCTION refuse_audit_record_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_records is append-only';
                END $$;
                CREATE TRIGGER audit_records_append_only
                    BEFORE UPDATE OR DELETE ON audit_records
                    FOR EACH ROW EXECUTE FUNCTION refuse_audit_record_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_records");

            migrationBuilder.Sql("DROP FUNCTION refuse_audit_record_change();");
        }
    }
}
