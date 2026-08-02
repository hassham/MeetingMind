using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Application.Meetings;
using MeetingMind.Application.Dashboard;
using MeetingMind.Infrastructure.BackgroundJobs;
using MeetingMind.Infrastructure.Configuration;
using MeetingMind.Infrastructure.Persistence;
using MeetingMind.Infrastructure.Operations;
using MeetingMind.Infrastructure.Exports;
using MeetingMind.Application.Actions;
using MeetingMind.Api;
using MeetingMind.Infrastructure.Storage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var localSettingsPath = MeetingMindConfiguration.GetRepositoryLocalSettingsPath(
    builder.Environment.ContentRootPath);
builder.Configuration.AddJsonFile(localSettingsPath, optional: true, reloadOnChange: true);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = MeetingMindConfiguration.GetConnectionString(builder.Configuration);

var storageOptions = MeetingMindConfiguration.ValidateStorageOptions(
    builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions());
builder.Services.AddSingleton(storageOptions);

var audioProcessingOptions = builder.Configuration
    .GetSection("AudioProcessing")
    .Get<AudioProcessingOptions>() ?? new AudioProcessingOptions();
builder.Services.AddSingleton(audioProcessingOptions);

var transcriptionOptions = builder.Configuration
    .GetSection("Transcription")
    .Get<TranscriptionOptions>() ?? new TranscriptionOptions();
builder.Services.AddSingleton(transcriptionOptions);

var databaseStartupOptions = MeetingMindConfiguration.ValidateDatabaseStartupOptions(
    builder.Configuration.GetSection("DatabaseStartup").Get<DatabaseStartupOptions>()
        ?? new DatabaseStartupOptions());
builder.Services.AddSingleton(databaseStartupOptions);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = storageOptions.MaxUploadSizeBytes;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = storageOptions.MaxUploadSizeBytes;
});

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

builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IOperationalReadinessService, OperationalReadinessService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();
builder.Services.AddScoped<IMeetingJobRepository, EfMeetingJobRepository>();
builder.Services.AddScoped<IMeetingMinutesQueryRepository, EfMeetingMinutesQueryRepository>();
builder.Services.AddScoped<IDashboardRepository, EfDashboardRepository>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IUploadMeetingService, UploadMeetingService>();
builder.Services.AddScoped<IMeetingStatusService, MeetingStatusService>();
builder.Services.AddScoped<IMeetingTranscriptService, MeetingTranscriptService>();
builder.Services.AddScoped<IMeetingMinutesResultService, MeetingMinutesResultService>();
builder.Services.AddScoped<IMeetingRetryService, MeetingRetryService>();
builder.Services.AddScoped<IMeetingHistoryService, MeetingHistoryService>();
builder.Services.AddScoped<IMeetingMinutesQueryService, MeetingMinutesQueryService>();
builder.Services.AddScoped<IActionRepository, EfActionRepository>();
builder.Services.AddScoped<IActionItemExporter, ActionItemExporter>();
builder.Services.AddScoped<IActionService, ActionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await DevelopmentDatabaseStartup.MigrateApiAsync(
        app.Services,
        databaseStartupOptions,
        app.Logger);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<ActionExceptionHandlerMiddleware>();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = Array.Empty<IDashboardAuthorizationFilter>()
});

app.MapControllers();

app.Run();

public partial class Program;
