using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VideoProcessor.Api.Common;
using VideoProcessor.Core.Data;
using VideoProcessor.Core.Domain;
using VideoProcessor.Core.Infrastructure;
using VideoProcessor.Core.Infrastructure.Storage;

namespace VideoProcessor.Api.Features.Videos.ConfirmUpload;

public sealed record ConfirmUploadResponse(
    Guid Id,
    string FileName,
    VideoStatus Status,
    VideoRejectionReason? RejectionReason,
    long DeclaredSize,
    long? Size,
    MediaMetadataDto? Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt);

public sealed record MediaMetadataDto(
    string Container,
    double? DurationSeconds,
    int? Width,
    int? Height,
    string? VideoCodec,
    string? AudioCodec,
    long? Bitrate,
    double? FrameRate,
    bool HasAudio);

/// <summary>Validates an uploaded blob (ADR-0004) and makes its Video Ready or Rejected.</summary>
public sealed class ConfirmUploadHandler(
    AppDbContext db,
    VideoStorage storage,
    IVideoProcessor videoProcessor,
    IOptions<UploadOptions> options,
    TimeProvider time)
{
    public async Task<Result<ConfirmUploadResponse>> HandleAsync(
        string userId,
        Guid videoId,
        CancellationToken cancellationToken)
    {
        var video = await db.Videos.SingleOrDefaultAsync(v => v.Id == videoId && v.UserId == userId, cancellationToken);
        if (video is null)
        {
            return Error.NotFound("video.not_found", $"Video {videoId} was not found.");
        }

        if (video.Status != VideoStatus.AwaitingUpload)
        {
            return Error.Conflict("video.not_awaiting_upload", $"Video {videoId} is {video.Status}, not AwaitingUpload.");
        }

        if (await storage.GetUploadSizeAsync(video.BlobName, cancellationToken) is not { } uploadSize)
        {
            return Error.Validation("video.blob_missing", "No file has been uploaded for this Video.");
        }

        await Validate(video, uploadSize, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The request that won owns the blobs now; leave them alone.
            return Error.Conflict("video.not_awaiting_upload", $"Video {videoId} was confirmed by another request.");
        }

        // The Upload slot is spent. A Rejected Video keeps no file.
        await storage.DeleteUploadAsync(video.BlobName, cancellationToken);
        if (video.Status == VideoStatus.Rejected)
        {
            await storage.DeleteVideoAsync(video.BlobName, cancellationToken);
        }

        return ToResponse(video);
    }

    private async Task Validate(Video video, long uploadSize, CancellationToken cancellationToken)
    {
        // The declared size was only a hint; the blob itself must be within the limit.
        // Checked before copying so an oversized upload is never copied.
        if (uploadSize > options.Value.MaxSizeBytes)
        {
            video.Reject(uploadSize, VideoRejectionReason.TooLarge, time.GetUtcNow());
            return;
        }

        // From here on only the copy counts: the upload can still change through its Upload slot URL.
        var size = await storage.CopyUploadToVideosAsync(video.BlobName, cancellationToken);
        if (size > options.Value.MaxSizeBytes)
        {
            video.Reject(size, VideoRejectionReason.TooLarge, time.GetUtcNow());
            return;
        }

        if (size == 0)
        {
            video.Reject(size, VideoRejectionReason.NotAVideo, time.GetUtcNow());
            return;
        }

        var readUrl = storage.GetVideoReadUrl(video.BlobName, options.Value.ProbeUrlLifetime);
        var probe = await videoProcessor.ProbeAsync(readUrl.AbsoluteUri, cancellationToken);
        var now = time.GetUtcNow();

        if (!probe.IsSuccess || !probe.Value.HasVideo)
        {
            video.Reject(size, VideoRejectionReason.NotAVideo, now);
        }
        else if (!options.Value.AllowedContainers.Contains(probe.Value.Container, StringComparer.OrdinalIgnoreCase))
        {
            video.Reject(size, VideoRejectionReason.ContainerNotAllowed, now);
        }
        else
        {
            video.MarkReady(size, probe.Value, now);
        }
    }

    private static ConfirmUploadResponse ToResponse(Video video) => new(
        video.Id,
        video.FileName,
        video.Status,
        video.RejectionReason,
        video.DeclaredSize,
        video.Size,
        video.Metadata is { } m
            ? new MediaMetadataDto(
                m.Container,
                m.Duration?.TotalSeconds,
                m.Width,
                m.Height,
                m.VideoCodec,
                m.AudioCodec,
                m.Bitrate,
                m.FrameRate,
                m.HasAudio)
            : null,
        video.CreatedAt,
        video.ConfirmedAt);
}

public static class ConfirmUploadEndpoint
{
    public static void Map(IEndpointRouteBuilder videos) =>
        videos.MapPost("/{id:guid}/confirm", async (
                Guid id,
                ClaimsPrincipal user,
                ConfirmUploadHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(user.GetUserId(), id, cancellationToken);
                return result.ToHttpResult(TypedResults.Ok);
            })
            .WithName("ConfirmUpload")
            .WithSummary("Confirm an upload")
            .WithDescription(
                "Validates the uploaded file with FFprobe and makes the Video Ready with its metadata, "
                + "or Rejected with a reason.")
            .Produces<ConfirmUploadResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
