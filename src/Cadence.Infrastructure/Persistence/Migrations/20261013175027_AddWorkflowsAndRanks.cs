using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cadence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowsAndRanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rank",
                table: "issues",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                collation: "C");

            migrationBuilder.AddColumn<Guid>(
                name: "status_id",
                table: "issues",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "issues",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "workflows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    restrict_transitions = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflows", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflows_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workflows_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    wip_limit = table.Column<int>(type: "integer", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_statuses", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_statuses_workflows_workflow_id",
                        column: x => x.workflow_id,
                        principalTable: "workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_transitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    require_assignee = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_by = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_transitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_transitions_workflow_statuses_from_status_id",
                        column: x => x.from_status_id,
                        principalTable: "workflow_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workflow_transitions_workflow_statuses_to_status_id",
                        column: x => x.to_status_id,
                        principalTable: "workflow_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workflow_transitions_workflows_workflow_id",
                        column: x => x.workflow_id,
                        principalTable: "workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Existing projects get the default workflow (Workflow.CreateDefault), issues take the
            // status of their category, and ranks are spaced in creation order (Rank.AtPosition):
            // "d" and four base-62 digits. PostgreSQL 18's uuidv7() keeps new ids time-ordered.
            migrationBuilder.Sql("""
                INSERT INTO workflows (id, organization_id, project_id, restrict_transitions)
                SELECT uuidv7(), organization_id, id, false FROM projects;

                INSERT INTO workflow_statuses (id, organization_id, workflow_id, project_id, name, category, position, wip_limit, is_default)
                SELECT uuidv7(), w.organization_id, w.id, w.project_id, t.name, t.category, t.category, NULL, t.category = 1
                FROM workflows w
                CROSS JOIN (VALUES ('Backlog', 0), ('Todo', 1), ('In progress', 2), ('In review', 3), ('Done', 4), ('Canceled', 5)) AS t (name, category)
                ORDER BY w.id, t.category;

                UPDATE issues i SET status_id = s.id
                FROM workflow_statuses s
                WHERE s.project_id = i.project_id AND s.category = i.status;

                UPDATE issues i SET rank = 'd'
                    || substr(d.digits, (r.n / 238328) % 62 + 1, 1)
                    || substr(d.digits, (r.n / 3844) % 62 + 1, 1)
                    || substr(d.digits, (r.n / 62) % 62 + 1, 1)
                    || substr(d.digits, r.n % 62 + 1, 1)
                FROM (SELECT id, (row_number() OVER (PARTITION BY project_id ORDER BY id) - 1)::int AS n FROM issues) r,
                     (SELECT '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz'::text AS digits) d
                WHERE r.id = i.id;

                ALTER TABLE issues ALTER COLUMN rank DROP DEFAULT, ALTER COLUMN status_id DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_issues_long_ranks",
                table: "issues",
                column: "project_id",
                filter: "length(rank) > 12");

            migrationBuilder.CreateIndex(
                name: "ix_issues_project_id_rank",
                table: "issues",
                columns: new[] { "project_id", "rank" });

            migrationBuilder.CreateIndex(
                name: "ix_issues_status_id",
                table: "issues",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_statuses_project_id",
                table: "workflow_statuses",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_statuses_workflow_id",
                table: "workflow_statuses",
                column: "workflow_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_transitions_from_status_id",
                table: "workflow_transitions",
                column: "from_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_transitions_to_status_id",
                table: "workflow_transitions",
                column: "to_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_transitions_workflow_id_from_status_id_to_status_id",
                table: "workflow_transitions",
                columns: new[] { "workflow_id", "from_status_id", "to_status_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflows_organization_id",
                table: "workflows",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflows_project_id",
                table: "workflows",
                column: "project_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_issues_workflow_statuses_status_id",
                table: "issues",
                column: "status_id",
                principalTable: "workflow_statuses",
                principalColumn: "id");

            // Checked at commit: the workflow editor deletes statuses and moves their issues in one
            // transaction.
            migrationBuilder.Sql("ALTER TABLE issues ALTER CONSTRAINT fk_issues_workflow_statuses_status_id DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_issues_workflow_statuses_status_id",
                table: "issues");

            migrationBuilder.DropTable(
                name: "workflow_transitions");

            migrationBuilder.DropTable(
                name: "workflow_statuses");

            migrationBuilder.DropTable(
                name: "workflows");

            migrationBuilder.DropIndex(
                name: "ix_issues_long_ranks",
                table: "issues");

            migrationBuilder.DropIndex(
                name: "ix_issues_project_id_rank",
                table: "issues");

            migrationBuilder.DropIndex(
                name: "ix_issues_status_id",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "rank",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "status_id",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "issues");
        }
    }
}
