using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace MeetingMind.Api.IntegrationTests;

public sealed class MeetingMindApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("meetingmind_api_tests")
        .WithUsername("meetingmind_api_tests")
        .WithPassword("meetingmind_api_tests")
        .Build();

    private string _connectionString = string.Empty;

    public TestBackgroundJobService BackgroundJobs { get; } = new();

    public TestFileStorageService Storage { get; } = new();

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Docker Desktop must be running to execute MeetingMind API integration tests.",
                exception);
        }

        _connectionString = _container.GetConnectionString();
        await using (var dbContext = CreateDbContext())
        {
            await dbContext.Database.MigrateAsync();
        }

        Client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    public new async Task DisposeAsync()
    {
        Client.Dispose();
        Dispose();
        await _container.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting(
            "Storage:RootPath",
            Path.Combine(Path.GetTempPath(), "MeetingMind.Api.IntegrationTests"));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBackgroundJobService>();
            services.RemoveAll<IFileStorageService>();
            services.AddSingleton(BackgroundJobs);
            services.AddSingleton<IBackgroundJobService>(BackgroundJobs);
            services.AddSingleton(Storage);
            services.AddSingleton<IFileStorageService>(Storage);
        });
    }

    public async Task ResetAsync()
    {
        BackgroundJobs.Reset();
        Storage.Reset();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeetingMindDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE "MeetingMinutes", "MeetingTranscripts", "MeetingJobs" RESTART IDENTITY CASCADE;
            """);
    }

    public async Task SeedAsync(params object[] entities)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeetingMindDbContext>();
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync();
    }

    private MeetingMindDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MeetingMindDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new MeetingMindDbContext(options);
    }
}
