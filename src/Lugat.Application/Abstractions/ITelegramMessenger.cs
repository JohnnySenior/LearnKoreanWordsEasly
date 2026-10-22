namespace Lugat.Application.Abstractions;

public enum SendOutcome
{
    Sent,

    /// <summary>The bot may not write to this user: never started, or blocked (spec NOT02).</summary>
    Blocked,

    /// <summary>Temporary refusal (rate limit, server error); safe to try again later.</summary>
    Retryable,

    /// <summary>Permanent refusal for another reason.</summary>
    Failed,

    /// <summary>No clear answer (timeout, dropped connection): the message may have arrived (spec NOT03).</summary>
    Uncertain,
}

public sealed record SendResult(SendOutcome Outcome, long? MessageId = null, string? Error = null, TimeSpan? RetryAfter = null);

/// <summary>Sends bot messages with a button that opens the Mini App (spec NOT02: tapping a reminder opens practice).</summary>
public interface ITelegramMessenger
{
    /// <summary>False when no bot token is configured; reminders are then not attempted at all.</summary>
    bool IsConfigured { get; }

    Task<SendResult> SendAsync(long chatId, string text, string openAppButton, CancellationToken ct);

    /// <summary>A file to the chat (Bot API sendDocument, up to 50 MB), e.g. the weekly backup (D-37).</summary>
    Task<SendResult> SendDocumentAsync(long chatId, string fileName, byte[] content, string caption, CancellationToken ct);
}
