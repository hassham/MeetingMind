using MeetingMind.Application.Common.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MeetingMind.Infrastructure.Persistence;

public static class DevelopmentDatabaseStartup
{
    public static Task MigrateApiAsync(
        IServiceProvider services,
        DatabaseStartupOptions options,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithRetryAsync(
            services,
            options,
            logger,
            "apply database migrations",
            async (dbContext, attemptCancellationToken) =>
            {
                await dbContext.Database.MigrateAsync(attemptCancellationToken);
                logger.LogInformation("Development database migrations are up to date.");
            },
            cancellationToken);
    }

    public static Task WaitForWorkerSchemaAsync(
        IServiceProvider services,
        DatabaseStartupOptions options,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithRetryAsync(
            services,
            options,
            logger,
            "verify the migrated database schema",
            async (dbContext, attemptCancellationToken) =>
            {
                if (!await dbContext.Database.CanConnectAsync(attemptCancellationToken))
                {
                    throw new NpgsqlException("PostgreSQL is not accepting connections.");
                }

                var pendingMigrations = await dbContext.Database
                    .GetPendingMigrationsAsync(attemptCancellationToken);
                if (pendingMigrations.Any())
                {
                    throw new InvalidOperationException(
                        "The database has pending migrations. Start the Development API and wait for Swagger before starting the Worker.");
                }

                logger.LogInformation("Worker confirmed that the development database schema is ready.");
            },
            cancellationToken);
    }

    private static async Task ExecuteWithRetryAsync(
        IServiceProvider services,
        DatabaseStartupOptions options,
        ILogger logger,
        string operation,
        Func<MeetingMindDbContext, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<MeetingMindDbContext>();
                await action(dbContext, cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastException = exception;
                if (attempt == options.MaxAttempts)
                {
                    break;
                }

                TryLogRetry(logger, operation, attempt, options);

                await Task.Delay(TimeSpan.FromSeconds(options.DelaySeconds), cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Could not {operation} after {options.MaxAttempts} attempts. " +
            "Start Docker Desktop, run 'docker compose up -d', and confirm PostgreSQL is accepting connections on 127.0.0.1:5432. " +
            "If PostgreSQL is healthy, review the inner migration error. Local data is never reset automatically.",
            lastException);
    }

    private static void TryLogRetry(
        ILogger logger,
        string operation,
        int attempt,
        DatabaseStartupOptions options)
    {
        try
        {
            logger.LogWarning(
                "Could not {Operation}; attempt {Attempt} of {MaxAttempts}. Retrying in {DelaySeconds} seconds.",
                operation,
                attempt,
                options.MaxAttempts,
                options.DelaySeconds);
        }
        catch
        {
            // A logging sink must not replace the actionable database startup error.
        }
    }
}
