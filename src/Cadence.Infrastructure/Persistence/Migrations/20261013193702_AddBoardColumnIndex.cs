using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cadence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardColumnIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_issues_status_id",
                table: "issues");

            migrationBuilder.CreateIndex(
                name: "ix_issues_status_id_rank_id",
                table: "issues",
                columns: new[] { "status_id", "rank", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_issues_status_id_rank_id",
                table: "issues");

            migrationBuilder.CreateIndex(
                name: "ix_issues_status_id",
                table: "issues",
                column: "status_id");
        }
    }
}
