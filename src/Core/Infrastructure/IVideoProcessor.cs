using VideoProcessor.Core.Domain;

namespace VideoProcessor.Core.Infrastructure;

/// <summary>Video processing, kept behind one interface so callers never depend on how FFmpeg tools are run.</summary>
public interface IVideoProcessor
{
    /// <summary>
    /// Reads a media file's metadata. <paramref name="input"/> is a local path or an http(s) URL; FFprobe reads only
    /// headers and indexes, so URLs are cheap even for large files. Fails when the input isn't readable media;
    /// throws when FFprobe can't run, can't reach the input or times out, since that says nothing about the file.
    /// </summary>
    Task<Result<MediaMetadata>> ProbeAsync(string input, CancellationToken cancellationToken);
}
