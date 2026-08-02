using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActionConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ActionItems_CompletedAt",
                table: "ActionItems",
                sql: "(\"Status\" = 'Completed' AND \"CompletedAt\" IS NOT NULL) OR (\"Status\" <> 'Completed' AND \"CompletedAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ActionItems_GeneratedSourceKey",
                table: "ActionItems",
                sql: "(\"Source\" = 'Generated' AND \"GeneratedSourceKey\" IS NOT NULL) OR (\"Source\" = 'Manual' AND \"GeneratedSourceKey\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ActionItems_Source",
                table: "ActionItems",
                sql: "\"Source\" IN ('Generated', 'Manual')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ActionItems_Status",
                table: "ActionItems",
                sql: "\"Status\" IN ('Open', 'InProgress', 'Blocked', 'Completed', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ActionItems_CompletedAt",
                table: "ActionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ActionItems_GeneratedSourceKey",
                table: "ActionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ActionItems_Source",
                table: "ActionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ActionItems_Status",
                table: "ActionItems");
        }
    }
}
