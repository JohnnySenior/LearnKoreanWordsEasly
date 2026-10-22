using System.Text.Json;
using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Ladder;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Mistakes;

/// <summary>
/// Weak points (D-54): the mistakes notebook read for a short window and folded into what to practise next:
/// particles and verb endings missed in the ladder's gaps, the kinds of mistakes in the learner's own
/// sentences (sentence checks and ladder translations), word-for-word traps, words and listening. Nothing new is
/// stored; the "Zaif joylar zinapoyasi" (LadderService) is built from this.
/// </summary>
public sealed class WeakPointService(IAppDbContext db, MistakeService mistakes, TimeProvider time)
{
    public static readonly int[] Windows = [7, 30];

    public const int MaxForms = 6;

    public const int MaxWords = 8;

    public const int MaxTraps = 5;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The sentence checker's error types (D-52) with the labels the learner sees, as the ladder's.</summary>
    public static readonly IReadOnlyDictionary<string, string> SentenceErrorLabels = new Dictionary<string, string>
    {
        ["grammar"] = "grammatika",
        ["spelling"] = "imlo",
        ["meaning"] = "ma’no",
        ["word_choice"] = "so‘z tanlash",
        ["style"] = "uslub",
        ["naturalness"] = "tabiiylik",
        ["target_missing"] = "kerakli so‘z ishlatilmagan",
    };

    public async Task<WeakPointsDto> GetAsync(Guid ownerId, int days, CancellationToken ct)
    {
        if (!Windows.Contains(days))
        {
            throw new ValidationException("Davr 7 yoki 30 kun bo‘lishi mumkin.");
        }

        var since = time.GetUtcNow().AddDays(-days);
        var notebook = await mistakes.GetAsync(ownerId, days, ct);

        var ladders = await db.SentenceLadders.Where(l => l.OwnerId == ownerId && l.CreatedAt >= since).AsNoTracking().ToListAsync(ct);
        var forms = Forms(ladders);

        var errors = new List<(string Label, DateTimeOffset At, string? Quote, string? Hint)>();
        foreach (var ladder in ladders)
        {
            foreach (var a in ladder.Answers.Where(a => a.Step == LadderStep.Translate && !a.IsCorrect))
            {
                errors.AddRange(a.ErrorTypes.Select(e => (e, a.AnsweredAt, (string?)a.Given, a.ExplanationUz)));
            }
        }

        var checks = await db.SentenceChecks
            .Where(c => c.OwnerId == ownerId && c.CreatedAt >= since && c.ResultJson != null)
            .Select(c => new { c.CreatedAt, c.ResultJson })
            .AsNoTracking()
            .ToListAsync(ct);
        foreach (var c in checks)
        {
            var r = JsonSerializer.Deserialize<SentenceCheckResult>(c.ResultJson!, Json);
            if (r is null || r.Assisted)
            {
                continue;
            }

            if (r.Issues is { Count: > 0 } issues)
            {
                errors.AddRange(issues.Where(i => i.Severity == "error" && SentenceErrorLabels.ContainsKey(i.Type))
                    .Select(i => (SentenceErrorLabels[i.Type], c.CreatedAt, (string?)i.Quote, (string?)i.HintUz)));
            }
            else if (r.Verdict is not "correct" && SentenceErrorLabels.TryGetValue(r.ErrorType, out var label))
            {
                errors.Add((label, c.CreatedAt, null, r.HintUz)); // before D-52 a check named one problem only
            }
        }

        var errorTypes = errors.GroupBy(e => e.Label)
            .Select(g =>
            {
                var latest = g.OrderByDescending(e => e.At).First();
                return new WeakErrorTypeDto(g.Key, g.Count(), latest.Quote, latest.Hint);
            })
            .OrderByDescending(e => e.Count)
            .ToList();

        var traps = await db.TrapNotes.Where(t => t.OwnerId == ownerId && t.CreatedAt >= since)
            .OrderByDescending(t => t.CreatedAt).Take(MaxTraps)
            .Select(t => new WeakTrapDto(t.Wrong, t.Right, t.ExplanationUz))
            .AsNoTracking()
            .ToListAsync(ct);

        var listening = await db.ListeningSets.Where(l => l.OwnerId == ownerId && l.CreatedAt >= since).Select(l => l.Answers).AsNoTracking().ToListAsync(ct);

        return new WeakPointsDto(
            days,
            forms,
            errorTypes,
            notebook.Words.Where(w => !w.Improving).Take(MaxWords).ToList(),
            traps,
            listening.Sum(a => a.Count(x => !x.IsCorrect)),
            listening.Sum(a => a.Count));
    }

    /// <summary>
    /// Gaps answered wrongly, per right form (에서, -아서 …), most missed first; a form that was later chosen right as
    /// often as it was missed is no longer weak.
    /// </summary>
    public static List<WeakFormDto> Forms(IEnumerable<SentenceLadder> ladders)
    {
        var fills = ladders
            .SelectMany(l => l.Answers.Where(a => a.Step == LadderStep.Fill).Select(a => (Item: l.Items.Single(i => i.Index == a.Index), Answer: a)))
            .ToList();
        return fills.GroupBy(f => f.Item.Focus.Answer)
            .Select(g =>
            {
                var wrong = g.Where(f => !f.Answer.IsCorrect).OrderByDescending(f => f.Answer.AnsweredAt).ToList();
                return (Group: g, Wrong: wrong);
            })
            .Where(x => x.Wrong.Count > 0 && x.Wrong.Count >= x.Group.Count() - x.Wrong.Count)
            .OrderByDescending(x => x.Wrong.Count)
            .ThenByDescending(x => x.Wrong[0].Answer.AnsweredAt)
            .Take(MaxForms)
            .Select(x => new WeakFormDto(x.Group.Key, x.Wrong[0].Item.Focus.Kind, x.Wrong.Count, x.Group.Count(), x.Wrong[0].Item.Focus.SentenceWithGap, x.Wrong[0].Answer.Given))
            .ToList();
    }
}
