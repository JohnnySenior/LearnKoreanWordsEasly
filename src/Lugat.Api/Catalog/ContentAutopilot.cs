using Lugat.Application;
using Lugat.Application.Ai;
using Lugat.Application.Catalog;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Microsoft.Extensions.Options;

namespace Lugat.Api.Catalog;

/// <summary>
/// L-87: runs <see cref="ContentAutopilotService"/> step by step in the background, one level at a time, until the
/// level is published, a problem needs the editor, the AI limit or budget is reached, or the admin stops it. The state
/// is in memory (one app process): after a restart or deploy the job is not running, and starting it again continues
/// where the saved work ends, since every step saves its own result.
/// </summary>
public sealed class ContentAutopilot(IServiceScopeFactory scopes, IHostApplicationLifetime life, IOptions<AiOptions> ai, TimeProvider time, IConfiguration config, ILogger<ContentAutopilot> logger)
{
    /// <summary>A strong model writing ten cards may need minutes; nothing waits on the job but the admin page.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromMinutes(4);

    private const int MaxLog = 30;
    private const int MaxRetries = 3;

    private readonly Lock gate = new();
    private CancellationTokenSource? cancel;
    private ContentAutopilotDto state = new(false, null, null, [], [], false, null, null, null);

    public ContentAutopilotDto Status
    {
        get
        {
            lock (gate)
            {
                return state with { ContentModel = ContentModel };
            }
        }
    }

    private string? ContentModel => ai.Value.For(ai.Value.DefaultProvider) is { } p && !string.IsNullOrWhiteSpace(p.ContentModel) ? p.ContentModel.Trim() : null;

    public ContentAutopilotDto Start(Guid userId, CatalogLevel level)
    {
        lock (gate)
        {
            if (state.Running)
            {
                throw new ConflictException($"{state.Level} daraja allaqachon tayyorlanmoqda. Avval uni to‘xtating.");
            }

            cancel = CancellationTokenSource.CreateLinkedTokenSource(life.ApplicationStopping);
            state = new(true, level, "Boshlanmoqda…", [], [], false, time.GetUtcNow(), null, null);
            var token = cancel.Token;
            _ = Task.Run(() => RunAsync(userId, level, token), CancellationToken.None);
        }

        return Status;
    }

    public ContentAutopilotDto Stop()
    {
        lock (gate)
        {
            cancel?.Cancel();
        }

        return Status;
    }

    private async Task RunAsync(Guid userId, CatalogLevel level, CancellationToken ct)
    {
        var failures = 0;
        while (true)
        {
            AutopilotStep step;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<AiGateway>().Patience = Patience;
                step = await scope.ServiceProvider.GetRequiredService<ContentAutopilotService>().StepAsync(userId, level, ct);
                failures = 0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Finish("To‘xtatildi. Qilingan ish saqlangan; qayta boshlasangiz, shu joydan davom etadi.", false);
                return;
            }
            catch (Exception ex) when (ex is ServiceUnavailableException or ConflictException && ++failures < MaxRetries)
            {
                Note($"{ex.Message} Qayta urinilmoqda ({failures}/{MaxRetries - 1})…");
                try
                {
                    // Real time (not the app clock): a provider hiccup or overload needs a little while to pass.
                    await Task.Delay(config.GetValue("Catalog:AutopilotRetryDelay", TimeSpan.FromSeconds(30)) * failures, ct);
                }
                catch (OperationCanceledException)
                {
                    Finish("To‘xtatildi. Qilingan ish saqlangan; qayta boshlasangiz, shu joydan davom etadi.", false);
                    return;
                }

                continue;
            }
            catch (Exception ex)
            {
                if (ex is not AppException)
                {
                    logger.LogError(ex, "Content autopilot step failed for level {Level}", level);
                }

                // The service's own messages are written for the editor (limits, budget, a missing price); others are not.
                Finish(ex is AppException ? ex.Message : "Kutilmagan xato yuz berdi (server logida). Qilingan ish saqlangan; qayta boshlab ko‘ring.", true);
                return;
            }

            Note(step.Message, step.PublishedUnit);
            if (step.Finished)
            {
                Finish(step.Message, step.Stopped);
                return;
            }
        }
    }

    private void Note(string message, int? published = null)
    {
        lock (gate)
        {
            state = state with
            {
                Message = message,
                Log = [message, .. state.Log.Take(MaxLog - 1)],
                PublishedUnits = published is { } n ? [.. state.PublishedUnits, n] : state.PublishedUnits,
            };
        }
    }

    private void Finish(string message, bool stopped)
    {
        lock (gate)
        {
            state = state with
            {
                Running = false,
                Message = message,
                Stopped = stopped,
                FinishedAt = time.GetUtcNow(),
                Log = state.Log.FirstOrDefault() == message ? state.Log : [message, .. state.Log.Take(MaxLog - 1)],
            };
        }
    }
}
