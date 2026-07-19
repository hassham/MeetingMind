using Hangfire;
using Hangfire.PostgreSql;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Application.Meetings;
using MeetingMind.Application.Operations;
using MeetingMind.Infrastructure.Audio;
using MeetingMind.Infrastructure.Configuration;
using MeetingMind.Infrastructure.Failures;
using MeetingMind.Infrastructure.OpenAI;
using MeetingMind.Infrastructure.Persistence;
using MeetingMind.Infrastructure.Storage;
using MeetingMind.Infrastructure.Transcription;
using MeetingMind.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using MeetingMind.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = MeetingMindConfiguration.GetConnectionString(builder.Configuration);

var storageOptions = MeetingMindConfiguration.ValidateStorageOptions(
    builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions());
builder.Services.AddSingleton(storageOptions);

var audioProcessingOptions = MeetingMindConfiguration.ValidateAudioProcessingOptions(
    builder.Configuration.GetSection("AudioProcessing").Get<AudioProcessingOptions>()
        ?? new AudioProcessingOptions());
builder.Services.AddSingleton(audioProcessingOptions);

var transcriptionOptions = MeetingMindConfiguration.ValidateTranscriptionOptions(
    builder.Configuration.GetSection("Transcription").Get<TranscriptionOptions>()
        ?? new TranscriptionOptions(),
    storageOptions);
builder.Services.AddSingleton(transcriptionOptions);

var openAiOptions = MeetingMindConfiguration.ValidateOpenAiOptions(
    builder.Configuration.GetSection("OpenAI").Get<OpenAiOptions>() ?? new OpenAiOptions());
builder.Services.AddSingleton(openAiOptions);

var meetingMinutesGenerationOptions = MeetingMindConfiguration.ValidateMeetingMinutesGenerationOptions(
    builder.Configuration.GetSection("MeetingMinutesGeneration").Get<MeetingMinutesGenerationOptions>()
        ?? new MeetingMinutesGenerationOptions());
builder.Services.AddSingleton(meetingMinutesGenerationOptions);

var automaticRetryOptions = MeetingMindConfiguration.ValidateAutomaticRetryOptions(
    builder.Configuration.GetSection("AutomaticRetry").Get<AutomaticRetryOptions>()
        ?? new AutomaticRetryOptions());
builder.Services.AddSingleton(automaticRetryOptions);

var storageRetentionOptions = MeetingMindConfiguration.ValidateStorageRetentionOptions(
    builder.Configuration.GetSection("StorageRetention").Get<StorageRetentionOptions>()
        ?? new StorageRetentionOptions());
builder.Services.AddSingleton(storageRetentionOptions);

GlobalJobFilters.Filters.Remove<AutomaticRetryAttribute>();
GlobalJobFilters.Filters.Add(MeetingAutomaticRetryConfiguration.CreateFilter(automaticRetryOptions));

builder.Services.AddDbContext<MeetingMindDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddHangfire(configuration =>
{
    configuration.UsePostgreSqlStorage(options =>
    {
        options.UseNpgsqlConnection(connectionString);
    });
});

builder.Services.AddHangfireServer();
builder.Services.AddScoped<IMeetingJobRepository, EfMeetingJobRepository>();
builder.Services.AddScoped<IStorageRetentionRepository, EfStorageRetentionRepository>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IAudioProcessingService, FfmpegAudioProcessingService>();
builder.Services.AddScoped<ITranscriptionService, WhisperNetTranscriptionService>();
builder.Services.AddScoped<IMeetingMinutesGenerationClient, OpenAiMeetingMinutesGenerationClient>();
builder.Services.AddSingleton<TranscriptChunker>();
builder.Services.AddSingleton<MeetingMinutesMerger>();
builder.Services.AddScoped<IMeetingMinutesService, MeetingMinutesService>();
builder.Services.AddSingleton<IMeetingFailureClassifier, MeetingFailureClassifier>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IMeetingProcessingJob, MeetingProcessingJob>();
builder.Services.AddScoped<IStorageRetentionService, StorageRetentionService>();
builder.Services.AddScoped<IStorageRetentionJob, StorageRetentionJob>();

var host = builder.Build();

const string retentionJobId = "meetingmind-storage-retention";
using (var scope = host.Services.CreateScope())
{
    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    if (storageRetentionOptions.Enabled)
    {
        recurringJobs.AddOrUpdate<IStorageRetentionJob>(
            retentionJobId,
            job => job.RunAsync(),
            storageRetentionOptions.Schedule);
    }
    else
    {
        recurringJobs.RemoveIfExists(retentionJobId);
    }
}

host.Run();
