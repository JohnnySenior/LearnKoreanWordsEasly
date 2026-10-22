using System.Globalization;
using Lugat.Application.Abstractions;
using Lugat.Contracts;
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
public sealed record SessionCheck(int SessionVersion, UserRoles Roles);

public sealed class UserService(IAppDbContext db, TimeProvider time, IOperationLock operations, IOptionsMonitor<AdminOptions> admins)
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
            .Select(u => new SessionCheck(u.SessionVersion, u.Roles))
            .SingleOrDefaultAsync(ct);

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
        await db.TestRuns.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.SentenceLadders.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.TrapNotes.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.ListeningSets.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct);
        await db.ReminderDeliveries.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.UnitProgress.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.LearningItems.Where(x => x.OwnerId == userId).ExecuteDeleteAsync(ct); // personal words only; catalog items are shared
        await db.UserLogins.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);

        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        user.DisplayName = null;
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
        if (request.RemindersEnabled is { } enabled)
        {
            user.RemindersEnabled = enabled;
        }

        if (request.WeekdayReminderTime is { } weekday)
        {
            user.WeekdayReminderTime = new TimeOnly(weekday.Hour, weekday.Minute);
        }

        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(user, remindersAvailable, ct);
    }

    private async Task<PreferencesDto> ToDtoAsync(User u, bool remindersAvailable, CancellationToken ct) => new(
        u.TimeZone,
        u.RemindersEnabled,
        u.WeekdayReminderTime,
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
