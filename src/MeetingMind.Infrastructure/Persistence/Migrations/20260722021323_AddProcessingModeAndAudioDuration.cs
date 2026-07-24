using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingModeAndAudioDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessingMode",
                table: "MeetingJobs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "FullMeeting");

            migrationBuilder.AddColumn<long>(
                name: "SourceAudioDurationSeconds",
                table: "MeetingJobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingJobs_CreatedAt_Id",
                table: "MeetingJobs",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingJobs_ProcessingMode_CreatedAt",
                table: "MeetingJobs",
                columns: new[] { "ProcessingMode", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingJobs_Status_CreatedAt",
                table: "MeetingJobs",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MeetingJobs_ProcessingMode",
                table: "MeetingJobs",
                sql: "\"ProcessingMode\" IN ('TranscriptOnly', 'FullMeeting', 'MinutesFromTranscript')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MeetingJobs_SourceAudioDurationSeconds",
                table: "MeetingJobs",
                sql: "\"SourceAudioDurationSeconds\" IS NULL OR \"SourceAudioDurationSeconds\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MeetingJobs_TranscriptInputAudioMetadata",
                table: "MeetingJobs",
                sql: "\"ProcessingMode\" <> 'MinutesFromTranscript' OR (\"ProcessedFilePath\" IS NULL AND \"SourceAudioDurationSeconds\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeetingJobs_CreatedAt_Id",
                table: "MeetingJobs");

            migrationBuilder.DropIndex(
                name: "IX_MeetingJobs_ProcessingMode_CreatedAt",
                table: "MeetingJobs");

            migrationBuilder.DropIndex(
                name: "IX_MeetingJobs_Status_CreatedAt",
                table: "MeetingJobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MeetingJobs_ProcessingMode",
                table: "MeetingJobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MeetingJobs_SourceAudioDurationSeconds",
                table: "MeetingJobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MeetingJobs_TranscriptInputAudioMetadata",
                table: "MeetingJobs");

            migrationBuilder.DropColumn(
                name: "ProcessingMode",
                table: "MeetingJobs");

            migrationBuilder.DropColumn(
                name: "SourceAudioDurationSeconds",
                table: "MeetingJobs");
        }
    }
}
