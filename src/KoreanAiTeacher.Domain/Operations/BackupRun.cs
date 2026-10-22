namespace KoreanAiTeacher.Domain.Operations;

/// <summary>
/// One nightly database backup and its restore check (spec REL04, D-22).
/// A backup counts only when its restore into a scratch database succeeded.
/// </summary>
public sealed class BackupRun
{
    public Guid Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>File name inside the backup folder, e.g. kat-20261001-040000.dump.</summary>
    public string? FileName { get; set; }

    public long SizeBytes { get; set; }

    /// <summary>The dump was restored into a scratch database and matched the live schema.</summary>
    public bool Verified { get; set; }

    /// <summary>Rows per table in the restored copy, for a quick look (JSON).</summary>
    public string? RestoredCounts { get; set; }

    public string? Error { get; set; }

    /// <summary>When this copy reached the owner's Telegram chat (D-37: the copy outside Railway).</summary>
    public DateTimeOffset? SentToTelegramAt { get; set; }

    /// <summary>Last attempt to send it, successful or not; failures are retried after a pause.</summary>
    public DateTimeOffset? TelegramAttemptAt { get; set; }

    public string? TelegramError { get; set; }
}
