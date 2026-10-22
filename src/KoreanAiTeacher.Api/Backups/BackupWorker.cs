using KoreanAiTeacher.Infrastructure.Backups;
using Microsoft.Extensions.Options;

namespace KoreanAiTeacher.Api.Backups;

/// <summary>Runs the nightly backup once its time has come; a missed night is caught up after a restart (spec REL04).</summary>
public sealed class BackupWorker(IServiceScopeFactory scopes, IOptions<BackupOptions> options, ILogger<BackupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var backups = scope.ServiceProvider.GetRequiredService<BackupService>();
                if (await backups.IsDueAsync(stoppingToken))
                {
                    await backups.RunAsync(stoppingToken);
                }

                // The weekly copy outside Railway (D-37), sent after a verified backup exists.
                if (await backups.IsTelegramDueAsync(stoppingToken))
                {
                    await backups.SendToTelegramAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Backup worker iteration failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
