using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Unit.Tests;

public class MeetingJobTests
{
    [Fact]
    public void NewMeetingJobDefaultsToQueuedUploadState()
    {
        var job = new MeetingJob();

        Assert.Equal(MeetingJobStatus.Queued, job.Status);
        Assert.Equal(MeetingJobStage.Uploaded, job.Stage);
        Assert.Equal(MeetingProcessingMode.FullMeeting, job.ProcessingMode);
        Assert.Null(job.SourceAudioDurationSeconds);
        Assert.Equal(0, job.Progress);
        Assert.NotEqual(default, job.CreatedAt);
        Assert.NotEqual(default, job.UpdatedAt);
    }

    [Fact]
    public void ValidateModeInputAcceptsAudioAndTranscriptSources()
    {
        var audioJob = new MeetingJob
        {
            OriginalFileName = "meeting.mp3",
            OriginalFilePath = "Audio/Original/meeting.mp3",
            ProcessingMode = MeetingProcessingMode.TranscriptOnly,
            ProcessedFilePath = "Audio/Processed/meeting.wav",
            SourceAudioDurationSeconds = 90
        };
        var transcriptJob = new MeetingJob
        {
            OriginalFileName = "meeting.txt",
            OriginalFilePath = "Transcript/meeting.txt",
            ProcessingMode = MeetingProcessingMode.MinutesFromTranscript
        };

        audioJob.ValidateModeInput();
        transcriptJob.ValidateModeInput();
    }

    [Fact]
    public void ValidateModeInputRejectsAudioMetadataForTranscriptInput()
    {
        var job = new MeetingJob
        {
            OriginalFileName = "meeting.txt",
            OriginalFilePath = "Transcript/meeting.txt",
            ProcessingMode = MeetingProcessingMode.MinutesFromTranscript,
            SourceAudioDurationSeconds = 1
        };

        var exception = Assert.Throws<InvalidOperationException>(job.ValidateModeInput);

        Assert.Contains("processed-audio metadata", exception.Message);
    }

    [Fact]
    public void ProcessingModeCapabilitiesAreExplicit()
    {
        Assert.True(MeetingProcessingMode.TranscriptOnly.RequiresAudio());
        Assert.False(MeetingProcessingMode.TranscriptOnly.GeneratesMinutes());
        Assert.True(MeetingProcessingMode.FullMeeting.RequiresAudio());
        Assert.True(MeetingProcessingMode.FullMeeting.GeneratesMinutes());
        Assert.False(MeetingProcessingMode.MinutesFromTranscript.RequiresAudio());
        Assert.True(MeetingProcessingMode.MinutesFromTranscript.GeneratesMinutes());
        Assert.Equal(
            MeetingJobStage.GeneratingMinutes,
            MeetingProcessingMode.MinutesFromTranscript.InitialStage());
    }
}
