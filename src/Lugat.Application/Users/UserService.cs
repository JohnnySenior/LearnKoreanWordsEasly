using System.Globalization;
using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain;
using Lugat.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lugat.Application.Users;

/// <summary>A sign-in proven by its provider: Telegram's hash or Google's signed ID token checked out.</summary>
/// <param name="Display">Telegram username or Google email, only for showing which account is linked.</param>
public sealed record VerifiedLogin(LoginProvider Provider, string ProviderUserId, string? DisplayName, string? Display);

/// <summary>
/// The first administrators, by their login (L-14). The admin panel (stage 7) gives roles afterwards; this only
/// makes sure the owner can reach the admin endpoints before that panel exists. A listed login gains Admin when it
/// signs in; a role is never taken away here.
/// </summary>
public sealed class AdminOptions
{
    public const string SectionName = "Auth:Admins";

    public long[] TelegramUserIds { get; set; } = [];

    /// <summary>Google <c>sub</c> values (stable, unlike email addresses).</summary>
    public string[] GoogleSubjects { get; set; } = [];

    public bool IsAdmin(VerifiedLogin login) => login.Provider switch
    {
        LoginProvider.Telegram => long.TryParse(login.ProviderUserId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && TelegramUserIds.Contains(id),
        LoginProvider.Google => GoogleSubjects.Contains(login.ProviderUserId, StringComparer.Ordinal),
        _ => false,
    };
}

/// <summary>What a request's token is checked against on every call (L-13).</summary>
/// <param name="LastSeenAt">When the user was last marked active (<see cref="UserService.NeedsTouch"/>).</param>
public sealed record SessionCheck(int SessionVersion, UserRoles Roles, DateTimeOffset LastSeenAt);

public sealed class UserService(IAppDbContext db, TimeProvider time, IOperationLock operations, IOptionsMonitor<AdminOptions> admins, IOptionsMonitor<Premium.PremiumOptions> premium)
{
    public const int MaxDisplayName = 100;

    /// <summary>
    /// Finds the user of this login or creates one (spec §6.3). Serialized per login, and the login is unique per
    /// provider, so the same Telegram or Google account never makes two users (spec §13.2).
    /// </summary>
    public async Task<User> SignInAsync(VerifiedLogin login, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync(LockKey(login), ct);
        var now = time.GetUtcNow();
        var existing = await db.UserLogins.SingleOrDefaultAsync(l => l.Provider == login.Provider && l.ProviderUserId == login.ProviderUserId, ct);
        User user;
        if (existing is null)
        {
            user = new User
            {
                Id = Guid.CreateVersion7(now),
                DisplayName = Cut(login.DisplayName, MaxDisplayName),
                CreatedAt = now,
            };
            db.Users.Add(user);
            existing = NewLogin(user.Id, login, now);
            db.UserLogins.Add(existing);
            if (premium.CurrentValue.TrialDays is > 0 and var days)
            {
                // A trial for a new learner, when Johnny turns it on (spec §7.2, Q3).
                db.Subscriptions.Add(new Domain.Premium.Subscription
                {
                    Id = Guid.CreateVersion7(now),
                    UserId = user.Id,
                    Plan = Domain.Premium.SubscriptionPlan.Trial,
                    StartsAt = now,
                    EndsAt = now.AddDays(days),
                    Source = Domain.Premium.SubscriptionSource.Promo,
                    CreatedAt = now,
                });
            }
        }
        else
        {
            user = await db.Users.SingleAsync(u => u.Id == existing.UserId, ct);
        }

        existing.Display = Cut(login.Display, 256) ?? existing.Display;
        existing.LastUsedAt = now;
        user.LastSeenAt = now;
        if (admins.CurrentValue.IsAdmin(login))
        {
            user.Roles |= UserRoles.Admin;
        }

        await db.SaveChangesAsync(ct);
        await RecordDayAsync(user.Id, now, ct);
        return user;
    }

