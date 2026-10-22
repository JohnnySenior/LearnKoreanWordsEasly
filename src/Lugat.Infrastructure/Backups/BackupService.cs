using Lugat.Application.Abstractions;
using System.Diagnostics;
using System.IO.Compression;
using System.Globalization;
using System.Text.Json;
using Lugat.Domain.Operations;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Lugat.Infrastructure.Backups;

public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>Off by default so local runs and tests never dump; production turns it on (D-22).</summary>
    public bool Enabled { get; set; }

    /// <summary>Folder on the app's persistent volume (a different volume from the database's).</summary>
    public string Directory { get; set; } = "/data/backups";

    /// <summary>Local time of the nightly run in <see cref="TimeZone"/>.</summary>
    public TimeOnly RunAt { get; set; } = new(4, 0);

    public string TimeZone { get; set; } = "Asia/Seoul";

    public int KeepDaily { get; set; } = 7;

    public int KeepWeekly { get; set; } = 4;

    /// <summary>Must be at least the server's major version (production: 18).</summary>
    public string PgDump { get; set; } = "pg_dump";

    public string PgRestore { get; set; } = "pg_restore";

    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Telegram user who gets the weekly copy outside Railway (D-37). Empty = the first of
    /// <c>Telegram:AllowedUserIds</c>, the app's owner.
    /// </summary>
    public long? TelegramChatId { get; set; }

    /// <summary>How often a verified copy goes to Telegram.</summary>
    public TimeSpan TelegramEvery { get; set; } = TimeSpan.FromDays(7);

    /// <summary>After a failed send, wait this long before trying again.</summary>
    public TimeSpan TelegramRetryAfter { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Restore steps linked from the Telegram caption. A full address on purpose: a bare "HOSTING.md" is turned by
    /// Telegram into a link to the unrelated site hosting.md (.md is Moldova's domain).
    /// </summary>
    public string RestoreGuideUrl { get; set; } = "https://github.com/JohnnySenior/Lugat/blob/main/docs/HOSTING.md#zaxira-nusxa-d-22";

    /// <summary>Bot API limit for a document a bot uploads (50 MB); settable only so tests need no large files.</summary>
    public long TelegramMaxBytes { get; set; } = 50L * 1024 * 1024;
}

