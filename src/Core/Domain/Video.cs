namespace VideoProcessor.Core.Domain;

/// <summary>A file a User uploaded, together with its metadata. Only Ready Videos can be processed.</summary>
public sealed class Video
{
    public Guid Id { get; private set; }

    public string UserId { get; private set; } = null!;

    /// <summary>The User's original file name, kept for display only; never used to name or locate the blob.</summary>
    public string FileName { get; private set; } = null!;

    public long DeclaredSize { get; private set; }

    /// <summary>The size of the blob as found at Confirm.</summary>
    public long? Size { get; private set; }

    /// <summary>The service-generated name of the Video's blob.</summary>
    public string BlobName { get; private set; } = null!;

    public VideoStatus Status { get; private set; }

    public VideoRejectionReason? RejectionReason { get; private set; }

    public MediaMetadata? Metadata { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    private Video()
    {
    }

    public static Video AwaitUpload(string userId, string fileName, long declaredSize, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        UserId = userId,
        FileName = fileName,
        DeclaredSize = declaredSize,
        BlobName = Guid.NewGuid().ToString("N"),
        Status = VideoStatus.AwaitingUpload,
        CreatedAt = now,
    };

    public void MarkReady(long size, MediaMetadata metadata, DateTimeOffset now)
    {
        Confirm(size, now);
        Status = VideoStatus.Ready;
        Metadata = metadata;
    }

    public void Reject(long size, VideoRejectionReason reason, DateTimeOffset now)
    {
        Confirm(size, now);
        Status = VideoStatus.Rejected;
        RejectionReason = reason;
    }

    private void Confirm(long size, DateTimeOffset now)
    {
        // An invariant, not flow control: callers report a Video that isn't AwaitingUpload as a Conflict
        // before getting here, so reaching this throw means a bug.
        if (Status != VideoStatus.AwaitingUpload)
        {
            throw new InvalidOperationException($"Invariant violated: Video {Id} is {Status}, not AwaitingUpload.");
        }

        Size = size;
        ConfirmedAt = now;
    }
}

public enum VideoStatus
{
    AwaitingUpload,
    Ready,
    Rejected,
}

public enum VideoRejectionReason
{
    /// <summary>FFprobe could not read the file, or it has no video stream.</summary>
    NotAVideo,

    /// <summary>The container FFprobe detected is not in the allow-list.</summary>
    ContainerNotAllowed,

    /// <summary>The blob is larger than the maximum upload size.</summary>
    TooLarge,
}
