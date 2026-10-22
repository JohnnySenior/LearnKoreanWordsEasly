using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Infrastructure.Backups;
using KoreanAiTeacher.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KoreanAiTeacher.Api.Backups;

public static class BackupEndpoints
{
    public static IEndpointRouteBuilder MapBackupEndpoints(this IEndpointRouteBuilder app)
    {
        var backups = app.MapGroup("/api/backups").RequireAuthorization();

        backups.MapGet("/status", async (AppDbContext db, BackupService service, IOptions<BackupOptions> options, CancellationToken ct) =>
        {
            var last = await db.BackupRuns.OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
            var verified = await db.BackupRuns.Where(r => r.Verified).OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
            var sent = await db.BackupRuns.Where(r => r.SentToTelegramAt != null).OrderByDescending(r => r.SentToTelegramAt).FirstOrDefaultAsync(ct);
            var attempt = await db.BackupRuns.Where(r => r.TelegramAttemptAt != null).OrderByDescending(r => r.TelegramAttemptAt).FirstOrDefaultAsync(ct);
            var files = Directory.Exists(options.Value.Directory) ? Directory.GetFiles(options.Value.Directory, "kat-*.dump").Length : 0;
            var recipient = service.TelegramRecipient?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new BackupStatusDto(
                options.Value.Enabled,
                last is null ? null : new BackupRunDto(last.StartedAt, last.FinishedAt, last.SizeBytes, last.Verified, last.Error),
                verified?.StartedAt,
                files,
                service.TelegramReady,
                recipient is null ? null : "…" + recipient[^Math.Min(4, recipient.Length)..],
                sent?.SentToTelegramAt,
                attempt?.SentToTelegramAt is null ? attempt?.TelegramError : null);
        });

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
