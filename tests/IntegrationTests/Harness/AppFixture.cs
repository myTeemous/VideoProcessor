using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;
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

    private readonly AzuriteContainer _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.38.0")
        .WithCommand("--skipApiVersionCheck")
        .Build();

    /// <summary>The maximum upload size the Api runs with in tests: small enough for a test to exceed with a real blob.</summary>
    public const long MaxUploadSizeBytes = 5 * 1024 * 1024;

    private ApiFactory? _api;
    private IHost? _worker;

    /// <summary>The clock the Worker sees. Advance it to drive time-based Worker behaviour.</summary>
    public FakeTimeProvider Time { get; } = new();

    public HttpClient CreateClient() => Api.CreateClient();

    /// <summary>Registers a new User, logs in and returns a client that sends their bearer token.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        const string password = "Correct-horse-1";
        var client = CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password });
        register.EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    private ApiFactory Api => _api ?? throw new InvalidOperationException("Fixture not initialised.");

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _azurite.StartAsync());

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            ["ConnectionStrings:BlobStorage"] = _azurite.GetConnectionString(),
            ["Uploads:MaxSizeBytes"] = MaxUploadSizeBytes.ToString(CultureInfo.InvariantCulture),
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
