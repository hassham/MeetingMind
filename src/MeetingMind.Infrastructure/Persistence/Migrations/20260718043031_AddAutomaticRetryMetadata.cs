using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticRetryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutomaticRetryCount",
                table: "MeetingJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AutomaticRetryLimit",
                table: "MeetingJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAt",
                table: "MeetingJobs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutomaticRetryCount",
                table: "MeetingJobs");

            migrationBuilder.DropColumn(
                name: "AutomaticRetryLimit",
                table: "MeetingJobs");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "MeetingJobs");
        }
    }
}
