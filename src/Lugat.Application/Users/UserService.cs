using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Users;

public sealed class UserService(IAppDbContext db, TimeProvider time)
{
    /// <summary>Creates the user on first sign-in and refreshes the display fields afterwards.</summary>
    public async Task<User> EnsureAsync(long telegramUserId, string? firstName, string? username, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var user = await db.Users.SingleOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);
        if (user is null)
        {
            user = new User
            {
                Id = Guid.CreateVersion7(now),
                TelegramUserId = telegramUserId,
                CreatedAt = now,
            };
            db.Users.Add(user);
        }

        user.FirstName = firstName ?? user.FirstName;
        user.Username = username ?? user.Username;
        user.LastSeenAt = now;

        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<PreferencesDto> GetPreferencesAsync(Guid userId, bool remindersAvailable, CancellationToken ct) =>
        ToDto(await db.Users.SingleAsync(u => u.Id == userId, ct), remindersAvailable);

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
        return ToDto(user, remindersAvailable);
    }

    private static PreferencesDto ToDto(User u, bool remindersAvailable) => new(
        u.TimeZone,
        u.RemindersEnabled,
        u.WeekdayReminderTime,
        remindersAvailable,
        u.BotBlockedAt is not null);
}
