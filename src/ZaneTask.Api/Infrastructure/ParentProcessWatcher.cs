using System.Diagnostics;

namespace ZaneTask.Api.Infrastructure;

/// <summary>
/// When started by the desktop app (Hosting:ParentProcessId), shut down as soon as that app exits so the
/// server never keeps running in the background on its own.
/// </summary>
internal sealed class ParentProcessWatcher(
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<ParentProcessWatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!int.TryParse(configuration["Hosting:ParentProcessId"], out var parentId))
            return;

        try
        {
            using var parent = Process.GetProcessById(parentId);
            await parent.WaitForExitAsync(stoppingToken);
        }
        catch (ArgumentException)
        {
            // The parent is already gone.
        }
        catch (OperationCanceledException)
        {
            return; // Normal shutdown.
        }

        logger.LogInformation("Parent process {ParentId} exited; shutting down.", parentId);
        lifetime.StopApplication();
    }
}
