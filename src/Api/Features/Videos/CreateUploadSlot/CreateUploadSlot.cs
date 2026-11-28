using System.Security.Claims;
using FluentValidation;
using Microsoft.Extensions.Options;
using VideoProcessor.Api.Common;
using VideoProcessor.Core.Data;
using VideoProcessor.Core.Domain;
using VideoProcessor.Core.Infrastructure.Storage;

namespace VideoProcessor.Api.Features.Videos.CreateUploadSlot;

public sealed record CreateUploadSlotRequest(string FileName, long Size);

public sealed record CreateUploadSlotResponse(Guid VideoId, Uri UploadUrl, DateTimeOffset ExpiresAt);

internal sealed class CreateUploadSlotRequestValidator : AbstractValidator<CreateUploadSlotRequest>
{
    public CreateUploadSlotRequestValidator(IOptions<UploadOptions> options)
    {
        RuleFor(r => r.FileName).NotEmpty().MaximumLength(255);
        RuleFor(r => r.Size)
            .GreaterThan(0)
            .LessThanOrEqualTo(options.Value.MaxSizeBytes)
            .WithMessage($"'Size' must not exceed the maximum upload size of {options.Value.MaxSizeBytes} bytes.");
    }
}

public sealed class CreateUploadSlotHandler(
    AppDbContext db,
    VideoStorage storage,
    IOptions<UploadOptions> options,
    TimeProvider time)
{
    public async Task<Result<CreateUploadSlotResponse>> HandleAsync(
        string userId,
        CreateUploadSlotRequest request,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var video = Video.AwaitUpload(userId, request.FileName, request.Size, now);
        db.Videos.Add(video);
        await db.SaveChangesAsync(cancellationToken);

        var expiresAt = now.Add(options.Value.SlotLifetime);
        return new CreateUploadSlotResponse(video.Id, storage.GetUploadUrl(video.BlobName, expiresAt), expiresAt);
    }
}

public static class CreateUploadSlotEndpoint
{
    public static void Map(IEndpointRouteBuilder videos) =>
        videos.MapPost("/", async (
                CreateUploadSlotRequest request,
                ClaimsPrincipal user,
                CreateUploadSlotHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(user.GetUserId(), request, cancellationToken);
                return result.ToHttpResult(slot => TypedResults.Created($"/videos/{slot.VideoId}", slot));
            })
            .WithName("CreateUploadSlot")
            .WithSummary("Create an Upload slot")
            .WithDescription("Creates a Video awaiting upload and returns a short-lived, write-only URL for its blob.")
            .WithValidation<CreateUploadSlotRequest>()
            .Produces<CreateUploadSlotResponse>(StatusCodes.Status201Created);
}
