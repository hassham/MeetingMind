using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Application.Meetings;
using MeetingMind.Infrastructure.BackgroundJobs;
using MeetingMind.Infrastructure.Configuration;
using MeetingMind.Infrastructure.Persistence;
using MeetingMind.Infrastructure.Storage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = MeetingMindConfiguration.GetConnectionString(builder.Configuration);

var storageOptions = MeetingMindConfiguration.ValidateStorageOptions(
    builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions());
builder.Services.AddSingleton(storageOptions);

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
builder.Services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();
builder.Services.AddScoped<IMeetingJobRepository, EfMeetingJobRepository>();
builder.Services.AddScoped<IUploadMeetingService, UploadMeetingService>();
builder.Services.AddScoped<IMeetingStatusService, MeetingStatusService>();
builder.Services.AddScoped<IMeetingTranscriptService, MeetingTranscriptService>();
builder.Services.AddScoped<IMeetingMinutesResultService, MeetingMinutesResultService>();
builder.Services.AddScoped<IMeetingRetryService, MeetingRetryService>();
builder.Services.AddScoped<IMeetingHistoryService, MeetingHistoryService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = Array.Empty<IDashboardAuthorizationFilter>()
});

app.MapControllers();

app.Run();

public partial class Program;
