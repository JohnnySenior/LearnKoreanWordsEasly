using Lugat.Application.Abstractions;
using Lugat.Application.Study;
using Lugat.Contracts;
using Lugat.Domain.Reminders;
using Lugat.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lugat.Application.Reminders;

/// <summary>
/// Telegram practice reminders (spec NOT01–NOT04). Runs every minute from a background worker.
/// Every occurrence is claimed in the outbox before sending, so a restart or a second worker cannot send it twice
/// (spec AC10), and a reminder that is more than 30 minutes late is simply not sent (spec NOT03).
/// </summary>
public sealed class ReminderService(
    IAppDbContext db,
    ITelegramMessenger telegram,
    StudyService study,
    TimeProvider time,
    ILogger<ReminderService> logger)
{
    public const string OpenAppButton = "Takrorlashni ochish";

    /// <summary>The app page a reminder opens (spec §8: "Havola ilovani takrorlash sahifasida ochadi").</summary>
    public const string ReviewPath = "takrorlash";

    public async Task<int> RunDueAsync(CancellationToken ct)
    {
        if (!telegram.IsConfigured)
        {
            return 0;
        }

        var now = time.GetUtcNow();
        // Reminders go through the bot, so only learners who sign in with Telegram can get them (spec §8).
        var users = await (from u in db.Users
                           join l in db.UserLogins on u.Id equals l.UserId
                           where u.RemindersEnabled && u.DeletedAt == null && l.Provider == LoginProvider.Telegram
                           select new { User = u, l.ProviderUserId }).ToListAsync(ct);
        var sent = 0;
        foreach (var (user, chat) in users.Select(x => (x.User, ChatId(x.ProviderUserId))))
        {
            var zone = user.Zone();
            var occurrence = ReminderPlanner.Due(user.Id, now, zone, user.ReminderPreferences());
            if (occurrence is not null && await DeliverAsync(user, chat, occurrence, now, ct))
            {
                sent++;
            }
        }

        return sent;
    }

    /// <summary>Sends a one-off message so the learner can check that the bot may write to them (spec NOT02).</summary>
    public async Task<SendResult> SendTestAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        if (!telegram.IsConfigured)
        {
            return new SendResult(SendOutcome.Failed, Error: "Bot sozlanmagan.");
        }

        var login = await db.UserLogins.Where(l => l.UserId == userId && l.Provider == LoginProvider.Telegram)
            .Select(l => l.ProviderUserId).FirstOrDefaultAsync(ct);
        if (login is null)
        {
            return new SendResult(SendOutcome.Failed, Error: "Telegram hisobi bog‘lanmagan.");
        }

        var result = await telegram.SendAsync(ChatId(login), "Sinov xabari: eslatmalar shu yerga keladi.", OpenAppButton, ct, ReviewPath);
        await RecordBotAccessAsync(user, result, ct);
        return result;
    }

    /// <summary>A bot's private chat with a user has the user's Telegram id.</summary>
    private static long ChatId(string telegramUserId) => long.Parse(telegramUserId, System.Globalization.CultureInfo.InvariantCulture);

    private async Task<bool> DeliverAsync(User user, long chatId, ReminderOccurrence occurrence, DateTimeOffset now, CancellationToken ct)
    {
        var delivery = await ClaimAsync(user, occurrence, now, ct);
        if (delivery is null)
        {
            return false;
        }

        var today = await study.TodayAsync(user.Id, ct);
        var text = Compose(await study.DueWordsAsync(user.Id, ct), today.DueNow, today.SecondsPerCard);
        if (text is null)
        {
            delivery.Status = DeliveryStatus.Skipped;
            delivery.Error = "Muddati kelgan kartochka yo‘q.";
            await db.SaveChangesAsync(ct);
            return false;
        }

        var result = await telegram.SendAsync(chatId, text, OpenAppButton, ct, ReviewPath);
        delivery.Attempts++;
        switch (result.Outcome)
        {
            case SendOutcome.Sent:
                delivery.Status = DeliveryStatus.Sent;
                delivery.SentAt = now;
                delivery.ProviderMessageId = result.MessageId;
                break;
            case SendOutcome.Blocked:
                delivery.Status = DeliveryStatus.Blocked;
                break;
            case SendOutcome.Retryable when now + (result.RetryAfter ?? TimeSpan.FromMinutes(1)) - occurrence.ScheduledAt <= ReminderPlanner.MaxDelay:
                delivery.Status = DeliveryStatus.RetryPending;
                delivery.NextRetryAt = now + (result.RetryAfter ?? TimeSpan.FromMinutes(1));
                break;
            case SendOutcome.Uncertain:
                delivery.Status = DeliveryStatus.Uncertain;
                logger.LogWarning("Reminder {Key}: delivery outcome unknown ({Error}); not resending", occurrence.Key, result.Error);
                break;
            default:
                delivery.Status = DeliveryStatus.Failed;
                break;
        }

        delivery.Error = result.Error;
        await RecordBotAccessAsync(user, result, ct);
        return result.Outcome == SendOutcome.Sent;
    }

    /// <summary>
    /// Creates the outbox row for this occurrence, or takes over a row waiting for a retry.
    /// Returns null when the occurrence was already handled.
    /// </summary>
    private async Task<ReminderDelivery?> ClaimAsync(User user, ReminderOccurrence occurrence, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.ReminderDeliveries.SingleOrDefaultAsync(d => d.OccurrenceKey == occurrence.Key, ct);
        if (existing is not null)
        {
            if (existing.Status != DeliveryStatus.RetryPending || existing.NextRetryAt > now)
            {
                return null;
            }

            existing.Status = DeliveryStatus.Sending;
            existing.NextRetryAt = null;
            try
            {
                await db.SaveChangesAsync(ct);
                return existing;
            }
            catch (DbUpdateConcurrencyException)
            {
                db.DiscardChanges();
                return null;
            }
        }

        var delivery = new ReminderDelivery
        {
            Id = Guid.CreateVersion7(now),
            UserId = user.Id,
            OccurrenceKey = occurrence.Key,
            Slot = occurrence.Slot,
            ScheduledAt = occurrence.ScheduledAt,
            Status = DeliveryStatus.Sending,
            CreatedAt = now,
        };
        db.ReminderDeliveries.Add(delivery);
        try
        {
            await db.SaveChangesAsync(ct);
            return delivery;
        }
        catch (DbUpdateException)
        {
            // Another worker claimed the same occurrence first.
            db.DiscardChanges();
            return null;
        }
    }

    private async Task RecordBotAccessAsync(User user, SendResult result, CancellationToken ct)
    {
        var tracked = await db.Users.SingleAsync(u => u.Id == user.Id, ct);
        tracked.BotBlockedAt = result.Outcome switch
        {
            SendOutcome.Blocked => tracked.BotBlockedAt ?? time.GetUtcNow(),
            SendOutcome.Sent => null,
            _ => tracked.BotBlockedAt,
        };
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Spec §8: "Bugun N ta so‘zni takrorlash vaqti. ~M daqiqa." Short and polite, no blame or pressure (spec NOT04).
    /// Only cards that are due count: new words alone are no reason to write, so then nothing is sent (null).
    /// </summary>
    public static string? Compose(int dueWords, int dueCards, int secondsPerCard)
    {
        if (dueWords == 0 || dueCards == 0)
        {
            return null;
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(Math.Min(dueCards, 200) * secondsPerCard / 60.0));
        return $"Bugun {dueWords} ta so‘zni takrorlash vaqti. ~{minutes} daqiqa.";
    }
}
