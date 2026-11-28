using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoProcessor.Core.Domain;
using VideoProcessor.Core.Infrastructure.FFprobe;

namespace VideoProcessor.Core.Infrastructure.FFmpeg;

public sealed class FFmpegOptions
{
    public const string SectionName = "FFmpeg";

    /// <summary>The ffprobe executable: a name found on PATH, or a full path.</summary>
    public string FFprobePath { get; set; } = "ffprobe";

    /// <summary>How long a probe may run before it is killed and the input treated as unreadable.</summary>
    public TimeSpan ProbeTimeout { get; set; } = TimeSpan.FromSeconds(60);
}

/// <summary>
/// Runs FFmpeg tools as child processes. Arguments are always passed as a list, never as a shell string,
/// and every argument is either a constant or a value the application generated.
/// </summary>
internal sealed class FFmpegVideoProcessor(IOptions<FFmpegOptions> options, ILogger<FFmpegVideoProcessor> logger)
    : IVideoProcessor
{
    private static readonly Error Unreadable = Error.Validation("media.unreadable", "The file is not readable media.");

    public async Task<Result<MediaMetadata>> ProbeAsync(string input, CancellationToken cancellationToken)
    {
        string[] arguments =
        [
            "-v", "error",
            // Only the protocols needed to read this input, so a crafted file (an HLS playlist, say)
            // can't make FFprobe open local files or other kinds of URL.
            "-protocol_whitelist", IsHttp(input) ? "http,https,tcp,tls" : "file",
            "-print_format", "json",
            "-show_format",
            "-show_streams",
            input,
        ];

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.ProbeTimeout);

        ProcessOutcome outcome;
        try
        {
            outcome = await RunAsync(options.Value.FFprobePath, arguments, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("FFprobe timed out after {Timeout}.", options.Value.ProbeTimeout);
            return Unreadable;
        }

        if (outcome.ExitCode != 0)
        {
            logger.LogInformation("FFprobe exited with {ExitCode}: {Error}", outcome.ExitCode, outcome.StandardError);
            return Unreadable;
        }

        try
        {
            return FFprobeOutput.Parse(outcome.StandardOutput)?.ToMetadata() is { } metadata ? metadata : Unreadable;
        }
        catch (JsonException e)
        {
            logger.LogWarning(e, "FFprobe printed output that isn't the expected JSON.");
            return Unreadable;
        }
    }

    private static bool IsHttp(string input) =>
        input.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static async Task<ProcessOutcome> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{fileName}'.");

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new ProcessOutcome(process.ExitCode, await standardOutput, await standardError);
    }

    private sealed record ProcessOutcome(int ExitCode, string StandardOutput, string StandardError);
}
