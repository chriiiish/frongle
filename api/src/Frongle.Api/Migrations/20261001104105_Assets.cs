using System;
using Frongle.Api.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Frongle.Api.Migrations
{
    /// <inheritdoc />
    public partial class Assets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_code = table.Column<string>(type: "text", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    friendly_id = table.Column<string>(type: "text", nullable: false),
                    location = table.Column<Point>(type: "geography(Point, 4326)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assets", x => x.id);
                    table.ForeignKey(
                        name: "FK_assets_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assets_area_id",
                table: "assets",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_assets_location",
                table: "assets",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_assets_tenant_id_area_id_type_number",
                table: "assets",
                columns: new[] { "tenant_id", "area_id", "type", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assets_tenant_id_friendly_id",
                table: "assets",
                columns: new[] { "tenant_id", "friendly_id" },
                unique: true);

            migrationBuilder.Sql(TenantSecurity.EnableRowLevelSecurity("assets"));

            // Not an entity: only the number generator uses it, and the audit trail is the record of Assets.
            migrationBuilder.Sql("""
                CREATE TABLE asset_number_counters (
                    tenant_id text NOT NULL,
                    area_id uuid NOT NULL REFERENCES areas (id),
                    asset_type text NOT NULL,
                    last_number integer NOT NULL,
                    PRIMARY KEY (tenant_id, area_id, asset_type)
                );
                """);
            migrationBuilder.Sql(TenantSecurity.EnableRowLevelSecurity("asset_number_counters"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_number_counters");

            migrationBuilder.DropTable(
                name: "assets");
        }
    }
}
