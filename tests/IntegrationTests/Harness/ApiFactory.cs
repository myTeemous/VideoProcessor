using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VideoProcessor.IntegrationTests.Harness;

public sealed class ApiFactory(IReadOnlyDictionary<string, string?> settings) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }
}
