using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActionSeedingCheckpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActionsSeededAt",
                table: "MeetingMinutes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionsSeededAt",
                table: "MeetingMinutes");
        }
    }
}
