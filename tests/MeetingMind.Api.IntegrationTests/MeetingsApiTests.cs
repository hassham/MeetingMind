using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MeetingMind.Application.Meetings;
using MeetingMind.Domain.Entities;
using MeetingMind.Domain.Enums;

namespace MeetingMind.Api.IntegrationTests;

public sealed class MeetingsApiTests : IClassFixture<MeetingMindApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MeetingMindApiFactory _factory;

    public MeetingsApiTests(MeetingMindApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthReturnsHealthyServiceContract()
    {
        await _factory.ResetAsync();

        var response = await _factory.Client.GetAsync("/health");
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("MeetingMind.Api", json.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public async Task ReadinessReturnsNamedHealthyDependencyChecks()
    {
        await _factory.ResetAsync();

        var response = await _factory.Client.GetAsync("/health/ready");
        var json = await ReadJsonAsync(response);
        var checks = json.RootElement.GetProperty("checks").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            ["database", "storage", "ffmpeg", "whisper_model"],
            checks.Select(check => check.GetProperty("name").GetString()));
        Assert.All(checks, check => Assert.Equal("Healthy", check.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task UploadAcceptsSupportedAudioAndEnqueuesPersistedJob()
    {
        await _factory.ResetAsync();
        using var form = CreateUpload("planning.mp3", "audio/mpeg", "audio bytes");

        var response = await _factory.Client.PostAsync("/api/meetings/upload", form);
        var json = await ReadJsonAsync(response);
        var jobId = json.RootElement.GetProperty("jobId").GetGuid();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("Queued", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Uploaded", json.RootElement.GetProperty("stage").GetString());
        Assert.Equal(jobId, Assert.Single(_factory.BackgroundJobs.EnqueuedJobIds));

        var status = await _factory.Client.GetFromJsonAsync<JsonElement>($"/api/meetings/{jobId}/status");
        Assert.Equal(jobId, status.GetProperty("jobId").GetGuid());
        Assert.Equal("FullMeeting", status.GetProperty("processingMode").GetString());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("sourceAudioDurationSeconds").ValueKind);
        Assert.Equal("Queued", status.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UploadRejectsUnsupportedExtensionWithoutEnqueueing()
    {
        await _factory.ResetAsync();
        using var form = CreateUpload("notes.txt", "text/plain", "not audio");

        var response = await _factory.Client.PostAsync("/api/meetings/upload", form);
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("upload_validation", json.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal("Unsupported file extension.", json.RootElement.GetProperty("error").GetString());
        Assert.Empty(_factory.BackgroundJobs.EnqueuedJobIds);
    }

    [Theory]
    [InlineData("TranscriptOnly")]
    [InlineData("FullMeeting")]
    public async Task AudioEndpointAcceptsSupportedExplicitModes(string mode)
    {
        await _factory.ResetAsync();
        using var form = CreateUpload("planning.mp3", "audio/mpeg", "audio bytes");
        form.Add(new StringContent(mode), "mode");

        var response = await _factory.Client.PostAsync("/api/meetings/audio", form);
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(mode, json.RootElement.GetProperty("processingMode").GetString());
        Assert.Equal("Queued", json.RootElement.GetProperty("status").GetString());
        Assert.Single(_factory.BackgroundJobs.EnqueuedJobIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("MinutesFromTranscript")]
    [InlineData("fullmeeting")]
    public async Task AudioEndpointRejectsMissingOrUnsupportedMode(string mode)
    {
        await _factory.ResetAsync();
        using var form = CreateUpload("planning.mp3", "audio/mpeg", "audio bytes");
        if (mode.Length > 0)
        {
            form.Add(new StringContent(mode), "mode");
        }

        var response = await _factory.Client.PostAsync("/api/meetings/audio", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.BackgroundJobs.EnqueuedJobIds);
    }

    [Theory]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("notes.md", "text/markdown")]
    public async Task TranscriptEndpointNormalizesAndPersistsValidUtf8(
        string fileName,
        string contentType)
    {
        await _factory.ResetAsync();
        using var form = CreateUpload(fileName, contentType, "\uFEFFFirst\r\n\rSecond\r");

        var response = await _factory.Client.PostAsync("/api/meetings/transcript", form);
        var json = await ReadJsonAsync(response);
        var jobId = json.RootElement.GetProperty("jobId").GetGuid();
        var transcript = await _factory.Client.GetStringAsync(
            $"/api/meetings/{jobId}/transcript/download");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("MinutesFromTranscript", json.RootElement.GetProperty("processingMode").GetString());
        Assert.Equal("First\n\nSecond\n", transcript);
        Assert.Equal(jobId, Assert.Single(_factory.BackgroundJobs.EnqueuedJobIds));
    }

    [Theory]
    [InlineData("notes.md", "text/plain", "Valid text")]
    [InlineData("notes.txt", "text/markdown", "Valid text")]
    [InlineData("notes.txt", "text/plain", " \r\n\t")]
    [InlineData("notes.txt", "text/plain", "Text\0binary")]
    [InlineData("notes.txt", "text/plain", "Text\u0001binary")]
    public async Task TranscriptEndpointRejectsInvalidInputBeforeEnqueue(
        string fileName,
        string contentType,
        string content)
    {
        await _factory.ResetAsync();
        using var form = CreateUpload(fileName, contentType, content);

        var response = await _factory.Client.PostAsync("/api/meetings/transcript", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.BackgroundJobs.EnqueuedJobIds);
    }

    [Fact]
    public async Task StatusReturnsPersistedStateAndMissingJobReturnsNotFound()
    {
        await _factory.ResetAsync();
        var job = CreateJob(MeetingJobStatus.Processing, MeetingJobStage.Transcribing, 25);
        var now = DateTimeOffset.UtcNow;
        job.CreatedAt = now.AddSeconds(-120);
        job.StartedAt = now.AddSeconds(-45);
        job.UpdatedAt = now;
        job.AutomaticRetryCount = 1;
        job.AutomaticRetryLimit = 2;
        job.NextRetryAt = now.AddSeconds(60);
        job.ErrorCode = "temporary_interruption";
        job.ErrorMessage = "A temporary interruption stopped processing.";
        job.ProcessingMode = MeetingProcessingMode.TranscriptOnly;
        job.SourceAudioDurationSeconds = 75;
        await _factory.SeedAsync(job);

        var response = await _factory.Client.GetAsync($"/api/meetings/{job.Id}/status");
        var json = await ReadJsonAsync(response);
        var missing = await _factory.Client.GetAsync($"/api/meetings/{Guid.NewGuid()}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Processing", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("TranscriptOnly", json.RootElement.GetProperty("processingMode").GetString());
        Assert.Equal(75, json.RootElement.GetProperty("sourceAudioDurationSeconds").GetInt64());
        Assert.Equal("Transcribing", json.RootElement.GetProperty("stage").GetString());
        Assert.Equal(25, json.RootElement.GetProperty("progress").GetInt32());
        Assert.Equal("temporary_interruption", json.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("automaticRetryCount").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("automaticRetryLimit").GetInt32());
        Assert.Equal(
            job.NextRetryAt?.ToUnixTimeMilliseconds(),
            json.RootElement.GetProperty("nextRetryAt").GetDateTimeOffset().ToUnixTimeMilliseconds());
        Assert.InRange(json.RootElement.GetProperty("processingDurationSeconds").GetInt64(), 45, 50);
        Assert.InRange(json.RootElement.GetProperty("totalDurationSeconds").GetInt64(), 120, 125);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task HistoryReturnsNewestFirstPageMetadata()
    {
        await _factory.ResetAsync();
        var older = CreateJob(createdAt: DateTimeOffset.UtcNow.AddMinutes(-2));
        var newer = CreateJob(createdAt: DateTimeOffset.UtcNow);
        newer.Status = MeetingJobStatus.Completed;
        newer.Stage = MeetingJobStage.Completed;
        newer.StartedAt = newer.CreatedAt.AddSeconds(10);
        newer.CompletedAt = newer.CreatedAt.AddSeconds(30);
        newer.UpdatedAt = newer.CompletedAt.Value;
        newer.AutomaticRetryCount = 1;
        newer.AutomaticRetryLimit = 2;
        await _factory.SeedAsync(older, newer);

        var response = await _factory.Client.GetAsync("/api/meetings/history?skip=0&take=1");
        var json = await ReadJsonAsync(response);
        var items = json.RootElement.GetProperty("items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, json.RootElement.GetProperty("skip").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("take").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(newer.Id, items[0].GetProperty("jobId").GetGuid());
        Assert.Equal("FullMeeting", items[0].GetProperty("processingMode").GetString());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("sourceAudioDurationSeconds").ValueKind);
        Assert.Equal(1, items[0].GetProperty("automaticRetryCount").GetInt32());
        Assert.Equal(2, items[0].GetProperty("automaticRetryLimit").GetInt32());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("nextRetryAt").ValueKind);
        Assert.Equal(20, items[0].GetProperty("processingDurationSeconds").GetInt64());
        Assert.Equal(30, items[0].GetProperty("totalDurationSeconds").GetInt64());
    }

    [Fact]
    public async Task DashboardSummaryReturnsAllTimeContract()
    {
        await _factory.ResetAsync();
        var completed = CreateJob(
            MeetingJobStatus.Completed,
            MeetingJobStage.Completed,
            100,
            DateTimeOffset.UtcNow);
        completed.StartedAt = completed.CreatedAt.AddSeconds(10);
        completed.CompletedAt = completed.CreatedAt.AddSeconds(50);
        completed.SourceAudioDurationSeconds = 75;
        var transcript = new MeetingTranscript
        {
            Id = Guid.NewGuid(),
            MeetingJobId = completed.Id,
            TranscriptText = "Transcript",
            TranscriptFilePath = "Transcript/dashboard.txt"
        };
        var minutes = new MeetingMinutes
        {
            Id = Guid.NewGuid(),
            MeetingJobId = completed.Id,
            Title = "Dashboard meeting",
            Summary = "Summary"
        };
        await _factory.SeedAsync(completed, transcript, minutes);

        var response = await _factory.Client.GetAsync("/api/dashboard/summary");
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("All time", json.RootElement.GetProperty("timeBasis").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("totalJobs").GetInt32());
        Assert.Equal(100, json.RootElement.GetProperty("successRatePercent").GetDouble());
        Assert.Equal(75, json.RootElement.GetProperty("totalAudioDurationSeconds").GetInt64());
        Assert.Equal(40, json.RootElement.GetProperty("averageCompletedProcessingDurationSeconds").GetDouble());
        Assert.Equal(1, json.RootElement.GetProperty("transcriptCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("minutesCount").GetInt32());
        Assert.Equal(completed.Id, json.RootElement.GetProperty("recentJobs")[0].GetProperty("jobId").GetGuid());
        Assert.Equal("Dashboard meeting", json.RootElement.GetProperty("recentMinutes")[0].GetProperty("title").GetString());
        Assert.False(json.RootElement.TryGetProperty("actions", out _));
    }

    [Fact]
    public async Task ResultAndDownloadsReturnPersistedArtifacts()
    {
        await _factory.ResetAsync();
        var job = CreateJob(MeetingJobStatus.Completed, MeetingJobStage.Completed, 100);
        var transcriptPath = $"Transcript/{job.Id:N}.txt";
        var minutesPath = $"Minutes/{job.Id:N}.md";
        var content = CreateMinutesContent();
        var transcript = new MeetingTranscript
        {
            Id = Guid.NewGuid(),
            MeetingJobId = job.Id,
            TranscriptText = "Meeting transcript",
            TranscriptFilePath = transcriptPath
        };
        var minutes = new MeetingMinutes
        {
            Id = Guid.NewGuid(),
            MeetingJobId = job.Id,
            Title = content.Title,
            Summary = content.Summary,
            DecisionsJson = JsonSerializer.Serialize(content.Decisions, JsonOptions),
            ActionItemsJson = JsonSerializer.Serialize(content.ActionItems, JsonOptions),
            RisksJson = JsonSerializer.Serialize(content.Risks, JsonOptions),
            NextStepsJson = JsonSerializer.Serialize(content.NextSteps, JsonOptions),
            FullMinutesJson = JsonSerializer.Serialize(content, JsonOptions),
            MinutesFilePath = minutesPath
        };
        await _factory.SeedAsync(job, transcript, minutes);
        _factory.Storage.AddText(transcriptPath, "Meeting transcript");
        _factory.Storage.AddText(minutesPath, "# Planning\n\nSummary");

        var resultResponse = await _factory.Client.GetAsync($"/api/meetings/{job.Id}/result");
        var result = await ReadJsonAsync(resultResponse);
        var transcriptViewerResponse = await _factory.Client.GetAsync($"/api/meetings/{job.Id}/transcript");
        var transcriptViewer = await ReadJsonAsync(transcriptViewerResponse);
        var transcriptResponse = await _factory.Client.GetAsync($"/api/meetings/{job.Id}/transcript/download");
        var minutesResponse = await _factory.Client.GetAsync($"/api/meetings/{job.Id}/minutes/download");

        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);
        Assert.Equal("Planning", result.RootElement.GetProperty("title").GetString());
        Assert.Equal("Approve scope", result.RootElement.GetProperty("decisions")[0].GetString());
        Assert.Equal(HttpStatusCode.OK, transcriptViewerResponse.StatusCode);
        Assert.False(transcriptViewer.RootElement.GetProperty("hasTimestamps").GetBoolean());
        Assert.Equal("Meeting transcript", transcriptViewer.RootElement.GetProperty("paragraphs")[0].GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, transcriptViewer.RootElement.GetProperty("paragraphs")[0].GetProperty("startSeconds").ValueKind);
        Assert.Equal("Meeting transcript", await transcriptResponse.Content.ReadAsStringAsync());
        Assert.Equal("# Planning\n\nSummary", await minutesResponse.Content.ReadAsStringAsync());
        Assert.Equal("text/plain", transcriptResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("text/markdown", minutesResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task StructuredTranscriptViewerReturnsTimedParagraphs()
    {
        await _factory.ResetAsync();
        var job = CreateJob(MeetingJobStatus.Completed, MeetingJobStage.Completed, 100);
        var paragraphs = new[]
        {
            new TranscriptParagraph("Opening discussion.", TimeSpan.FromSeconds(65), TimeSpan.FromSeconds(72))
        };
        var formatting = new TranscriptFormattingSnapshot(1.5, 300, 700, "v1");
        var transcript = new MeetingTranscript
        {
            Id = Guid.NewGuid(),
            MeetingJobId = job.Id,
            TranscriptText = "Opening discussion.",
            TranscriptFilePath = $"Transcript/{job.Id:N}.txt",
            SegmentsJson = JsonSerializer.Serialize(
                new[] { new TranscriptionSegment(TimeSpan.FromSeconds(65), TimeSpan.FromSeconds(72), "Opening discussion.") },
                JsonOptions),
            ParagraphsJson = JsonSerializer.Serialize(paragraphs, JsonOptions),
            FormattingVersion = "v1",
            FormattingConfigurationJson = JsonSerializer.Serialize(formatting, JsonOptions)
        };
        await _factory.SeedAsync(job, transcript);

        var response = await _factory.Client.GetAsync($"/api/meetings/{job.Id}/transcript");
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.GetProperty("hasTimestamps").GetBoolean());
        Assert.Equal("v1", json.RootElement.GetProperty("formattingVersion").GetString());
        Assert.Equal(65, json.RootElement.GetProperty("paragraphs")[0].GetProperty("startSeconds").GetDouble());
    }

    [Fact]
    public async Task MissingResultAndDownloadsReturnNotFound()
    {
        await _factory.ResetAsync();
        var jobId = Guid.NewGuid();

        var result = await _factory.Client.GetAsync($"/api/meetings/{jobId}/result");
        var transcriptViewer = await _factory.Client.GetAsync($"/api/meetings/{jobId}/transcript");
        var transcript = await _factory.Client.GetAsync($"/api/meetings/{jobId}/transcript/download");
        var minutes = await _factory.Client.GetAsync($"/api/meetings/{jobId}/minutes/download");

        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, transcriptViewer.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, transcript.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, minutes.StatusCode);
    }

    [Fact]
    public async Task RetryRequeuesFailedJobAndRejectsCompletedJob()
    {
        await _factory.ResetAsync();
        var failed = CreateJob(MeetingJobStatus.Failed, MeetingJobStage.Transcribing, 25);
        failed.ErrorMessage = "temporary failure";
        var completed = CreateJob(MeetingJobStatus.Completed, MeetingJobStage.Completed, 100);
        await _factory.SeedAsync(failed, completed);

        var accepted = await _factory.Client.PostAsync($"/api/meetings/{failed.Id}/retry", null);
        var acceptedJson = await ReadJsonAsync(accepted);
        var conflict = await _factory.Client.PostAsync($"/api/meetings/{completed.Id}/retry", null);

        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal("Queued", acceptedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(failed.Id, Assert.Single(_factory.BackgroundJobs.EnqueuedJobIds));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    private static MultipartFormDataContent CreateUpload(string fileName, string contentType, string content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    private static MeetingJob CreateJob(
        MeetingJobStatus status = MeetingJobStatus.Queued,
        MeetingJobStage stage = MeetingJobStage.Uploaded,
        int progress = 0,
        DateTimeOffset? createdAt = null)
    {
        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        return new MeetingJob
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "meeting.mp3",
            OriginalFilePath = $"Audio/Original/{Guid.NewGuid():N}.mp3",
            Status = status,
            Stage = stage,
            Progress = progress,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    private static MeetingMinutesContent CreateMinutesContent()
    {
        return new MeetingMinutesContent(
            "Planning",
            "The team planned delivery.",
            ["Hasham"],
            ["Scope"],
            ["Approve scope"],
            [new MeetingActionItem("Write tests", "Hasham", "Friday")],
            ["Schedule"],
            ["Implement"]);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
