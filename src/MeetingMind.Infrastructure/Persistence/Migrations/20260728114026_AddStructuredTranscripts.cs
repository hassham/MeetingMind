using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredTranscripts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormattingConfigurationJson",
                table: "MeetingTranscripts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormattingVersion",
                table: "MeetingTranscripts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParagraphsJson",
                table: "MeetingTranscripts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SegmentsJson",
                table: "MeetingTranscripts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormattingConfigurationJson",
                table: "MeetingTranscripts");

            migrationBuilder.DropColumn(
                name: "FormattingVersion",
                table: "MeetingTranscripts");

            migrationBuilder.DropColumn(
                name: "ParagraphsJson",
                table: "MeetingTranscripts");

            migrationBuilder.DropColumn(
                name: "SegmentsJson",
                table: "MeetingTranscripts");
        }
    }
}