    /// <summary>Adds a second way to sign in (spec §5.1). A login already used by another account is refused, not merged.</summary>
    public async Task<MeDto> LinkAsync(Guid userId, VerifiedLogin login, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync(LockKey(login), ct);
        var existing = await db.UserLogins.SingleOrDefaultAsync(l => l.Provider == login.Provider && l.ProviderUserId == login.ProviderUserId, ct);
        if (existing is not null && existing.UserId != userId)
        {
            throw new ConflictException("Bu hisob boshqa foydalanuvchiga bog‘langan. Avval o‘sha foydalanuvchi bilan kirib, uni o‘chiring.");
        }

        if (existing is null)
        {
            if (await db.UserLogins.AnyAsync(l => l.UserId == userId && l.Provider == login.Provider, ct))
            {
                throw new ConflictException(login.Provider == LoginProvider.Telegram
                    ? "Bu hisobga boshqa Telegram allaqachon bog‘langan."
                    : "Bu hisobga boshqa Google hisobi allaqachon bog‘langan.");
            }

            db.UserLogins.Add(NewLogin(userId, login, time.GetUtcNow()));
            var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
            if (admins.CurrentValue.IsAdmin(login))
            {
                user.Roles |= UserRoles.Admin;
            }

            await db.SaveChangesAsync(ct);
        }

        return await GetMeAsync(userId, ct);
    }

    /// <summary>The token's session version and the user's roles now, or null for a deleted or unknown user.</summary>
    public async Task<SessionCheck?> CheckSessionAsync(Guid userId, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.DeletedAt == null)
            .Select(u => new SessionCheck(u.SessionVersion, u.Roles, u.LastSeenAt))
            .SingleOrDefaultAsync(ct);

    /// <summary>"Last seen" and the day's activity are written at most about once an hour, not on every request.</summary>
    public bool NeedsTouch(DateTimeOffset lastSeenAt)
    {
        var now = time.GetUtcNow();
        return now - lastSeenAt >= TimeSpan.FromHours(1) || KoreaTime.Day(lastSeenAt) != KoreaTime.Day(now);
    }

    /// <summary>Marks the user active now: "oxirgi kirish" for the teacher, daily active and retention for the admin.</summary>
    public async Task TouchAsync(Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(x => x.SetProperty(u => u.LastSeenAt, now), ct);
        await RecordDayAsync(userId, now, ct);
    }

    private async Task RecordDayAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var day = KoreaTime.Day(now);
        if (await db.ActivityDays.AnyAsync(a => a.UserId == userId && a.Day == day, ct))
        {
            return;
        }

