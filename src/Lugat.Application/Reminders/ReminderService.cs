using Lugat.Application.Abstractions;
using Lugat.Application.Study;
using Lugat.Contracts;
using Lugat.Domain.Reminders;
using Lugat.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lugat.Application.Reminders;

/// <summary>How a reminder reached the learner (L-63).</summary>
public enum ReminderChannel { None, Push, Telegram }

public sealed record ReminderSendResult(SendResult Result, ReminderChannel Channel);

/// <summary>
/// Practice reminders (spec NOT01–NOT04) by Web Push or the Telegram bot. Runs every minute from a background worker.
/// Every occurrence is claimed in the outbox before sending, so a restart or a second worker cannot send it twice
/// (spec AC10), and a reminder that is more than 30 minutes late is simply not sent (spec NOT03).
/// One occurrence goes out once (L-63): to the learner's push devices when they have any, and through the bot only
/// when no device took it (none subscribed, or every one failed).
/// </summary>
public sealed class ReminderService(
    IAppDbContext db,
    ITelegramMessenger telegram,
    IWebPushSender push,
    StudyService study,
    TimeProvider time,
    ILogger<ReminderService> logger)
{
    public const string OpenAppButton = "Takrorlashni ochish";

    /// <summary>The app page a reminder opens (spec §8: "Havola ilovani takrorlash sahifasida ochadi").</summary>
    public const string ReviewPath = "takrorlash";

    /// <summary>The Premium page, for the trial-end note (L-64).</summary>
    public const string PremiumPath = "premium";

    /// <summary>Same tag on every reminder: on one device a newer one replaces the older (L-63).</summary>
    public const string PushTag = "lugat-reminder";

    /// <summary>L-64: the trial-end note goes this long before the trial ends, and not in its last hour.</summary>
    public static readonly TimeSpan TrialEndNotice = TimeSpan.FromHours(24);

    public const string TrialEndText = "Premium sinov muddati ertaga tugaydi. Davom ettirmoqchi bo‘lsangiz, Premium sahifasida to‘lov yo‘li bor.";

    public async Task<int> RunDueAsync(CancellationToken ct)
    {
        if (!telegram.IsConfigured && !push.IsConfigured)
        {
            return 0;
        }

        var now = time.GetUtcNow();
        var withTelegram = telegram.IsConfigured;
        var withPush = push.IsConfigured;
        // A learner can be reached through the bot (a Telegram login) or on a subscribed device (L-63).
        var users = await (from u in db.Users
                           where u.RemindersEnabled && u.DeletedAt == null
                           let chat = db.UserLogins.Where(l => l.UserId == u.Id && l.Provider == LoginProvider.Telegram).Select(l => l.ProviderUserId).FirstOrDefault()
                           where withTelegram && chat != null || withPush && db.PushSubscriptions.Any(p => p.UserId == u.Id)
                           select new { User = u, Chat = withTelegram ? chat : null }).ToListAsync(ct);
        var sent = 0;
        foreach (var x in users)
        {
            var occurrence = ReminderPlanner.Due(x.User.Id, now, x.User.Zone(), x.User.ReminderPreferences());
            if (occurrence is not null && await DeliverAsync(x.User, x.Chat is null ? null : ChatId(x.Chat), occurrence, now, ct))
            {
                sent++;
            }
        }

        // L-64: a trial ending within a day, with nothing paid after it, gets one calm note.
        var noticeFrom = now + TimeSpan.FromHours(1);
        var noticeTo = now + TrialEndNotice;
        var trials = await db.Subscriptions
            .Where(s => s.Plan == Domain.Premium.SubscriptionPlan.Trial && s.StartsAt <= now && s.EndsAt > noticeFrom && s.EndsAt <= noticeTo
                && !db.Subscriptions.Any(o => o.UserId == s.UserId && o.EndsAt > s.EndsAt))
            .Select(s => new { s.Id, s.UserId, s.EndsAt }).ToListAsync(ct);
        foreach (var trial in trials)
        {
            if (users.FirstOrDefault(u => u.User.Id == trial.UserId) is { } x
                && await DeliverTrialEndAsync(x.User, x.Chat is null ? null : ChatId(x.Chat), trial.Id, trial.EndsAt - TrialEndNotice, now, ct))
            {
                sent++;
            }
        }

        // L-71: a teacher's deck due within a day, not done yet, gets one calm note — what is left, nothing more.
        var decks = await db.GroupDecks.AsNoTracking()
            .Where(d => d.Status == Domain.Teaching.GroupDeckStatus.Published && d.DueAt > noticeFrom && d.DueAt <= noticeTo)
            .Select(d => new { d.Id, d.Name, d.DueAt }).ToListAsync(ct);
        foreach (var deck in decks)
        {
            var learners = await db.PersonalWords.Where(p => p.GroupDeckId == deck.Id).Select(p => p.OwnerId).Distinct().ToListAsync(ct);
            var reachable = users.Where(u => learners.Contains(u.User.Id)).ToList();
            if (reachable.Count == 0)
            {
                continue;
            }

            var done = await Teaching.GroupDeckReport.DoneAsync(db, deck.Id, reachable.Select(u => u.User.Id).ToList(), now, ct);
            foreach (var x in reachable.Where(x => !done[x.User.Id].Done))
            {
                var left = done[x.User.Id];
                if (await DeliverOnceAsync(x.User, x.Chat is null ? null : ChatId(x.Chat), $"deck-due:{deck.Id:N}:{x.User.Id:N}", ReminderSlot.DeckDue,
                        deck.DueAt!.Value - TrialEndNotice, DeckDueText(deck.Name, left), $"sozlarim/guruh/{deck.Id}", now, ct))
                {
                    sent++;
                }
            }
        }

        return sent;
    }

    /// <summary>«1-dars» muddati ertaga: what is left, in one calm sentence (spec §1.4: no pressure, no blame).</summary>
    public static string DeckDueText(string deck, DeckDoneDto left)
    {
        var parts = new List<string>();
        if (left.Seen < left.Words)
        {
            parts.Add($"{left.Words - left.Seen} ta so‘z");
        }

        if (!left.TestTaken)
        {
            parts.Add("sinov");
        }

        return $"«{deck}» lug‘ati muddati ertaga. Qoldi: {string.Join(" va ", parts)}.";
    }

    private Task<bool> DeliverTrialEndAsync(User user, long? chatId, Guid trialId, DateTimeOffset scheduledAt, DateTimeOffset now, CancellationToken ct) =>
        DeliverOnceAsync(user, chatId, $"trial-end:{trialId:N}", ReminderSlot.TrialEnd, scheduledAt, TrialEndText, PremiumPath, now, ct);

    /// <summary>
    /// L-64: sent once per trial (its own outbox key), by the same channel rule as reminders; never retried, since a
    /// missed note about a trial is not worth a second message. L-71: the deck due note goes the same way, once per
    /// learner and deck.
    /// </summary>
    private async Task<bool> DeliverOnceAsync(User user, long? chatId, string key, ReminderSlot slot, DateTimeOffset scheduledAt, string text, string path,
        DateTimeOffset now, CancellationToken ct)
    {
        var delivery = new ReminderDelivery
        {
            Id = Guid.CreateVersion7(now),
            UserId = user.Id,
            OccurrenceKey = key,
            Slot = slot,
            ScheduledAt = scheduledAt,
            Status = DeliveryStatus.Sending,
            CreatedAt = now,
        };
        if (await db.ReminderDeliveries.AnyAsync(d => d.OccurrenceKey == delivery.OccurrenceKey, ct))
        {
            return false;
        }

        db.ReminderDeliveries.Add(delivery);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.DiscardChanges();
            return false;
        }

        var result = (await SendAsync(user, chatId, text, ct, path)).Result;
        delivery.Attempts = 1;
        (delivery.Status, delivery.SentAt) = result.Outcome switch
        {
            SendOutcome.Sent => (DeliveryStatus.Sent, now),
            SendOutcome.Blocked => (DeliveryStatus.Blocked, (DateTimeOffset?)null),
            SendOutcome.Uncertain => (DeliveryStatus.Uncertain, null),
            _ => (DeliveryStatus.Failed, null),
        };
        delivery.ProviderMessageId = result.MessageId;
        delivery.Error = result.Error;
        await db.SaveChangesAsync(ct);
        return result.Outcome == SendOutcome.Sent;
    }

    /// <summary>A one-off message, so the learner can check that reminders reach them (spec NOT02), by the same rule.</summary>
    public async Task<ReminderSendResult> SendTestAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        var chat = telegram.IsConfigured
            ? await db.UserLogins.Where(l => l.UserId == userId && l.Provider == LoginProvider.Telegram).Select(l => l.ProviderUserId).FirstOrDefaultAsync(ct)
            : null;
        return await SendAsync(user, chat is null ? null : ChatId(chat), "Sinov xabari: eslatmalar shu yerga keladi.", ct);
    }

    /// <summary>
    /// Push to every subscribed device first; a device that is gone (404/410) is deleted. The bot writes only when no
    /// device took the message, so a learner with both never gets the same reminder twice.
    /// </summary>
    private async Task<ReminderSendResult> SendAsync(User user, long? chatId, string text, CancellationToken ct, string path = ReviewPath)
    {
        if (push.IsConfigured)
        {
            var devices = await db.PushSubscriptions.Where(p => p.UserId == user.Id).ToListAsync(ct);
            var delivered = false;
            foreach (var device in devices)
            {
                var outcome = await push.SendAsync(new PushTarget(device.Endpoint, device.P256dh, device.Auth),
                    new PushNotification("Koreyscha", text, "/" + path, PushTag), ct);
                switch (outcome)
                {
                    case PushOutcome.Sent:
                        (device.LastSuccessAt, device.FailedAt, delivered) = (time.GetUtcNow(), null, true);
                        break;
                    case PushOutcome.Gone:
                        db.PushSubscriptions.Remove(device);
                        break;
                    default:
                        device.FailedAt = time.GetUtcNow();
                        break;
                }
            }

            await db.SaveChangesAsync(ct);
            if (delivered)
            {
                return new(new SendResult(SendOutcome.Sent), ReminderChannel.Push);
            }
        }

        if (chatId is { } chat && telegram.IsConfigured)
        {
            var result = await telegram.SendAsync(chat, text, path == ReviewPath ? OpenAppButton : path == PremiumPath ? "Premium sahifasini ochish" : "Lug‘atni ochish", ct, path);
            await RecordBotAccessAsync(user, result, ct);
            return new(result, ReminderChannel.Telegram);
        }

        return new(new SendResult(SendOutcome.Failed, Error: "Eslatma kanali yo‘q yoki qurilma javob bermadi."), ReminderChannel.None);
    }

    /// <summary>A bot's private chat with a user has the user's Telegram id.</summary>
    private static long ChatId(string telegramUserId) => long.Parse(telegramUserId, System.Globalization.CultureInfo.InvariantCulture);

    private async Task<bool> DeliverAsync(User user, long? chatId, ReminderOccurrence occurrence, DateTimeOffset now, CancellationToken ct)
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

        var result = (await SendAsync(user, chatId, text, ct)).Result;
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
        await db.SaveChangesAsync(ct);
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
