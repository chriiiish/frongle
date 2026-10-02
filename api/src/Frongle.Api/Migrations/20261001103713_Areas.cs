using System;
using Frongle.Api.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Frongle.Api.Migrations
{
    /// <inheritdoc />
    public partial class Areas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "areas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    boundary = table.Column<Polygon>(type: "geography(Polygon, 4326)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_areas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_areas_boundary",
                table: "areas",
                column: "boundary")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_areas_tenant_id_code",
                table: "areas",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.Sql(TenantSecurity.EnableRowLevelSecurity("areas"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "areas");
        }
    }
}
