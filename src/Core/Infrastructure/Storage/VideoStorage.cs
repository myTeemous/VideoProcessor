using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace VideoProcessor.Core.Infrastructure.Storage;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    /// <summary>Where Users upload through Upload slots. Blobs here are untrusted and short-lived.</summary>
    public string UploadsContainer { get; set; } = "uploads";

    /// <summary>Where confirmed Videos' files live. No Upload slot URL can write here.</summary>
    public string VideosContainer { get; set; } = "videos";
}

/// <summary>
/// Videos' files and the SAS URLs that grant access to single blobs. A Video's file is uploaded to the uploads
/// container and copied into the videos container at Confirm, so the Upload slot URL, which stays valid for a while,
/// can never change a file after it was validated (ADR-0005).
/// </summary>
public sealed class VideoStorage(BlobServiceClient blobService, IOptions<BlobStorageOptions> options, TimeProvider time)
{
    private BlobContainerClient Uploads => blobService.GetBlobContainerClient(options.Value.UploadsContainer);

    private BlobContainerClient Videos => blobService.GetBlobContainerClient(options.Value.VideosContainer);

    public async Task EnsureContainersExistAsync(CancellationToken cancellationToken)
    {
        await Uploads.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await Videos.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    }

    /// <summary>A URL that can create and write the one upload blob and nothing else: it cannot read, list or delete.</summary>
    public Uri GetUploadUrl(string blobName, DateTimeOffset expiresAt) =>
        Uploads.GetBlobClient(blobName).GenerateSasUri(BlobSasPermissions.Create | BlobSasPermissions.Write, expiresAt);

    /// <summary>The upload blob's size in bytes, or null when nothing has been uploaded.</summary>
    public async Task<long?> GetUploadSizeAsync(string blobName, CancellationToken cancellationToken)
    {
        try
        {
            var properties = await Uploads.GetBlobClient(blobName).GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.ContentLength;
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Copies the upload blob into the videos container, service side, and returns the copy's size. Validate the copy,
    /// not the upload: the upload can still be overwritten through its Upload slot URL.
    /// </summary>
    public async Task<long> CopyUploadToVideosAsync(string blobName, CancellationToken cancellationToken)
    {
        // Within one storage account the copy is authorised by the account credential, so the source needs no SAS.
        var source = Uploads.GetBlobClient(blobName).Uri;
        var target = Videos.GetBlobClient(blobName);

        var copy = await target.StartCopyFromUriAsync(source, cancellationToken: cancellationToken);
        await copy.WaitForCompletionAsync(cancellationToken);

        var properties = await target.GetPropertiesAsync(cancellationToken: cancellationToken);
        return properties.Value.ContentLength;
    }

    public async Task DeleteUploadAsync(string blobName, CancellationToken cancellationToken) =>
        await Uploads.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    public async Task DeleteVideoAsync(string blobName, CancellationToken cancellationToken) =>
        await Videos.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    /// <summary>A URL that can only read the one Video blob.</summary>
    public Uri GetVideoReadUrl(string blobName, TimeSpan lifetime) =>
        Videos.GetBlobClient(blobName).GenerateSasUri(BlobSasPermissions.Read, time.GetUtcNow().Add(lifetime));
}
