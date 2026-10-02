using System;
using Frongle.Api.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frongle.Api.Migrations
{
    /// <inheritdoc />
    public partial class Retag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "needs_retag",
                table: "assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "former_friendly_ids",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    friendly_id = table.Column<string>(type: "text", nullable: false),
                    replaced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_former_friendly_ids", x => x.id);
                    table.ForeignKey(
                        name: "FK_former_friendly_ids_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_former_friendly_ids_asset_id",
                table: "former_friendly_ids",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_former_friendly_ids_tenant_id_friendly_id",
                table: "former_friendly_ids",
                columns: new[] { "tenant_id", "friendly_id" },
                unique: true);

            migrationBuilder.Sql(TenantSecurity.EnableRowLevelSecurity("former_friendly_ids"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "former_friendly_ids");

            migrationBuilder.DropColumn(
                name: "needs_retag",
                table: "assets");
        }
    }
}
