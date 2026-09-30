using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kairion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SourceAdapters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "provider_settings_json",
                table: "research_projects",
                type: "jsonb",
                maxLength: 16000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "source_ingestion_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    candidate_count = table.Column<int>(type: "integer", nullable: false),
                    diagnostic_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    retrieved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    retry_after_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_ingestion_runs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_source_runs_project_retrieved",
                table: "source_ingestion_runs",
                columns: new[] { "project_id", "retrieved_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_source_runs_project_run",
                table: "source_ingestion_runs",
                columns: new[] { "project_id", "run_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "source_ingestion_runs");

            migrationBuilder.DropColumn(
                name: "provider_settings_json",
                table: "research_projects");
        }
    }
}
