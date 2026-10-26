using Lugat.Api.Auth;
using Lugat.Contracts;
using Lugat.Infrastructure.Backups;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lugat.Api.Backups;

public static class BackupEndpoints
{
    public static IEndpointRouteBuilder MapBackupEndpoints(this IEndpointRouteBuilder app)
    {
        // Backups hold every user's data: the owner only (spec §6.4).
        var backups = app.MapGroup("/api/backups").RequireAuthorization(Policies.Admin);

        backups.MapGet("/status", async (AppDbContext db, BackupService service, IOptions<BackupOptions> options, CancellationToken ct) =>
        {
            var last = await db.BackupRuns.OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
            var verified = await db.BackupRuns.Where(r => r.Verified).OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
            var sent = await db.BackupRuns.Where(r => r.SentToTelegramAt != null).OrderByDescending(r => r.SentToTelegramAt).FirstOrDefaultAsync(ct);
            var attempt = await db.BackupRuns.Where(r => r.TelegramAttemptAt != null).OrderByDescending(r => r.TelegramAttemptAt).FirstOrDefaultAsync(ct);
            var files = Directory.Exists(options.Value.Directory) ? Directory.GetFiles(options.Value.Directory, "lugat-*.dump").Length : 0;
            var recipient = service.TelegramRecipient?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new BackupStatusDto(
                options.Value.Enabled,
                last is null ? null : new BackupRunDto(last.StartedAt, last.FinishedAt, last.SizeBytes, last.Verified, last.Error),
                verified?.StartedAt,
                files,
                service.TelegramReady,
                recipient is null ? null : "…" + recipient[^Math.Min(4, recipient.Length)..],
                sent?.SentToTelegramAt,
                attempt?.SentToTelegramAt is null ? attempt?.TelegramError : null,
                (await service.TelegramOverdueAsync(ct)).Select(r => r.StartedAt).ToList());
        });

        // "O‘chirdim": the owner deleted the copies older than a month from the Telegram chat (a bot cannot).
        backups.MapPost("/telegram-removed", async (BackupService service, CancellationToken ct) =>
            new BackupSendResult(true, await service.ConfirmTelegramRemovedAsync(ct) is var n and > 0
                ? $"{n} ta eski nusxa Telegram'dan o‘chirilgan deb belgilandi."
                : "Muddati o‘tgan nusxa yo‘q."));

        // "Telegram'ga yuborish": the newest verified copy to the owner's chat, at most once per 10 minutes (D-37).
        backups.MapPost("/send", async (AppDbContext db, BackupService service, IOptions<BackupOptions> options, TimeProvider time, CancellationToken ct) =>
        {
            if (!options.Value.Enabled)
            {
                return Results.Problem(detail: "Zaxiralash bu serverda yoqilmagan.", statusCode: StatusCodes.Status409Conflict);
            }

            var since = time.GetUtcNow().AddMinutes(-10);
            if (await db.BackupRuns.AnyAsync(r => r.TelegramAttemptAt >= since, ct))
            {
                return Results.Problem(detail: "Nusxa hozirgina yuborilgan. 10 daqiqadan keyin qayta urinib ko‘ring.", statusCode: StatusCodes.Status409Conflict);
            }

            var (run, error) = await service.SendToTelegramAsync(ct);
            return run?.SentToTelegramAt is not null
                ? Results.Ok(new BackupSendResult(true, "Nusxa Telegram'ga yuborildi. Botdagi faylni saqlab qo‘ying."))
                : Results.Ok(new BackupSendResult(false, error ?? "Yuborilmadi."));
        });

        // "Hozir zaxiralash": at most one run per 10 minutes, so a double tap cannot start two dumps.
        backups.MapPost("/run", async (AppDbContext db, BackupService service, IOptions<BackupOptions> options, TimeProvider time, CancellationToken ct) =>
        {
            if (!options.Value.Enabled)
            {
                return Results.Problem(detail: "Zaxiralash bu serverda yoqilmagan.", statusCode: StatusCodes.Status409Conflict);
            }

            var since = time.GetUtcNow().AddMinutes(-10);
            if (await db.BackupRuns.AnyAsync(r => r.StartedAt >= since, ct))
            {
                return Results.Problem(detail: "Zaxira nusxa hozirgina olingan. 10 daqiqadan keyin qayta urinib ko‘ring.", statusCode: StatusCodes.Status409Conflict);
            }

            var run = await service.RunAsync(ct);
            return Results.Ok(new BackupRunDto(run.StartedAt, run.FinishedAt, run.SizeBytes, run.Verified, run.Error));
        });

        return app;
    }
}
