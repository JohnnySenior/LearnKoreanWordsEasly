using KoreanAiTeacher.Application.Imports;
using Microsoft.Extensions.Options;

namespace KoreanAiTeacher.Api.Imports;

/// <summary>
/// Runs queued imports in the background (spec IMP02): closing the app does not lose the job, and
/// jobs left "Extracting" by a crash are picked up again after a restart.
/// </summary>
public sealed class ImportWorker(IServiceScopeFactory scopes, IOptions<ImportOptions> options, ILogger<ImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ImportService>().ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Import worker iteration failed");
            }

            try
            {
                await Task.Delay(options.Value.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
