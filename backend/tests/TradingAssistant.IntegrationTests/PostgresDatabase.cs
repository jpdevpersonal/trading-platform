using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradingAssistant.Infrastructure;
using Xunit;

namespace TradingAssistant.IntegrationTests;

internal sealed class PostgresDatabase : IAsyncDisposable
{
    private readonly string schema = $"m03_{Guid.NewGuid():N}";
    public string ConnectionString { get; }

    private PostgresDatabase()
    {
        var settings = new NpgsqlConnectionStringBuilder(RequiredConnectionString())
        {
            SearchPath = schema,
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 5
        };
        ConnectionString = settings.ConnectionString;
    }

    public static string RequiredConnectionString()
    {
        var connection = Environment.GetEnvironmentVariable("TRADING_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection), "Set TRADING_TEST_CONNECTION to an isolated PostgreSQL database (see README).");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        Assert.True(settings.Host is "127.0.0.1" or "localhost" && settings.Database?.EndsWith("_test", StringComparison.Ordinal) == true,
            "Tests require a loopback PostgreSQL database ending in _test.");
        return connection;
    }

    public static async Task<PostgresDatabase> CreateAsync()
    {
        var database = new PostgresDatabase();
        await database.ExecuteAsync($"""CREATE SCHEMA "{database.schema}" """);
        return database;
    }

    public TradingContext CreateContext() =>
        new(new DbContextOptionsBuilder<TradingContext>().UseNpgsql(ConnectionString, options => options.CommandTimeout(5)).Options);

    public async Task MigrateAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Assert.IsType<T>(await command.ExecuteScalarAsync());
    }

    public Task ImportAsync() =>
        ExecuteAsync(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sample-instruments.sql")));

    public async ValueTask DisposeAsync() =>
        await ExecuteAsync($"""DROP SCHEMA "{schema}" CASCADE""");
}
