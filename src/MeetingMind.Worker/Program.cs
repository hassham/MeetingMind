using Hangfire;
using Hangfire.PostgreSql;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Infrastructure.Audio;
using MeetingMind.Infrastructure.Configuration;
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
