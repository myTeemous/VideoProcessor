using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;
using VideoProcessor.Worker;

namespace VideoProcessor.IntegrationTests.Harness;

/// <summary>
/// Starts PostgreSQL and Azurite in containers, the Api through <see cref="ApiFactory"/>
/// and the Worker host in-process, all sharing one database and one storage account.
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private readonly AzuriteContainer _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest")
        .WithCommand("--skipApiVersionCheck")
        .Build();

    private ApiFactory? _api;
    private IHost? _worker;

    /// <summary>The clock the Worker sees. Advance it to drive time-based Worker behaviour.</summary>
    public FakeTimeProvider Time { get; } = new();

    public HttpClient CreateClient() => Api.CreateClient();

    private ApiFactory Api => _api ?? throw new InvalidOperationException("Fixture not initialised.");

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _azurite.StartAsync());

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            ["ConnectionStrings:BlobStorage"] = _azurite.GetConnectionString(),
        };

        // Creating the server starts the Api, which applies migrations in Development.
        _api = new ApiFactory(settings);
        _ = _api.Server;

        var workerBuilder = WorkerHost.CreateBuilder([]);
        workerBuilder.Configuration.AddInMemoryCollection(settings);
        workerBuilder.Services.AddSingleton<TimeProvider>(Time);
        _worker = workerBuilder.Build();
        await _worker.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_worker is not null)
        {
            await _worker.StopAsync();
            _worker.Dispose();
        }

        if (_api is not null)
        {
            await _api.DisposeAsync();
        }

        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _azurite.DisposeAsync().AsTask());
    }
}
