namespace VideoProcessor.Api.Features.Videos;

/// <summary>Limits and lifetimes for Upload slots and Confirm, bound from the "Uploads" configuration section.</summary>
public sealed class UploadOptions
{
    public const string SectionName = "Uploads";

    public long MaxSizeBytes { get; set; } = 5L * 1024 * 1024 * 1024;

    public static readonly string[] DefaultAllowedContainers = ["mp4", "mov", "webm", "mkv", "avi"];

    /// <summary>
    /// Containers, as FFprobe detects them, that a Video may use; <see cref="DefaultAllowedContainers"/> when empty.
    /// Not initialised with the defaults, because configuration binding appends to an existing array
    /// rather than replacing it.
    /// </summary>
    public string[] AllowedContainers { get; set; } = [];

    /// <summary>How long an Upload slot's write-only URL stays valid.</summary>
    public TimeSpan SlotLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>How long the read-only URL FFprobe uses at Confirm stays valid.</summary>
    public TimeSpan ProbeUrlLifetime { get; set; } = TimeSpan.FromMinutes(5);
}
