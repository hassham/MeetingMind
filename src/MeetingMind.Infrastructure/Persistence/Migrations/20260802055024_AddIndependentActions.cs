using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIndependentActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Assignee = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MeetingJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProvenanceMeetingTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ProvenanceSourceFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    GeneratedSourceKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItems", x => x.Id);
                    table.CheckConstraint("CK_ActionItems_Description", "length(btrim(\"Description\")) BETWEEN 1 AND 2000");
                    table.CheckConstraint("CK_ActionItems_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_ActionItems_MeetingJobs_MeetingJobId",
                        column: x => x.MeetingJobId,
                        principalTable: "MeetingJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_CreatedAt_Id",
                table: "ActionItems",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_DueDate_Status",
                table: "ActionItems",
                columns: new[] { "DueDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_GeneratedSourceKey",
                table: "ActionItems",
                column: "GeneratedSourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_MeetingJobId_CreatedAt",
                table: "ActionItems",
                columns: new[] { "MeetingJobId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_Source_CreatedAt",
                table: "ActionItems",
                columns: new[] { "Source", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_Status_CreatedAt",
                table: "ActionItems",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionItems");
        }
    }
}
