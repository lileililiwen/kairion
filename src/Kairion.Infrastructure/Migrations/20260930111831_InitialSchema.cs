using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kairion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cluster_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deep_analysis_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cluster_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deep_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    problem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    context = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    current_solution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    dissatisfaction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    workaround = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    desired_outcome = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    price_sensitivity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    pain_strength = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    provider_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deep_analyses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "human_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_human_revisions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observed_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    first_observed_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pain_clusters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    review_state_version = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pain_clusters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "research_projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    brief_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    brief_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    topics = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    enabled_source_provider_ids = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    included_competitors = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    source_query_strategy = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_window_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_window_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    archived_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "screening_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    relevance = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    pain = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    commercial_hint = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    spam = table.Column<bool>(type: "boolean", nullable: false),
                    decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_screening_results", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "source_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    external_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    canonical_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    excerpt = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    published_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    observed_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    provenance_json = table.Column<string>(type: "jsonb", nullable: false),
                    duplicate_of_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    latest_observation_provider_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    latest_observation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    latest_observation_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    latest_observation_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    latest_observation_observed_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    latest_observation_http_status = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cluster_assignments_cluster",
                table: "cluster_assignments",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "ix_cluster_assignments_source",
                table: "cluster_assignments",
                column: "source_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_deep_analyses_source_item",
                table: "deep_analyses",
                column: "source_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_deep_analyses_source_version",
                table: "deep_analyses",
                columns: new[] { "source_item_id", "schema_version" });

            migrationBuilder.CreateIndex(
                name: "ix_human_revisions_cluster",
                table: "human_revisions",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "ix_observations_project_cluster_observed",
                table: "observations",
                columns: new[] { "project_id", "cluster_id", "observed_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_observations_project_observed",
                table: "observations",
                columns: new[] { "project_id", "observed_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_observations_source",
                table: "observations",
                column: "source_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_pain_clusters_project",
                table: "pain_clusters",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_screening_results_source_item",
                table: "screening_results",
                column: "source_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_screening_results_source_version",
                table: "screening_results",
                columns: new[] { "source_item_id", "analysis_version" });

            migrationBuilder.CreateIndex(
                name: "ix_source_items_canonical_url",
                table: "source_items",
                columns: new[] { "project_id", "canonical_url" });

            migrationBuilder.CreateIndex(
                name: "ix_source_items_observed_utc",
                table: "source_items",
                column: "observed_utc");

            migrationBuilder.CreateIndex(
                name: "ux_source_items_identity",
                table: "source_items",
                columns: new[] { "project_id", "provider_id", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cluster_assignments");

            migrationBuilder.DropTable(
                name: "deep_analyses");

            migrationBuilder.DropTable(
                name: "human_revisions");

            migrationBuilder.DropTable(
                name: "observations");

            migrationBuilder.DropTable(
                name: "pain_clusters");

            migrationBuilder.DropTable(
                name: "research_projects");

            migrationBuilder.DropTable(
                name: "screening_results");

            migrationBuilder.DropTable(
                name: "source_items");
        }
    }
}