        db.ActivityDays.Add(new UserActivityDay { UserId = userId, Day = day });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.DiscardChanges(); // a parallel request recorded the same day
        }
    }

    /// <summary>Signs out everywhere: every access and refresh token issued so far stops working (L-13).</summary>
    public async Task LogoutAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        user.SessionVersion++;
        await db.SaveChangesAsync(ct);
    }

    public async Task<MeDto> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, ct);
        var logins = await db.UserLogins.AsNoTracking().Where(l => l.UserId == userId).OrderBy(l => l.CreatedAt)
            .Select(l => new LoginDto(l.Provider, l.Display, l.CreatedAt)).ToListAsync(ct);
        return new MeDto(
            user.Id,
            user.DisplayName,
            [.. Enum.GetValues<UserRoles>().Where(r => r != UserRoles.None && user.Roles.HasFlag(r)).Select(r => r.ToString())],
            logins,
            user.TimeZone,
            user.ShowRomanization,
            user.Theme);
    }

    public async Task<MeDto> UpdateMeAsync(Guid userId, UpdateMeRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        if (request.DisplayName is { } name)
        {
            name = name.Trim();
            if (name.Length is 0 or > MaxDisplayName)
            {
                throw new ValidationException($"Ism 1 dan {MaxDisplayName} belgigacha bo‘lsin.");
            }

            user.DisplayName = name;
        }

        if (request.ShowRomanization is { } romanization)
        {
            user.ShowRomanization = romanization;
        }

        if (request.Theme is { } theme)
        {
            user.Theme = Enum.IsDefined(theme) ? theme : throw new ValidationException("Mavzu noto‘g‘ri.");
        }

        await db.SaveChangesAsync(ct);
        return await GetMeAsync(userId, ct);
    }

    /// <summary>
    /// Deletes the account (spec §5.3): logins, study words, cards, answers, tests and every exercise are removed;
    /// the user row stays only as an anonymous tombstone for records that must remain (AI usage now, payments later).
    /// All tokens stop working at once.
    /// </summary>
    public async Task DeleteAsync(Guid userId, CancellationToken ct)
    {
        await using var tx = await db.BeginTransactionAsync(ct);
        // Children before parents: review events and usages hang off cards and words.
        await db.ReviewEvents.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.StudySessions.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.Cards.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.WordUsages.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.WritingAttempts.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.SentenceChecks.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.QuestionReports.Where(x => x.ReporterId == userId).ExecuteDeleteAsync(ct);
        await db.TestRuns.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.SentenceLadders.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.TrapNotes.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.ListeningSets.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.Crosswords.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.ReminderDeliveries.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.ActivityDays.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.GroupMembers.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        // A teacher's groups close with the account; their learners' own data stays theirs.
        await db.Groups.Where(x => x.TeacherUserId == userId && x.ClosedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(g => g.ClosedAt, time.GetUtcNow()), ct);
        await db.UnitProgress.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.PersonalWords.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.WordDecks.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.LearningItems.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct); // personal words only; catalog items are shared
        await db.UserLogins.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);

        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        user.DisplayName = null;
        user.PaymentCode = null;
        user.PromoCodeId = null;
        user.SignupSource = null;
        user.Roles = UserRoles.None;
        user.RemindersEnabled = false;
        user.BotBlockedAt = null;
        user.AiProvider = null;
        user.SessionVersion++;
        user.DeletedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<PreferencesDto> GetPreferencesAsync(Guid userId, bool remindersAvailable, CancellationToken ct) =>
        await ToDtoAsync(await db.Users.SingleAsync(u => u.Id == userId, ct), remindersAvailable, ct);

    /// <summary>Reminder schedule stays under the learner's control (spec NOT04).</summary>
    public async Task<PreferencesDto> UpdatePreferencesAsync(Guid userId, UpdatePreferencesRequest request, bool remindersAvailable, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        if (request.TimeZone is { } zone)
        {
            user.TimeZone = User.TimeZones.Contains(zone) && TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _)
                ? zone
                : throw new ValidationException("Vaqt zonasi noto‘g‘ri: Koreya (Asia/Seoul) yoki O‘zbekiston (Asia/Tashkent) bo‘lsin.");
        }

        if (request.RemindersEnabled is { } enabled)
        {
            user.RemindersEnabled = enabled;
        }

        if (request.ReminderTime is { } at)
        {
            user.ReminderTime = new TimeOnly(at.Hour, at.Minute);
        }

        if (request.ReminderDays is { } days)
        {
            user.ReminderDays = days.Count > 0
                ? Domain.Reminders.ReminderDaysExtensions.FromList(days)
                : throw new ValidationException("Kamida bitta kunni tanlang yoki eslatmalarni o‘chiring.");
        }

        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(user, remindersAvailable, ct);
    }

    private async Task<PreferencesDto> ToDtoAsync(User u, bool remindersAvailable, CancellationToken ct) => new(
        u.TimeZone,
        u.RemindersEnabled,
        u.ReminderTime,
        Domain.Reminders.ReminderDaysExtensions.ToList(u.ReminderDays),
        remindersAvailable,
        u.BotBlockedAt is not null,
        await db.UserLogins.AnyAsync(l => l.UserId == u.Id && l.Provider == LoginProvider.Telegram, ct));

    private static UserLogin NewLogin(Guid userId, VerifiedLogin login, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        UserId = userId,
        Provider = login.Provider,
        ProviderUserId = login.ProviderUserId,
        Display = Cut(login.Display, 256),
        CreatedAt = now,
        LastUsedAt = now,
    };

    private static string LockKey(VerifiedLogin login) => $"login:{login.Provider}:{login.ProviderUserId}";

    private static string? Cut(string? text, int max) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim() is var t && t.Length > max ? t[..max] : text.Trim();
}
