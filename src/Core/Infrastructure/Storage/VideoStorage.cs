using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace VideoProcessor.Core.Infrastructure.Storage;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string VideosContainer { get; set; } = "videos";
}

/// <summary>The blob container holding Videos' files, and the SAS URLs that grant access to single blobs.</summary>
public sealed class VideoStorage(BlobServiceClient blobService, IOptions<BlobStorageOptions> options, TimeProvider time)
{
    private BlobContainerClient Container => blobService.GetBlobContainerClient(options.Value.VideosContainer);

    public async Task EnsureContainerExistsAsync(CancellationToken cancellationToken) =>
        await Container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

    /// <summary>A URL that can create and write the one blob and nothing else: it cannot read, list or delete.</summary>
    public Uri GetUploadUrl(string blobName, DateTimeOffset expiresAt) =>
        Container.GetBlobClient(blobName).GenerateSasUri(BlobSasPermissions.Create | BlobSasPermissions.Write, expiresAt);

    /// <summary>A URL that can only read the one blob.</summary>
    public Uri GetReadUrl(string blobName, TimeSpan lifetime) =>
        Container.GetBlobClient(blobName).GenerateSasUri(BlobSasPermissions.Read, time.GetUtcNow().Add(lifetime));

    /// <summary>The blob's size in bytes, or null when it doesn't exist.</summary>
    public async Task<long?> GetSizeAsync(string blobName, CancellationToken cancellationToken)
    {
        try
        {
            var properties = await Container.GetBlobClient(blobName).GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.ContentLength;
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return null;
        }
    }
}
