namespace VideoProcessor.Core.Domain;

/// <summary>
/// What FFprobe found in a media file. Video fields are null when the file has no video stream
/// (for example an audio Output); audio codec is null when it has no audio stream.
/// </summary>
/// <param name="Container">The container FFprobe detected, normalised to a short name such as mp4, mov, webm, mkv or avi.</param>
public sealed record MediaMetadata(
    string Container,
    TimeSpan? Duration,
    int? Width,
    int? Height,
    string? VideoCodec,
    string? AudioCodec,
    long? Bitrate,
    double? FrameRate)
{
    public bool HasVideo => VideoCodec is not null;

    public bool HasAudio => AudioCodec is not null;
}