/// <summary>
/// Nightly logical backup with an automatic restore check (spec REL04, D-22): pg_dump to the app volume, then
/// pg_restore into a scratch database on the same server, compare the schema with the live database, drop it.
/// The password goes to the tools through PGPASSWORD, never on the command line or into logs (SEC05).
/// </summary>
public sealed class BackupService(
    AppDbContext db,
    IConfiguration config,
    IOptions<BackupOptions> options,
    ITelegramMessenger telegram,
    TimeProvider time,
    ILogger<BackupService> logger,
    IOperationLock operations,
    IFileStore files)
{
    private BackupOptions Options => options.Value;

    /// <summary>Who gets the weekly copy; null when nobody is configured (D-37).</summary>
    public long? TelegramRecipient =>
        Options.TelegramChatId is > 0 ? Options.TelegramChatId : config.GetSection("Telegram:AllowedUserIds").Get<long[]>()?.FirstOrDefault(id => id > 0) is { } first and > 0 ? first : null;

    public bool TelegramReady => telegram.IsConfigured && TelegramRecipient is not null;

    /// <summary>
    /// True when the copy outside Railway is due: a verified backup exists, none was sent within
    /// <see cref="BackupOptions.TelegramEvery"/>, and the last failed attempt is old enough to retry.
    /// </summary>
    public async Task<bool> IsTelegramDueAsync(CancellationToken ct)
    {
        if (!TelegramReady)
        {
            return false;
        }

        var now = time.GetUtcNow();
        var sentSince = now - Options.TelegramEvery + TimeSpan.FromHours(1); // a little slack so the weekly run is not pushed a night later
        if (await db.BackupRuns.AnyAsync(r => r.SentToTelegramAt >= sentSince, ct))
        {
            return false;
        }

        var retrySince = now - Options.TelegramRetryAfter;
        if (await db.BackupRuns.AnyAsync(r => r.TelegramAttemptAt >= retrySince && r.SentToTelegramAt == null, ct))
        {
            return false;
        }

        return await db.BackupRuns.AnyAsync(r => r.Verified, ct);
    }

    /// <summary>Sends the newest verified backup file to the owner's Telegram chat (D-37). Returns the run it sent, or why not.</summary>
    public async Task<(BackupRun? Run, string? Error)> SendToTelegramAsync(CancellationToken ct)
    {
        if (TelegramRecipient is not { } chat || !telegram.IsConfigured)
        {
            return (null, "Telegram bot yoki qabul qiluvchi sozlanmagan.");
        }

        var run = await db.BackupRuns.Where(r => r.Verified && r.FileName != null).OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
        if (run is null)
        {
            return (null, "Hali tekshirilgan zaxira nusxa yo‘q.");
        }

        var now = time.GetUtcNow();
        run.TelegramAttemptAt = now;
        var dump = Path.Combine(Options.Directory, run.FileName!);
        var bundle = dump + ".zip";

        // The full bundle (database + original PDFs) when it fits; otherwise the database alone, which holds all the
        // learning history and is small. The books are the learner's own files and can be uploaded again (D-51).
        var path = File.Exists(bundle) && new FileInfo(bundle).Length <= Options.TelegramMaxBytes ? bundle : dump;
        var withoutBooks = path == dump && File.Exists(bundle);
        SendResult result;
        if (!File.Exists(path))
        {
            result = new SendResult(SendOutcome.Failed, Error: "Nusxa fayli diskda topilmadi.");
        }
        else if (new FileInfo(path).Length > Options.TelegramMaxBytes)
        {
            result = new SendResult(SendOutcome.Failed, Error: "Nusxa 50 MB dan katta, Telegram bot yubora olmaydi.");
        }
        else
        {
            var bytes = await File.ReadAllBytesAsync(path, ct);
            var caption = $"TOPIK yordamchi · zaxira nusxa {run.StartedAt:yyyy-MM-dd HH:mm} UTC · {bytes.Length / 1024.0 / 1024.0:0.##} MB · tiklab tekshirilgan.\n"
                + (withoutBooks
                    ? "Faqat baza (o‘quv tarixi): kitob PDF'lari bilan birga 50 MB dan oshdi, ularni o‘zingizdagi asl fayldan qayta yuklaysiz.\n"
                    : "")
                + "Bu faylni o‘chirmang: Railway'dagi baza yo‘qolsa, shu fayldan tiklanadi.\n"
                + $"Tiklash yo‘riqnomasi: {Options.RestoreGuideUrl}";
            result = await telegram.SendDocumentAsync(chat, Path.GetFileName(path), bytes, caption, ct);
        }

        if (result.Outcome == SendOutcome.Sent)
        {
            run.SentToTelegramAt = now;
            run.TelegramError = null;
        }
        else
        {
            run.TelegramError = Cut(result.Outcome == SendOutcome.Blocked
                ? "Bot sizga yoza olmadi: Telegram'da botni oching va /start bosing."
                : result.Error ?? "Telegram'ga yuborilmadi.", 500);
            logger.LogWarning("Backup {File} was not sent to Telegram: {Outcome}", run.FileName, result.Outcome);
        }

        await db.SaveChangesAsync(CancellationToken.None);
        return (run, run.TelegramError);
    }

    public async Task<BackupRun> RunAsync(CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync("backup-run", ct);
        var connection = new NpgsqlConnectionStringBuilder(config.GetConnectionString("Default"));
        var started = time.GetUtcNow();
        var run = new BackupRun { Id = Guid.CreateVersion7(started), StartedAt = started };
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync(ct);

        var fileName = $"lugat-{started:yyyyMMdd-HHmmss}.dump";
        var path = Path.Combine(Options.Directory, fileName);
        var scratch = $"lugat_restore_check_{started:yyyyMMddHHmmss}";
        try
        {
            // Inside the try: a folder that cannot be created is a failed backup, recorded like any other failure.
            var folder = System.IO.Directory.CreateDirectory(Options.Directory).FullName;
            path = Path.Combine(folder, fileName);
            // pg_dump and the verification manifest see exactly the same repeatable-read snapshot.
            await using var snapshotConnection = new NpgsqlConnection(connection.ConnectionString);
            await snapshotConnection.OpenAsync(ct);
            await using var snapshotTransaction = await snapshotConnection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            await using var snapshotCommand = new NpgsqlCommand("SELECT pg_export_snapshot()", snapshotConnection, snapshotTransaction);
            var snapshot = (string)(await snapshotCommand.ExecuteScalarAsync(ct))!;
            var live = await DescribeAsync(snapshotConnection, snapshotTransaction, ct);
            await ExecAsync(Options.PgDump, connection, ["--snapshot", snapshot, "--format=custom", "--no-owner", "--no-privileges", "--file", path + ".tmp", connection.Database!], ct);
            File.Move(path + ".tmp", path, overwrite: true);
            run.FileName = fileName;
            run.SizeBytes = new FileInfo(path).Length;

            await ExecSqlAsync(connection, $"CREATE DATABASE \"{scratch}\"", ct);
            try
            {
                var target = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = scratch };
                await ExecAsync(Options.PgRestore, target, ["--no-owner", "--no-privileges", "--exit-on-error", "--dbname", scratch, path], ct);
                await using var restoredConnection = new NpgsqlConnection(target.ConnectionString);
                await restoredConnection.OpenAsync(ct);
                var restored = await DescribeAsync(restoredConnection, null, ct);
                var problems = Compare(live, restored);
                run.RestoredCounts = JsonSerializer.Serialize(restored.Counts);
                if (problems is null) await CreateBundleAsync(path, snapshotConnection, snapshotTransaction, restored.Counts, ct);
                run.Verified = problems is null;
                run.Error = problems;
            }
            finally
            {
                await DropScratchAsync(connection, scratch);
            }

            Prune(folder);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError("Backup {File} failed: {Error}", fileName, ex.Message);
            run.Error = Cut(ex.Message, 1000);
            TryDelete(path + ".tmp");
        }

        run.FinishedAt = time.GetUtcNow();
        await db.SaveChangesAsync(CancellationToken.None);
        logger.LogInformation("Backup {File}: {Size} bytes, verified {Verified}", run.FileName, run.SizeBytes, run.Verified);
        return run;
    }

    /// <summary>True when the nightly run for the latest scheduled moment has not happened yet.</summary>
    public async Task<bool> IsDueAsync(CancellationToken ct)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(Options.TimeZone, out var z) ? z : TimeZoneInfo.Utc;
        var now = time.GetUtcNow();
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var todayRun = new DateTimeOffset(local.Date + Options.RunAt.ToTimeSpan(), local.Offset);
        var lastScheduled = todayRun <= local ? todayRun : todayRun.AddDays(-1);
        return !await db.BackupRuns.AnyAsync(r => r.StartedAt >= lastScheduled.ToUniversalTime(), ct);
    }

    private sealed record DbShape(string? LastMigration, SortedDictionary<string, long> Counts);

    private sealed record OriginalFile(string Key, string Sha256);

    /// <summary>The recoverable bundle contains the dump, referenced PDFs and a hash/count manifest, not session secrets.</summary>
    private async Task CreateBundleAsync(string dump, NpgsqlConnection connection, NpgsqlTransaction transaction,
        SortedDictionary<string, long> counts, CancellationToken ct)
    {
        var originals = new List<OriginalFile>();
        await using (var command = new NpgsqlCommand("SELECT \"StorageKey\", \"ContentSha256\" FROM source_documents", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct)) originals.Add(new OriginalFile(reader.GetString(0), reader.GetString(1)));
        }

        var temp = dump + ".zip.tmp";
        try
        {
            using (var archive = System.IO.Compression.ZipFile.Open(temp, System.IO.Compression.ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(dump, "database.dump", System.IO.Compression.CompressionLevel.Optimal);
                foreach (var original in originals.DistinctBy(f => f.Key))
                {
                    await using var stream = await files.OpenReadAsync(original.Key, ct)
                        ?? throw new IOException("Zaxiralash uchun asl PDF topilmadi. Kitobni qayta yuklang.");
                    var entry = archive.CreateEntry("files/" + original.Key, System.IO.Compression.CompressionLevel.Optimal);
                    await using var destination = entry.Open();
                    using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
                    var buffer = new byte[65536];
                    int length;
                    while ((length = await stream.ReadAsync(buffer, ct)) != 0)
                    {
                        hash.AppendData(buffer, 0, length);
                        await destination.WriteAsync(buffer.AsMemory(0, length), ct);
                    }
                    if (Convert.ToHexStringLower(hash.GetHashAndReset()) != original.Sha256)
                        throw new IOException("Asl PDF xeshi bazadagi qiymatga mos emas.");
                }

                await using var manifest = archive.CreateEntry("manifest.json").Open();
                await JsonSerializer.SerializeAsync(manifest, new { version = 1, counts, originals }, cancellationToken: ct);
            }

            File.Move(temp, dump + ".zip", overwrite: true);
        }
        finally { TryDelete(temp); }
    }

    private static string? Compare(DbShape live, DbShape restored)
    {
        var missing = live.Counts.Keys.Except(restored.Counts.Keys).ToList();
        if (missing.Count > 0)
        {
            return "Tiklangan nusxada jadvallar yo‘q: " + string.Join(", ", missing);
        }

        if (live.LastMigration != restored.LastMigration)
        {
            return $"Migratsiya mos emas: {live.LastMigration} / {restored.LastMigration}";
        }

        var different = live.Counts.Where(t => restored.Counts.GetValueOrDefault(t.Key) != t.Value).Select(t => t.Key).ToList();
        return different.Count == 0 ? null : "Tiklangan nusxada qatorlar soni mos emas: " + string.Join(", ", different);
    }

    private static async Task<DbShape> DescribeAsync(NpgsqlConnection conn, NpgsqlTransaction? transaction, CancellationToken ct)
    {
        var tables = new List<string>();
        await using (var cmd = new NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY 1", conn, transaction))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                tables.Add(reader.GetString(0));
            }
        }

        var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            await using var count = new NpgsqlCommand($"SELECT count(*) FROM \"{table.Replace("\"", "\"\"", StringComparison.Ordinal)}\"", conn, transaction);
            counts[table] = (long)(await count.ExecuteScalarAsync(ct))!;
        }

        string? last = null;
        if (tables.Contains("__EFMigrationsHistory"))
        {
            await using var cmd = new NpgsqlCommand("SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\"", conn, transaction);
            last = await cmd.ExecuteScalarAsync(ct) as string;
        }

        return new DbShape(last, counts);
    }

    private async Task ExecAsync(string tool, NpgsqlConnectionStringBuilder target, string[] args, CancellationToken ct)
    {
        var start = new ProcessStartInfo(tool)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("--host");
        start.ArgumentList.Add(target.Host!);
        start.ArgumentList.Add("--port");
        start.ArgumentList.Add(target.Port.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--username");
        start.ArgumentList.Add(target.Username!);
        start.ArgumentList.Add("--no-password");
        foreach (var a in args)
        {
            start.ArgumentList.Add(a);
        }

        start.Environment["PGPASSWORD"] = target.Password ?? "";
        start.Environment["PGSSLMODE"] = "prefer";
        start.Environment["PGGSSENCMODE"] = "disable";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Options.CommandTimeout);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{tool} ishga tushmadi.");
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{Path.GetFileName(tool)} {Options.CommandTimeout} ichida tugamadi.");
        }

        await stdout;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(tool)} xato bilan tugadi ({process.ExitCode}): {Cut((await stderr).Trim(), 600)}");
        }
    }

    private static async Task ExecSqlAsync(NpgsqlConnectionStringBuilder target, string sql, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(target.ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task DropScratchAsync(NpgsqlConnectionStringBuilder connection, string scratch)
    {
        NpgsqlConnection.ClearAllPools();
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                await ExecSqlAsync(connection, $"DROP DATABASE IF EXISTS \"{scratch}\" WITH (FORCE)", CancellationToken.None);
                return;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
            {
                await Task.Delay(250); // an autovacuum worker on the fresh database finishes quickly
            }
        }

        logger.LogWarning("Scratch database {Database} could not be dropped", scratch);
    }

    /// <summary>Keeps the newest <see cref="BackupOptions.KeepDaily"/> files and one per week for <see cref="BackupOptions.KeepWeekly"/> weeks.</summary>
    private void Prune(string folder)
    {
        var files = new DirectoryInfo(folder).GetFiles("lugat-*.dump").OrderByDescending(f => f.Name, StringComparer.Ordinal).ToList();
        var keep = files.Take(Options.KeepDaily).ToHashSet();
        foreach (var weekly in files.GroupBy(f => ISOWeek.GetYear(f.LastWriteTimeUtc) * 100 + ISOWeek.GetWeekOfYear(f.LastWriteTimeUtc))
                     .Take(Options.KeepWeekly).Select(g => g.First()))
        {
            keep.Add(weekly);
        }

        foreach (var f in files.Where(f => !keep.Contains(f)))
        {
            TryDelete(f.FullName);
            TryDelete(f.FullName + ".zip");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private static string Cut(string text, int max) => text.Length > max ? text[..max] : text;
}
