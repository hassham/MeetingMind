using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeetingMind.Infrastructure.Persistence
{
    public class MeetingMindDbContext : DbContext
    {
        public MeetingMindDbContext(DbContextOptions<MeetingMindDbContext> options)
            : base(options)
        {
        }

        public DbSet<MeetingJob> MeetingJobs => Set<MeetingJob>();

        public DbSet<MeetingTranscript> MeetingTranscripts => Set<MeetingTranscript>();

        public DbSet<MeetingMinutes> MeetingMinutes => Set<MeetingMinutes>();

        public DbSet<ActionItem> ActionItems => Set<ActionItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MeetingJob>(entity =>
            {
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_MeetingJobs_ProcessingMode",
                        "\"ProcessingMode\" IN ('TranscriptOnly', 'FullMeeting', 'MinutesFromTranscript')");
                    table.HasCheckConstraint(
                        "CK_MeetingJobs_SourceAudioDurationSeconds",
                        "\"SourceAudioDurationSeconds\" IS NULL OR \"SourceAudioDurationSeconds\" >= 0");
                    table.HasCheckConstraint(
                        "CK_MeetingJobs_TranscriptInputAudioMetadata",
                        "\"ProcessingMode\" <> 'MinutesFromTranscript' OR (\"ProcessedFilePath\" IS NULL AND \"SourceAudioDurationSeconds\" IS NULL)");
                });
                entity.HasKey(job => job.Id);
                entity.Property(job => job.OriginalFileName).HasMaxLength(255).IsRequired();
                entity.Property(job => job.OriginalFilePath).HasMaxLength(1024).IsRequired();
                entity.Property(job => job.ProcessedFilePath).HasMaxLength(1024);
                entity.Property(job => job.ProcessingMode)
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    .HasDefaultValue(MeetingProcessingMode.FullMeeting)
                    .HasSentinel((MeetingProcessingMode)(-1))
                    .IsRequired();
                entity.Property(job => job.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
                entity.Property(job => job.Stage).HasConversion<string>().HasMaxLength(64).IsRequired();
                entity.Property(job => job.ErrorMessage).HasMaxLength(4000);
                entity.Property(job => job.ErrorCode).HasMaxLength(64);
                entity.Property(job => job.HangfireJobId).HasMaxLength(128);
                entity.Property(job => job.Progress).IsRequired();
                entity.Property(job => job.AutomaticRetryCount).IsRequired();
                entity.Property(job => job.AutomaticRetryLimit).IsRequired();
                entity.Property(job => job.CreatedAt).IsRequired();
                entity.Property(job => job.UpdatedAt).IsRequired();
                entity.HasIndex(job => new { job.CreatedAt, job.Id })
                    .IsDescending()
                    .HasDatabaseName("IX_MeetingJobs_CreatedAt_Id");
                entity.HasIndex(job => new { job.Status, job.CreatedAt })
                    .IsDescending(false, true)
                    .HasDatabaseName("IX_MeetingJobs_Status_CreatedAt");
                entity.HasIndex(job => new { job.ProcessingMode, job.CreatedAt })
                    .IsDescending(false, true)
                    .HasDatabaseName("IX_MeetingJobs_ProcessingMode_CreatedAt");

                entity.HasOne(job => job.Transcript)
                    .WithOne(transcript => transcript.MeetingJob)
                    .HasForeignKey<MeetingTranscript>(transcript => transcript.MeetingJobId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(job => job.Minutes)
                    .WithOne(minutes => minutes.MeetingJob)
                    .HasForeignKey<MeetingMinutes>(minutes => minutes.MeetingJobId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(job => job.Actions)
                    .WithOne(action => action.MeetingJob)
                    .HasForeignKey(action => action.MeetingJobId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<MeetingTranscript>(entity =>
            {
                entity.HasKey(transcript => transcript.Id);
                entity.Property(transcript => transcript.TranscriptText).IsRequired();
                entity.Property(transcript => transcript.TranscriptFilePath).HasMaxLength(1024);
                entity.Property(transcript => transcript.SegmentsJson);
                entity.Property(transcript => transcript.ParagraphsJson);
                entity.Property(transcript => transcript.FormattingVersion).HasMaxLength(32);
                entity.Property(transcript => transcript.FormattingConfigurationJson);
                entity.Property(transcript => transcript.CreatedAt).IsRequired();
                entity.HasIndex(transcript => transcript.MeetingJobId).IsUnique();
            });

            modelBuilder.Entity<MeetingMinutes>(entity =>
            {
                entity.HasKey(minutes => minutes.Id);
                entity.Property(minutes => minutes.Title).HasMaxLength(255).IsRequired();
                entity.Property(minutes => minutes.Summary).IsRequired();
                entity.Property(minutes => minutes.DecisionsJson).IsRequired();
                entity.Property(minutes => minutes.ActionItemsJson).IsRequired();
                entity.Property(minutes => minutes.RisksJson).IsRequired();
                entity.Property(minutes => minutes.NextStepsJson).IsRequired();
                entity.Property(minutes => minutes.FullMinutesJson).IsRequired();
                entity.Property(minutes => minutes.MinutesFilePath).HasMaxLength(1024);
                entity.Property(minutes => minutes.CreatedAt).IsRequired();
                entity.Property(minutes => minutes.ActionsSeededAt);
                entity.HasIndex(minutes => minutes.MeetingJobId).IsUnique();
                entity.HasIndex(minutes => new { minutes.CreatedAt, minutes.Id })
                    .IsDescending()
                    .HasDatabaseName("IX_MeetingMinutes_CreatedAt_Id");
            });

            modelBuilder.Entity<ActionItem>(entity =>
            {
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_ActionItems_Description", "length(btrim(\"Description\")) BETWEEN 1 AND 2000");
                    table.HasCheckConstraint("CK_ActionItems_Version", "\"Version\" > 0");
                    table.HasCheckConstraint("CK_ActionItems_Status", "\"Status\" IN ('Open', 'InProgress', 'Blocked', 'Completed', 'Cancelled')");
                    table.HasCheckConstraint("CK_ActionItems_Source", "\"Source\" IN ('Generated', 'Manual')");
                    table.HasCheckConstraint("CK_ActionItems_GeneratedSourceKey", "(\"Source\" = 'Generated' AND \"GeneratedSourceKey\" IS NOT NULL) OR (\"Source\" = 'Manual' AND \"GeneratedSourceKey\" IS NULL)");
                    table.HasCheckConstraint("CK_ActionItems_CompletedAt", "(\"Status\" = 'Completed' AND \"CompletedAt\" IS NOT NULL) OR (\"Status\" <> 'Completed' AND \"CompletedAt\" IS NULL)");
                });
                entity.HasKey(action => action.Id);
                entity.Property(action => action.Description).HasMaxLength(2000).IsRequired();
                entity.Property(action => action.Assignee).HasMaxLength(200);
                entity.Property(action => action.Notes).HasMaxLength(10000);
                entity.Property(action => action.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
                entity.Property(action => action.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
                entity.Property(action => action.ProvenanceMeetingTitle).HasMaxLength(255);
                entity.Property(action => action.ProvenanceSourceFileName).HasMaxLength(255);
                entity.Property(action => action.GeneratedSourceKey).HasMaxLength(160);
                entity.Property(action => action.CreatedAt).IsRequired();
                entity.Property(action => action.UpdatedAt).IsRequired();
                entity.Property(action => action.Version).IsConcurrencyToken().IsRequired();
                entity.HasIndex(action => action.GeneratedSourceKey).IsUnique();
                entity.HasIndex(action => new { action.CreatedAt, action.Id }).IsDescending();
                entity.HasIndex(action => new { action.Status, action.CreatedAt });
                entity.HasIndex(action => new { action.DueDate, action.Status });
                entity.HasIndex(action => new { action.Source, action.CreatedAt });
                entity.HasIndex(action => new { action.MeetingJobId, action.CreatedAt });
            });
        }
    }
}
