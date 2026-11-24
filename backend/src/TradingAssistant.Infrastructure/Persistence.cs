using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TradingAssistant.Application;

namespace TradingAssistant.Infrastructure;

public static class Persistence
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<TradingContext>(options => options.UseNpgsql(connectionString));
        return services.AddScoped<IDatabaseReadiness, DatabaseReadiness>();
    }
}

internal sealed class DatabaseReadiness(TradingContext context) : IDatabaseReadiness
{
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.Database.GetConnectionString()))
        {
            return false;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            // A round trip also detects dead connections returned by the pool.
            await context.Database.ExecuteSqlRawAsync("SELECT 1", deadline.Token);
            return true;
        }
        catch (NpgsqlException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
