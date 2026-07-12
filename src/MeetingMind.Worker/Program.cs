using Hangfire;
using Hangfire.PostgreSql;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Infrastructure.Audio;
using MeetingMind.Infrastructure.OpenAI;
using MeetingMind.Infrastructure.Persistence;
using MeetingMind.Infrastructure.Storage;
using MeetingMind.Infrastructure.Transcription;
using MeetingMind.Worker.Jobs;
using MeetingMind.Worker.Options;
using Microsoft.EntityFrameworkCore;
using MeetingMind.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

var processingOptions = builder.Configuration.GetSection("Processing").Get<ProcessingOptions>() ?? new ProcessingOptions();
builder.Services.AddSingleton(processingOptions);

var storageOptions = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
builder.Services.AddSingleton(storageOptions);

var audioProcessingOptions = builder.Configuration.GetSection("AudioProcessing").Get<AudioProcessingOptions>()
    ?? new AudioProcessingOptions();
builder.Services.AddSingleton(audioProcessingOptions);

var transcriptionOptions = builder.Configuration.GetSection("Transcription").Get<TranscriptionOptions>()
    ?? new TranscriptionOptions();
builder.Services.AddSingleton(transcriptionOptions);

var openAiOptions = builder.Configuration.GetSection("OpenAI").Get<OpenAiOptions>() ?? new OpenAiOptions();
openAiOptions.ApiKey = string.IsNullOrWhiteSpace(openAiOptions.ApiKey)
    ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    : openAiOptions.ApiKey;
builder.Services.AddSingleton(openAiOptions);

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
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IAudioProcessingService, FfmpegAudioProcessingService>();
builder.Services.AddScoped<ITranscriptionService, WhisperNetTranscriptionService>();
builder.Services.AddScoped<IMeetingMinutesService, OpenAiMeetingMinutesService>();
builder.Services.AddScoped<IMeetingProcessingJob, MeetingProcessingJob>();

var host = builder.Build();
host.Run();
