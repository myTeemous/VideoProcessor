using VideoProcessor.Core;

namespace VideoProcessor.Worker;

/// <summary>Composes the Worker host. Integration tests use it to start the Worker in-process.</summary>
public static class WorkerHost
{
    public static HostApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddCore();
        return builder;
    }
}
