using MeetingMind.Domain.Entities;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MeetingJob>(entity =>
            {
                entity.HasKey(job => job.Id);
                entity.Property(job => job.OriginalFileName).HasMaxLength(255).IsRequired();
                entity.Property(job => job.OriginalFilePath).HasMaxLength(1024).IsRequired();
                entity.Property(job => job.ProcessedFilePath).HasMaxLength(1024);
                entity.Property(job => job.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
                entity.Property(job => job.Stage).HasConversion<string>().HasMaxLength(64).IsRequired();
                entity.Property(job => job.ErrorMessage).HasMaxLength(4000);
                entity.Property(job => job.HangfireJobId).HasMaxLength(128);
                entity.Property(job => job.Progress).IsRequired();
                entity.Property(job => job.CreatedAt).IsRequired();
                entity.Property(job => job.UpdatedAt).IsRequired();

                entity.HasOne(job => job.Transcript)
                    .WithOne(transcript => transcript.MeetingJob)
                    .HasForeignKey<MeetingTranscript>(transcript => transcript.MeetingJobId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(job => job.Minutes)
                    .WithOne(minutes => minutes.MeetingJob)
                    .HasForeignKey<MeetingMinutes>(minutes => minutes.MeetingJobId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<MeetingTranscript>(entity =>
            {
                entity.HasKey(transcript => transcript.Id);
                entity.Property(transcript => transcript.TranscriptText).IsRequired();
                entity.Property(transcript => transcript.TranscriptFilePath).HasMaxLength(1024);
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
                entity.HasIndex(minutes => minutes.MeetingJobId).IsUnique();
            });
        }
    }
}
