using Lugat.Infrastructure.Backups;

namespace Lugat.Application.Tests.Backups;

/// <summary>
/// Local backup retention against the privacy policy's "deleted data leaves the backups within a month" (audit
/// 2026-10-26): 7 daily and 4 weekly copies, none older than 30 days, the newest always kept.
/// </summary>
public class BackupRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 19, 0, 0, TimeSpan.Zero); // 04:00 KST next day

    private static List<(string Name, DateTimeOffset WrittenAt)> Nightly(int days, int skipNewest = 0) =>
        Enumerable.Range(skipNewest, days).Select(d => Now.AddDays(-d)).Select(at => ($"lugat-{at:yyyyMMdd-HHmmss}.dump", at)).ToList();

    [Fact]
    public void Sixty_nights_keep_the_week_and_weekly_copies_but_nothing_past_a_month()
    {
        var files = Nightly(60);

        var kept = BackupService.Retain(files, Now, new BackupOptions()).ToHashSet();
        var ages = files.Where(f => kept.Contains(f.Name)).Select(f => (Now - f.WrittenAt).TotalDays).ToList();

        Assert.All(ages, age => Assert.True(age <= 30, $"{age} days"));
        Assert.Equal(7, ages.Count(a => a < 7)); // the last seven nights
        Assert.InRange(kept.Count, 9, 11); // and one per earlier week
    }

    [Fact]
    public void When_backups_stopped_long_ago_only_the_newest_copy_stays()
    {
        var files = Nightly(20, skipNewest: 40); // the last copy is 40 days old

        var kept = BackupService.Retain(files, Now, new BackupOptions()).ToList();

        Assert.Equal([files[0].Name], kept);
    }

    [Fact]
    public void The_month_can_be_set_but_the_newest_copy_is_never_dropped()
    {
        var files = Nightly(10);

        var kept = BackupService.Retain(files, Now, new BackupOptions { MaxAgeDays = 0 }).ToList();

        Assert.Equal([files[0].Name], kept);
    }
}
