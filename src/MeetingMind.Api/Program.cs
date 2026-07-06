using MeetingMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<MeetingMindDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

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

app.Run();
