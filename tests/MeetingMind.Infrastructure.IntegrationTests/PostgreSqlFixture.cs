using MeetingMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace MeetingMind.Infrastructure.IntegrationTests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("meetingmind_tests")
        .WithUsername("meetingmind_tests")
        .WithPassword("meetingmind_tests")
        .Build();

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Docker Desktop must be running to execute MeetingMind PostgreSQL integration tests.",
                exception);
        }

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public MeetingMindDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MeetingMindDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new MeetingMindDbContext(options);
    }

    public async Task ResetAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE "MeetingMinutes", "MeetingTranscripts", "MeetingJobs" RESTART IDENTITY CASCADE;
            """);
    }
}
