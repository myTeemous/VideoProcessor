using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using VideoProcessor.Core.Domain;

namespace VideoProcessor.Core.Infrastructure.FFprobe;

/// <summary>The parts of <c>ffprobe -print_format json -show_format -show_streams</c> output that become <see cref="MediaMetadata"/>.</summary>
internal sealed record FFprobeOutput(
    [property: JsonPropertyName("streams")] IReadOnlyList<FFprobeStream>? Streams,
    [property: JsonPropertyName("format")] FFprobeFormat? Format)
{
    private static readonly HashSet<string> WebmCodecs = ["vp8", "vp9", "av1", "vorbis", "opus"];

    public static FFprobeOutput? Parse(string json) => JsonSerializer.Deserialize<FFprobeOutput>(json);

    /// <summary>The metadata, or null when FFprobe found no container format.</summary>
    public MediaMetadata? ToMetadata()
    {
        if (Format?.FormatName is not { Length: > 0 } formatName)
        {
            return null;
        }

        var streams = Streams ?? [];

        // Cover art in audio files is reported as a video stream; it isn't one.
        var video = streams.FirstOrDefault(s => s.CodecType == "video" && s.Disposition?.AttachedPic != 1);
        var audio = streams.FirstOrDefault(s => s.CodecType == "audio");

        return new MediaMetadata(
            Container: NormaliseContainer(formatName, streams),
            Duration: ParseDouble(Format.Duration) is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
            Width: video?.Width,
            Height: video?.Height,
            VideoCodec: video?.CodecName,
            AudioCodec: audio?.CodecName,
            Bitrate: long.TryParse(Format.BitRate, CultureInfo.InvariantCulture, out var bitrate) ? bitrate : null,
            FrameRate: video is null ? null : ParseRate(video.AverageFrameRate) ?? ParseRate(video.RealFrameRate));
    }

    /// <summary>
    /// FFprobe names demuxers, not containers: one demuxer reads MP4, MOV, 3GP and others; another reads WebM and MKV.
    /// The ISO family is told apart by its major brand; WebM is Matroska restricted to WebM codecs.
    /// </summary>
    private string NormaliseContainer(string formatName, IReadOnlyList<FFprobeStream> streams) => formatName switch
    {
        "mov,mp4,m4a,3gp,3g2,mj2" => IsoContainer(Format?.Tags?.MajorBrand?.Trim()),
        "matroska,webm" => streams.All(s => s.CodecName is not null && WebmCodecs.Contains(s.CodecName)) ? "webm" : "mkv",
        _ => formatName.Split(',')[0],
    };

    /// <summary>
    /// Brands that mean a plain MP4 (ISO base media, MPEG-4 and Apple's M4V). Anything outside this list is not
    /// reported as mp4, so it can't pass an allow-list that names only mp4.
    /// </summary>
    private static readonly HashSet<string> Mp4Brands =
        ["isom", "iso2", "iso3", "iso4", "iso5", "iso6", "mp41", "mp42", "avc1", "M4V", "M4VH", "M4VP", "dash", "mmp4"];

    private static string IsoContainer(string? majorBrand) => majorBrand switch
    {
        // QuickTime files predating the ftyp box carry no brand.
        null or "" or "qt" => "mov",
        _ when Mp4Brands.Contains(majorBrand) => "mp4",
        _ when majorBrand.StartsWith("3g2", StringComparison.Ordinal) => "3g2",
        _ when majorBrand.StartsWith("3g", StringComparison.Ordinal) => "3gp",
        "mjp2" or "mj2s" => "mj2",
        _ => majorBrand.ToLowerInvariant(),
    };

    private static double? ParseDouble(string? value) =>
        double.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : null;

    /// <summary>Parses a rate such as "30000/1001"; "0/0" means unknown.</summary>
    private static double? ParseRate(string? rate)
    {
        var parts = rate?.Split('/');
        if (parts is not { Length: 2 }
            || ParseDouble(parts[0]) is not { } numerator
            || ParseDouble(parts[1]) is not ({ } denominator and not 0))
        {
            return null;
        }

        return numerator == 0 ? null : Math.Round(numerator / denominator, 3);
    }
}

internal sealed record FFprobeStream(
    [property: JsonPropertyName("codec_type")] string? CodecType,
    [property: JsonPropertyName("codec_name")] string? CodecName,
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height,
    [property: JsonPropertyName("avg_frame_rate")] string? AverageFrameRate,
    [property: JsonPropertyName("r_frame_rate")] string? RealFrameRate,
    [property: JsonPropertyName("disposition")] FFprobeDisposition? Disposition);

internal sealed record FFprobeDisposition([property: JsonPropertyName("attached_pic")] int AttachedPic);

internal sealed record FFprobeFormat(
    [property: JsonPropertyName("format_name")] string? FormatName,
    [property: JsonPropertyName("duration")] string? Duration,
    [property: JsonPropertyName("bit_rate")] string? BitRate,
    [property: JsonPropertyName("tags")] FFprobeTags? Tags);

internal sealed record FFprobeTags([property: JsonPropertyName("major_brand")] string? MajorBrand);
