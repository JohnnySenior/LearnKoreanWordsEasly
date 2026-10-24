using System.Text.RegularExpressions;
using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain;
using Lugat.Domain.Premium;
using Lugat.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Users;

/// <summary>
/// Which ad channel brings learners who stay (L-40): every channel gets its own link (<c>koreyscha.com/?src=tg</c>).
/// A visit with the link is counted per day without anything about the visitor; the first link a new user came with
/// stays on their account. The admin sees visits, sign-ups, D7 retention and paying users per channel.
/// </summary>
public sealed partial class SourceService(IAppDbContext db, TimeProvider time)
{
    public const int MaxLength = 32;

    /// <summary>A link counts only for an account made this recently; an old user opening an ad link is not "brought".</summary>
    public static readonly TimeSpan NewAccount = TimeSpan.FromDays(2);

    /// <summary>"Instagram " → "instagram"; null when it is not a short code of letters, digits, "-" and "_".</summary>
    public static string? Normalize(string? source)
    {
        var s = (source ?? "").Trim().ToLowerInvariant();
        return s.Length is > 0 and <= MaxLength && Code().IsMatch(s) ? s : null;
    }

    public async Task VisitAsync(string? source, CancellationToken ct)
    {
        if (Normalize(source) is not { } src)
        {
            return;
        }

        var day = KoreaTime.Day(time.GetUtcNow());
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (await db.SourceVisits.Where(v => v.Source == src && v.Day == day).ExecuteUpdateAsync(v => v.SetProperty(x => x.Count, x => x.Count + 1), ct) > 0)
            {
                return;
            }

            db.SourceVisits.Add(new SourceVisit { Source = src, Day = day, Count = 1 });
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException)
            {
                // Another visit made today's row first: count on it.
                db.DiscardChanges();
            }
        }
    }

    /// <summary>The first channel stays; a later link, or one opened long after signing up, changes nothing.</summary>
    public async Task AttachAsync(Guid userId, string? source, CancellationToken ct)
    {
        if (Normalize(source) is not { } src)
        {
            return;
        }

        var since = time.GetUtcNow() - NewAccount;
        await db.Users.Where(u => u.Id == userId && u.SignupSource == null && u.CreatedAt >= since)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.SignupSource, src), ct);
    }

    /// <summary>
    /// The last <paramref name="days"/> Korea days per channel, most sign-ups first. D7 as in the metrics: of the
    /// channel's users who signed up at least 7 days ago, those active exactly 7 days after their sign-up day.
    /// </summary>
    public async Task<IReadOnlyList<SourceRowDto>> ReportAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 365);
        var today = KoreaTime.Day(time.GetUtcNow());
        var from = today.AddDays(-(days - 1));
        var fromUtc = KoreaTime.StartOf(from);
        var visits = (await db.SourceVisits.Where(v => v.Day >= from).ToListAsync(ct))
            .GroupBy(v => v.Source).ToDictionary(g => g.Key, g => g.Sum(v => v.Count));
        var users = await db.Users.Where(u => u.SignupSource != null && u.CreatedAt >= fromUtc && u.DeletedAt == null)
            .Select(u => new { u.Id, Source = u.SignupSource!, u.CreatedAt }).ToListAsync(ct);
        var ids = users.Select(u => u.Id).ToList();
        var active = (await db.ActivityDays.Where(a => ids.Contains(a.UserId)).Select(a => new { a.UserId, a.Day }).ToListAsync(ct))
            .Select(a => (a.UserId, a.Day)).ToHashSet();
        var paying = (await db.Payments.Where(p => p.Status == PaymentStatus.Confirmed && ids.Contains(p.UserId)).Select(p => p.UserId).Distinct().ToListAsync(ct))
            .ToHashSet();

        return visits.Keys.Union(users.Select(u => u.Source))
            .Select(src =>
            {
                var mine = users.Where(u => u.Source == src).ToList();
                var cohort = mine.Where(u => KoreaTime.Day(u.CreatedAt) <= today.AddDays(-7)).ToList();
                var returned = cohort.Count(u => active.Contains((u.Id, KoreaTime.Day(u.CreatedAt).AddDays(7))));
                return new SourceRowDto(src, visits.GetValueOrDefault(src), mine.Count,
                    new RetentionDto(cohort.Count, returned, cohort.Count == 0 ? null : (int)Math.Round(returned * 100.0 / cohort.Count)),
                    mine.Count(u => paying.Contains(u.Id)));
            })
            .OrderByDescending(r => r.SignUps).ThenByDescending(r => r.Visits).ThenBy(r => r.Source)
            .ToList();
    }

    [GeneratedRegex("^[a-z0-9_-]+$")]
    private static partial Regex Code();
}
