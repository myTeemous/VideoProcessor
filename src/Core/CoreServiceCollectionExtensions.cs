using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VideoProcessor.Core.Data;
using VideoProcessor.Core.Infrastructure;
using VideoProcessor.Core.Infrastructure.FFmpeg;
using VideoProcessor.Core.Infrastructure.Storage;

namespace VideoProcessor.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>Registers what both the Api and the Worker need: the database, Blob Storage and the clock.</summary>
    /// <remarks>Connection strings are read when first resolved, so hosts and tests can supply them late.</remarks>
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseNpgsql(RequiredConnectionString(sp, "Postgres")));

        services.AddSingleton(sp => new BlobServiceClient(RequiredConnectionString(sp, "BlobStorage")));
        services.AddOptions<BlobStorageOptions>().BindConfiguration(BlobStorageOptions.SectionName);
        services.AddSingleton<VideoStorage>();

        services.AddOptions<FFmpegOptions>().BindConfiguration(FFmpegOptions.SectionName);
        services.AddSingleton<IVideoProcessor, FFmpegVideoProcessor>();

        return services;
    }

    private static string RequiredConnectionString(IServiceProvider sp, string name) =>
        sp.GetRequiredService<IConfiguration>().GetConnectionString(name)
        ?? throw new InvalidOperationException($"Connection string '{name}' is not configured.");
}
