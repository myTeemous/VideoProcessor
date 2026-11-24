namespace VideoProcessor.Worker;

// Explicit (not top-level) so this Program doesn't collide with the Api's public Program in tests.
internal static class Program
{
    private static async Task Main(string[] args) =>
        await WorkerHost.CreateBuilder(args).Build().RunAsync();
}
