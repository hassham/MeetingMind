using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Application.Meetings;
using MeetingMind.Infrastructure.Persistence;
using MeetingMind.Infrastructure.Storage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var storageOptions = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
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
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IMeetingJobRepository, EfMeetingJobRepository>();
builder.Services.AddScoped<IUploadMeetingService, UploadMeetingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/health/db", async (MeetingMindDbContext dbContext) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync();

    return canConnect
        ? Results.Ok(new { status = "Healthy", database = "PostgreSQL" })
        : Results.Problem("Database connection failed");
})
.WithName("DatabaseHealthCheck")
.WithOpenApi();

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "Healthy",
        service = "MeetingMind.Api",
        timestamp = DateTime.UtcNow
    });
})
.WithName("HealthCheck")
.WithOpenApi();

app.MapPost("/api/meetings/upload", async (
    IFormFile file,
    IUploadMeetingService uploadMeetingService,
    CancellationToken cancellationToken) =>
{
    await using var stream = file.OpenReadStream();
    var request = new UploadMeetingRequest(
        stream,
        file.FileName,
        file.ContentType,
        file.Length);

    try
    {
        var result = await uploadMeetingService.UploadAsync(request, cancellationToken);

        return Results.Accepted($"/api/meetings/{result.JobId}/status", new
        {
            jobId = result.JobId,
            status = result.Status.ToString(),
            stage = result.Stage.ToString()
        });
    }
    catch (UploadValidationException exception)
    {
        return Results.BadRequest(new
        {
            error = exception.Message
        });
    }
})
.DisableAntiforgery()
.WithName("UploadMeeting")
.WithOpenApi();

app.Run();
