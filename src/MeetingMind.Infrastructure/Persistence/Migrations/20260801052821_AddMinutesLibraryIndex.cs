using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMinutesLibraryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutes_CreatedAt_Id",
                table: "MeetingMinutes",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeetingMinutes_CreatedAt_Id",
                table: "MeetingMinutes");
        }
    }
}
