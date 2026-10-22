using System.Net.Http.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Api.Tests.Me;
using Lugat.Application.Abstractions;
using Lugat.Domain.Operations;
using Lugat.Infrastructure.Persistence;
using Lugat.Contracts;
using Lugat.Infrastructure.Backups;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Backups;

/// <summary>Real pg_dump and pg_restore against the throw-away test database (spec REL04: restore is actually tried).</summary>
public class BackupTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static bool ToolsMissing() =>
        !(Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Any(p => File.Exists(Path.Combine(p, "pg_dump")));

    [Fact]
    public async Task Backup_is_written_restored_into_a_scratch_database_and_marked_verified()
    {
        Assert.SkipWhen(ToolsMissing(), "pg_dump is not installed");
        var folder = Path.Combine(Path.GetTempPath(), "lugat-backup-test", Guid.NewGuid().ToString("N"));
        await using var factory = new ApiFactory();
        factory.Settings["Backup:Directory"] = folder;
        var client = await factory.SignedInClientAsync(ApiFactory.UserA); // creates a user row

        await using var scope = factory.Services.CreateAsyncScope();
        var run = await scope.ServiceProvider.GetRequiredService<BackupService>().RunAsync(CancellationToken.None);
        var status = await client.GetFromJsonAsync<BackupStatusDto>("/api/backups/status", Json);

        Assert.Null(run.Error);
        Assert.True(run.Verified);
        Assert.True(File.Exists(Path.Combine(folder, run.FileName!)));
        Assert.True(run.SizeBytes > 0);
        Assert.Contains("\"users\":1", run.RestoredCounts);
        Assert.True((status!.LastVerifiedAt!.Value - run.StartedAt).Duration() < TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, status.FilesKept);
        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        using (var archive = ZipFile.OpenRead(Path.Combine(folder, run.FileName! + ".zip")))
        {
            Assert.NotNull(archive.GetEntry("database.dump"));
            using var manifest = JsonDocument.Parse(archive.GetEntry("manifest.json")!.Open());
            Assert.Equal(1, manifest.RootElement.GetProperty("counts").GetProperty("users").GetInt32());
        }
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task A_failed_dump_is_recorded_and_never_counts_as_a_backup()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lugat-backup-test", Guid.NewGuid().ToString("N"));
        await using var factory = new ApiFactory();
        factory.Settings["Backup:Directory"] = folder;
        factory.Settings["Backup:PgDump"] = "/nonexistent/pg_dump";
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        await using var scope = factory.Services.CreateAsyncScope();
        var run = await scope.ServiceProvider.GetRequiredService<BackupService>().RunAsync(CancellationToken.None);
        var status = await client.GetFromJsonAsync<BackupStatusDto>("/api/backups/status", Json);

        Assert.False(run.Verified);
        Assert.NotNull(run.Error);
        Assert.Null(status!.LastVerifiedAt);
        Assert.False(status.Last!.Verified);
    }

    [Fact]
    public async Task Nightly_backup_is_due_once_per_scheduled_time()
    {
        await using var factory = new ApiFactory();
        factory.Settings["Backup:PgDump"] = "/nonexistent/pg_dump"; // the run fails fast; only the schedule matters here
        factory.Settings["Backup:Directory"] = Path.Combine(Path.GetTempPath(), "lugat-backup-test", Guid.NewGuid().ToString("N"));
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 10, 5, 3, 30, 0, TimeSpan.FromHours(9)).ToUniversalTime());
        await factory.SignedInClientAsync(ApiFactory.UserA);

        await using var scope = factory.Services.CreateAsyncScope();
        var backups = scope.ServiceProvider.GetRequiredService<BackupService>();
        Assert.True(await backups.IsDueAsync(CancellationToken.None)); // yesterday's 04:00 has not run
        await backups.RunAsync(CancellationToken.None);
        Assert.False(await backups.IsDueAsync(CancellationToken.None));

        factory.Time.Advance(TimeSpan.FromMinutes(40)); // 04:10 KST
        Assert.True(await backups.IsDueAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_folder_that_cannot_be_created_is_a_recorded_failure()
    {
        var blocker = Path.Combine(Path.GetTempPath(), "lugat-backup-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);
        await File.WriteAllTextAsync(blocker, "a file where the folder should be");
        await using var factory = new ApiFactory();
        factory.Settings["Backup:Directory"] = Path.Combine(blocker, "backups");
        await factory.SignedInClientAsync(ApiFactory.UserA);

        await using var scope = factory.Services.CreateAsyncScope();
        var run = await scope.ServiceProvider.GetRequiredService<BackupService>().RunAsync(CancellationToken.None);

        Assert.False(run.Verified);
        Assert.NotNull(run.Error);
        Assert.NotNull(run.FinishedAt);
        File.Delete(blocker);
    }

    /// <summary>A verified run whose file is on disk, without running pg_dump (the Telegram part is tested on its own).</summary>
    private static async Task<BackupRun> SeedVerifiedAsync(ApiFactory factory, string folder, string name = "lugat-20261004-040000.dump")
    {
        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, name), [1, 2, 3, 4]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = new BackupRun
        {
            Id = Guid.NewGuid(), StartedAt = factory.Time.GetUtcNow(), FinishedAt = factory.Time.GetUtcNow(), FileName = name, SizeBytes = 4, Verified = true,
        };
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    [Fact]
    public async Task The_newest_verified_copy_goes_to_the_owner_on_telegram_once_a_week()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lugat-backup-test", Guid.NewGuid().ToString("N"));
        var telegram = new FakeMessenger();
        await using var factory = new ApiFactory { Messenger = telegram };
        factory.Settings["Backup:Directory"] = folder;
        factory.Settings["Backup:TelegramChatId"] = ApiFactory.UserA.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await SeedVerifiedAsync(factory, folder);

        await using var scope = factory.Services.CreateAsyncScope();
        var backups = scope.ServiceProvider.GetRequiredService<BackupService>();
        Assert.True(await backups.IsTelegramDueAsync(CancellationToken.None));
        var (run, error) = await backups.SendToTelegramAsync(CancellationToken.None);
        var status = (await client.GetFromJsonAsync<BackupStatusDto>("/api/backups/status", Json))!;

        Assert.Null(error);
        var (chat, file, content, caption) = Assert.Single(telegram.Documents);
        Assert.Equal((ApiFactory.UserA, "lugat-20261004-040000.dump"), (chat, file));
        Assert.Equal([1, 2, 3, 4], content);
        Assert.Contains("tiklab tekshirilgan", caption);
        Assert.Contains("https://github.com/JohnnySenior/LearnKoreanWordsEasly/blob/main/docs/HOSTING.md", caption);
        Assert.DoesNotContain(" docs/HOSTING.md", caption); // Telegram links a bare "HOSTING.md" to the site hosting.md
        Assert.NotNull(run!.SentToTelegramAt);
        Assert.True(status.TelegramReady);
        Assert.Equal("…1001", status.TelegramRecipient);
        Assert.NotNull(status.LastSentToTelegramAt);
        Assert.False(await backups.IsTelegramDueAsync(CancellationToken.None));

        factory.Time.Advance(TimeSpan.FromDays(7));
        Assert.True(await backups.IsTelegramDueAsync(CancellationToken.None));
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task A_bundle_too_big_for_telegram_falls_back_to_the_database_alone()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lugat-backup-test", Guid.NewGuid().ToString("N"));
        var telegram = new FakeMessenger();
        await using var factory = new ApiFactory { Messenger = telegram };
        factory.Settings["Backup:Directory"] = folder;
        factory.Settings["Backup:TelegramMaxBytes"] = "16"; // stands in for Telegram's 50 MB
        factory.Settings["Backup:TelegramChatId"] = "777000";
        await factory.SignedInClientAsync(ApiFactory.UserA);
        await SeedVerifiedAsync(factory, folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, "lugat-20261004-040000.dump.zip"), new byte[32]); // database + manifest

        await using var scope = factory.Services.CreateAsyncScope();
        var (run, error) = await scope.ServiceProvider.GetRequiredService<BackupService>().SendToTelegramAsync(CancellationToken.None);

        Assert.Null(error);
        var (_, file, content, caption) = Assert.Single(telegram.Documents);
        Assert.Equal("lugat-20261004-040000.dump", file);
        Assert.Equal([1, 2, 3, 4], content);
        Assert.NotNull(run!.SentToTelegramAt);
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task A_refused_send_is_shown_and_retried_after_a_pause_and_the_recipient_can_be_set()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lugat-backup-test", Guid.NewGuid().ToString("N"));
        var telegram = new FakeMessenger { NextOutcome = SendOutcome.Blocked };
        await using var factory = new ApiFactory { Messenger = telegram };
        factory.Settings["Backup:Directory"] = folder;
        factory.Settings["Backup:TelegramChatId"] = "777000";
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await SeedVerifiedAsync(factory, folder);

        await using var scope = factory.Services.CreateAsyncScope();
        var backups = scope.ServiceProvider.GetRequiredService<BackupService>();
        var (_, error) = await backups.SendToTelegramAsync(CancellationToken.None);
        var status = (await client.GetFromJsonAsync<BackupStatusDto>("/api/backups/status", Json))!;
        var dueRightAway = await backups.IsTelegramDueAsync(CancellationToken.None);
        factory.Time.Advance(TimeSpan.FromHours(7));
        var dueLater = await backups.IsTelegramDueAsync(CancellationToken.None);
        telegram.NextOutcome = SendOutcome.Sent;
        await backups.SendToTelegramAsync(CancellationToken.None);

        Assert.Contains("/start", error);
        Assert.Equal("…7000", status.TelegramRecipient);
        Assert.Null(status.LastSentToTelegramAt);
        Assert.Contains("/start", status.TelegramError);
        Assert.False(dueRightAway);
        Assert.True(dueLater);
        Assert.Equal(777000, Assert.Single(telegram.Documents).ChatId);
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task Without_a_configured_recipient_nothing_goes_to_telegram()
    {
        // The Mini App allow-list is gone (spec §3.1), so its first user is no longer a fallback owner.
        var folder = Path.Combine(Path.GetTempPath(), "lugat-backup-test", Guid.NewGuid().ToString("N"));
        var telegram = new FakeMessenger();
        await using var factory = new ApiFactory { Messenger = telegram };
        factory.Settings["Backup:Directory"] = folder;
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await SeedVerifiedAsync(factory, folder);

        await using var scope = factory.Services.CreateAsyncScope();
        var backups = scope.ServiceProvider.GetRequiredService<BackupService>();
        var status = (await client.GetFromJsonAsync<BackupStatusDto>("/api/backups/status", Json))!;

        Assert.False(status.TelegramReady);
        Assert.False(await backups.IsTelegramDueAsync(CancellationToken.None));
        Assert.NotNull((await backups.SendToTelegramAsync(CancellationToken.None)).Error);
        Assert.Empty(telegram.Documents);
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task Nothing_is_sent_without_a_verified_copy()
    {
        var telegram = new FakeMessenger();
        await using var factory = new ApiFactory { Messenger = telegram };
        factory.Settings["Backup:TelegramChatId"] = "777000";
        await factory.SignedInClientAsync(ApiFactory.UserA);

        await using var scope = factory.Services.CreateAsyncScope();
        var backups = scope.ServiceProvider.GetRequiredService<BackupService>();

        Assert.False(await backups.IsTelegramDueAsync(CancellationToken.None));
        Assert.Contains("tekshirilgan", (await backups.SendToTelegramAsync(CancellationToken.None)).Error);
        Assert.Empty(telegram.Documents);
    }
}
