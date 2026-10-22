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
    public const string OpenAppButton = "Mashqni ochish";

    public async Task<int> RunDueAsync(CancellationToken ct)
    {
        if (!telegram.IsConfigured)
        {
            return 0;
        }

        var now = time.GetUtcNow();
        var users = await db.Users.Where(u => u.RemindersEnabled).ToListAsync(ct);
        var sent = 0;
        foreach (var user in users)
        {
            var zone = user.Zone();
            var occurrence = ReminderPlanner.Due(user.Id, now, zone, user.ReminderPreferences());
            if (occurrence is not null && await DeliverAsync(user, occurrence, now, ct))
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

        var result = await telegram.SendAsync(user.TelegramUserId, "Sinov xabari: eslatmalar shu yerga keladi.", OpenAppButton, ct);
        await RecordBotAccessAsync(user, result, ct);
        return result;
    }

    private async Task<bool> DeliverAsync(User user, ReminderOccurrence occurrence, DateTimeOffset now, CancellationToken ct)
    {
        var delivery = await ClaimAsync(user, occurrence, now, ct);
        if (delivery is null)
        {
            return false;
        }

        var today = await study.TodayAsync(user.Id, ct);
        var text = Compose(today);
        if (text is null)
        {
            delivery.Status = DeliveryStatus.Skipped;
            delivery.Error = "Mashq qilinadigan kartochka yo‘q.";
            await db.SaveChangesAsync(ct);
            return false;
        }

        var result = await telegram.SendAsync(user.TelegramUserId, text, OpenAppButton, ct);
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
    /// Short and polite: what is ready and roughly how long it takes; no blame or pressure (spec NOT04).
    /// Returns null when there is nothing to practise, so no empty reminder is sent.
    /// </summary>
    public static string? Compose(TodayDto today)
    {
        var cards = today.DueNow + today.NewAvailable;
        if (cards == 0)
        {
            return null;
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(Math.Min(cards, 200) * today.SecondsPerCard / 60.0));
        var ready = today.NewAvailable > 0
            ? $"{today.DueNow} ta takrorlash va {today.NewAvailable} ta yangi kartochka"
            : $"{today.DueNow} ta takrorlash";
        return $"Bugungi mashq tayyor: {ready}. Taxminan {minutes} daqiqa.";
    }
}
