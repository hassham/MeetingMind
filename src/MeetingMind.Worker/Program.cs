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
using MeetingMind.Application.Actions;
using MeetingMind.Infrastructure.Exports;

var builder = Host.CreateApplicationBuilder(args);
var localSettingsPath = MeetingMindConfiguration.GetRepositoryLocalSettingsPath(
    builder.Environment.ContentRootPath);
builder.Configuration.AddJsonFile(localSettingsPath, optional: true, reloadOnChange: true);

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

var databaseStartupOptions = MeetingMindConfiguration.ValidateDatabaseStartupOptions(
    builder.Configuration.GetSection("DatabaseStartup").Get<DatabaseStartupOptions>()
        ?? new DatabaseStartupOptions());
builder.Services.AddSingleton(databaseStartupOptions);

var transcriptFormattingOptions = MeetingMindConfiguration.ValidateTranscriptFormattingOptions(
    builder.Configuration.GetSection("TranscriptFormatting").Get<TranscriptFormattingOptions>()
        ?? new TranscriptFormattingOptions());
builder.Services.AddSingleton(transcriptFormattingOptions);

var actionBackfillOptions = builder.Configuration.GetSection("ActionBackfill").Get<ActionBackfillOptions>() ?? new ActionBackfillOptions();
if (actionBackfillOptions.BatchSize is < 1 or > 500) throw new InvalidOperationException("Configuration setting 'ActionBackfill:BatchSize' must be between 1 and 500.");
builder.Services.AddSingleton(actionBackfillOptions);

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
builder.Services.AddScoped<IActionRepository, EfActionRepository>();
builder.Services.AddScoped<IActionItemExporter, ActionItemExporter>();
builder.Services.AddScoped<IActionService, ActionService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IAudioProcessingService, FfmpegAudioProcessingService>();
builder.Services.AddScoped<ITranscriptionService, WhisperNetTranscriptionService>();
builder.Services.AddScoped<IMeetingMinutesGenerationClient, OpenAiMeetingMinutesGenerationClient>();
builder.Services.AddSingleton<TranscriptChunker>();
builder.Services.AddSingleton<TranscriptFormatter>();
builder.Services.AddSingleton<MeetingMinutesMerger>();
builder.Services.AddScoped<IMeetingMinutesService, MeetingMinutesService>();
builder.Services.AddSingleton<IMeetingFailureClassifier, MeetingFailureClassifier>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IMeetingProcessingJob, MeetingProcessingJob>();
builder.Services.AddScoped<IStorageRetentionService, StorageRetentionService>();
builder.Services.AddScoped<IStorageRetentionJob, StorageRetentionJob>();
builder.Services.AddScoped<IActionBackfillJob, ActionBackfillJob>();

var host = builder.Build();

if (builder.Environment.IsDevelopment())
{
    await DevelopmentDatabaseStartup.WaitForWorkerSchemaAsync(
        host.Services,
        databaseStartupOptions,
        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup"));
}

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

if (actionBackfillOptions.Enabled)
{
    BackgroundJob.Enqueue<IActionBackfillJob>(job => job.RunAsync());
}

host.Run();
