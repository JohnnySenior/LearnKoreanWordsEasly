using KoreanAiTeacher.Application.Abstractions;
using KoreanAiTeacher.Application.Study;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Domain.Lessons;
using KoreanAiTeacher.Domain.Reminders;
using KoreanAiTeacher.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KoreanAiTeacher.Application.Reminders;

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
            var lessonDays = await LessonDaysAsync(user.Id, zone, now, ct);
            var occurrence = ReminderPlanner.Due(user.Id, now, zone, user.ReminderPreferences(), lessonDays.Contains);
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
        var text = Compose(occurrence, today, user.Zone(), Exams.ExamService.DaysLeft(user, now));
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

    private async Task<HashSet<DateOnly>> LessonDaysAsync(Guid userId, TimeZoneInfo zone, DateTimeOffset now, CancellationToken ct)
    {
        var from = now.AddDays(-2);
        var to = now.AddDays(2);
        var dues = await db.LessonAssignments
            .Where(a => a.OwnerId == userId && a.Status == AssignmentStatus.Planned && a.DueAt >= from && a.DueAt <= to)
            .Select(a => a.DueAt).ToListAsync(ct);
        return dues.Select(d => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(d, zone).DateTime)).ToHashSet();
    }

    /// <summary>
    /// Short and polite: what is ready and roughly how long it takes; no blame or pressure (spec NOT04).
    /// Returns null when there is nothing to practise, so no empty reminder is sent.
    /// </summary>
    public static string? Compose(ReminderOccurrence occurrence, TodayDto today, TimeZoneInfo zone, int? examDaysLeft = null)
    {
        var cards = today.DueNow + today.NewAvailable;
        var minutes = Math.Max(1, (int)Math.Ceiling(Math.Min(cards, 200) * today.SecondsPerCard / 60.0));
        var ready = today.NewAvailable > 0
            ? $"{today.DueNow} ta takrorlash va {today.NewAvailable} ta yangi kartochka"
            : $"{today.DueNow} ta takrorlash";

        if (occurrence.Slot == ReminderSlot.SaturdayLesson && today.NextLesson is { } lesson)
        {
            var at = TimeZoneInfo.ConvertTime(lesson.DueAt, zone);
            var extra = today.DueNow > 0 ? $" Xohlasangiz, qisqa takrorlash tayyor: {today.DueNow} ta kartochka." : "";
            return $"Bugun soat {at:HH\\:mm} da dars: {lesson.Title}.{extra}";
        }

        if (cards == 0)
        {
            return null;
        }

        var lessonLine = today.NextLesson is { } next
            ? $"\nKeyingi dars: {next.Title}, {Uz.Day(TimeZoneInfo.ConvertTime(next.DueAt, zone))}."
            : "";
        // A calm countdown, not pressure (NOT04, D-34).
        var examLine = examDaysLeft switch
        {
            null => "",
            0 => "\nBugun imtihon. Omad! 화이팅!",
            1 => "\nImtihon ertaga.",
            { } d => $"\nImtihongacha {d} kun.",
        };
        return $"Bugungi mashq tayyor: {ready}. Taxminan {minutes} daqiqa.{lessonLine}{examLine}";
    }

    private static class Uz
    {
        private static readonly string[] Days = ["yakshanba", "dushanba", "seshanba", "chorshanba", "payshanba", "juma", "shanba"];

        public static string Day(DateTimeOffset local) => $"{Days[(int)local.DayOfWeek]} {local:HH\\:mm}";
    }
}
