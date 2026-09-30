using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kairion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompetitorGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "competitors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_competitors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "source_item_competitors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competitor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_item_competitors", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_competitors_project",
                table: "competitors",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ux_competitors_project_name",
                table: "competitors",
                columns: new[] { "project_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_source_item_competitors_competitor",
                table: "source_item_competitors",
                column: "competitor_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_item_competitors_project",
                table: "source_item_competitors",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ux_source_item_competitors_item_competitor",
                table: "source_item_competitors",
                columns: new[] { "source_item_id", "competitor_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "competitors");

            migrationBuilder.DropTable(
                name: "source_item_competitors");
        }
    }
}
