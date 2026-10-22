using Lugat.Domain.Learning;

namespace Lugat.Domain.Lessons;

/// <summary>A card the planner may choose from.</summary>
/// <param name="InLesson">Belongs to the nearest upcoming lesson assignment.</param>
/// <param name="UpcomingLesson">
/// Position of the planned lesson the word belongs to, nearest first (0 = the nearest lesson); null when it is in none.
/// Lets new words of lessons planned ahead (a whole week) be learned before the nearest one is handed in.
/// </param>
public sealed record PlanCandidate(
    Guid CardId,
    Guid WordSenseId,
    ReviewDirection Direction,
    LearningState State,
    bool InLesson,
    int OrderInDocument,
    int? UpcomingLesson = null);

/// <param name="Capacity">How many prompts fit in the chosen time; null = no limit (still capped at <see cref="SessionPlanner.MaxItems"/>).</param>
/// <param name="NewOnlyFromLesson">When lessons are planned, new words come only from them (nearest lesson first); otherwise new words follow the book order.</param>
/// <param name="DrillLesson">
/// Whole-lesson practice before the teacher's test: the candidates are the words of one lesson and every one of them
/// is asked, also those not yet due. An early answer never moves a card up the ladder (scheduler WasDue).
/// </param>
public sealed record PlanRequest(DateTimeOffset Now, int? Capacity, bool NewOnlyFromLesson, bool DrillLesson = false);

/// <summary>
/// Builds a session queue (spec PLAN08 technical baseline):
/// recently forgotten cards first, then due cards of the nearest lesson, other due cards and new material.
/// About 20% of the session is reserved for long-overdue old cards so they are never pushed aside for ever;
/// an empty category gives its share to the others. The reverse of a card never comes right after it (spec SRS03);
/// a new word's reverse comes several prompts later and in a mixed order (D-45).
/// There is no daily cap on new words (spec PLAN03).
/// </summary>
public static class SessionPlanner
{
    public const int MaxItems = 200;

    public const double OverdueShare = 0.2;

    /// <summary>A due card this much past its date counts as long overdue.</summary>
    public static readonly TimeSpan OverdueAfter = TimeSpan.FromDays(1);

    /// <summary>
    /// A new word's reverse comes at least this many prompts after its Korean → Uzbek card (so 3+ other prompts in
    /// between), except for the last few words of a session, which have fewer words after them (D-45).
    /// </summary>
    public const int MinReverseGap = 4;

    /// <summary>The reverse is placed this many words later, plus a random part up to <see cref="ReverseOffsetSpread"/>.</summary>
    private const double ReverseOffsetMin = 3;

    private const double ReverseOffsetSpread = 5;

    public static List<SessionItem> Plan(IReadOnlyList<PlanCandidate> candidates, PlanRequest request)
    {
        var capacity = Math.Min(request.Capacity ?? MaxItems, MaxItems);
        if (capacity <= 0)
        {
            return [];
        }

        var now = request.Now;
        var due = candidates.Where(c => c.State.Phase != LearningPhase.New && c.State.IsDue(now)).OrderBy(c => c.State.DueAt).ToList();

        var relearning = due.Where(c => c.State.Phase == LearningPhase.Relearning).ToList();
        var reviews = due.Where(c => c.State.Phase == LearningPhase.Review).ToList();
        var lessonReviews = reviews.Where(c => c.InLesson).ToList();
        var overdue = reviews.Where(c => !c.InLesson && now - c.State.DueAt!.Value >= OverdueAfter).ToList();
        var otherReviews = reviews.Where(c => !c.InLesson && now - c.State.DueAt!.Value < OverdueAfter).ToList();

        if (request.DrillLesson)
        {
            // Cards of the lesson that are not due yet come after the due ones, oldest review first.
            lessonReviews.AddRange(candidates
                .Where(c => c.State.Phase is LearningPhase.Review or LearningPhase.Relearning && !c.State.IsDue(now))
                .OrderBy(c => c.State.DueAt));
        }

        var newSenses = candidates
            .Where(c => c.State.Phase == LearningPhase.New && (c.InLesson || c.UpcomingLesson is not null || !request.NewOnlyFromLesson))
            .GroupBy(c => c.WordSenseId)
            .OrderBy(g => g.Min(c => c.InLesson ? 0 : c.UpcomingLesson ?? int.MaxValue))
            .ThenBy(g => g.Min(c => c.OrderInDocument))
            .Select(g => g.OrderBy(c => c.Direction).ToList())
            .ToList();

        var overdueQuota = overdue.Count == 0 ? 0 : Math.Min(overdue.Count, (int)Math.Ceiling(capacity * OverdueShare));
        var room = capacity - overdueQuota;

        var chosen = new List<SessionItem>();
        room -= Take(chosen, relearning, SessionItemKind.Relearning, room);
        room -= Take(chosen, lessonReviews, SessionItemKind.LessonReview, room);
        var overdueTaken = Take(chosen, overdue, SessionItemKind.Overdue, overdueQuota);
        room -= Take(chosen, otherReviews, SessionItemKind.Review, room);

        var newItems = new List<SessionItem>();
        var newSensesTaken = new List<List<PlanCandidate>>();
        foreach (var sense in newSenses)
        {
            if (sense.Count > room)
            {
                break;
            }

            newSensesTaken.Add(sense);
            room -= sense.Count;
        }

        newItems.AddRange(InterleaveNew(newSensesTaken, new Random(Seed(now))));

        chosen.AddRange(newItems);

        // Leftover space goes to more overdue cards.
        Take(chosen, overdue.Skip(overdueTaken).ToList(), SessionItemKind.Overdue, room);

        return SeparateReverses(chosen);
    }

    /// <summary>
    /// New words: the Korean → Uzbek cards keep book order, and each reverse is placed a random 3–8 words later
    /// (roughly 5–15 prompts). Sorting everything by that place mixes the
    /// Uzbek → Korean prompts among the next words, so they neither follow straight after their Korean cards nor come
    /// in the same order (D-45; before, a group of three was asked K1 K2 K3 U1 U2 U3). The last words' reverses end
    /// the block in a mixed order.
    /// </summary>
    private static List<SessionItem> InterleaveNew(List<List<PlanCandidate>> senses, Random random)
    {
        var placed = new List<(double Key, int Tie, PlanCandidate Card)>();
        for (var i = 0; i < senses.Count; i++)
        {
            placed.Add((i, 0, senses[i][0]));
            placed.AddRange(senses[i].Skip(1).Select(c => (i + ReverseOffsetMin + random.NextDouble() * ReverseOffsetSpread, 1, c)));
        }

        return [.. placed.OrderBy(p => p.Key).ThenBy(p => p.Tie).Select(p => Item(p.Card, SessionItemKind.New))];
    }

    /// <summary>Same queue for the same moment, so a plan can be reproduced in tests.</summary>
    private static int Seed(DateTimeOffset now) => (int)(now.UtcTicks ^ (now.UtcTicks >> 32));

    private static int Take(List<SessionItem> into, List<PlanCandidate> from, SessionItemKind kind, int max)
    {
        var take = Math.Max(0, Math.Min(max, from.Count));
        into.AddRange(from.Take(take).Select(c => Item(c, kind)));
        return take;
    }

    private static SessionItem Item(PlanCandidate c, SessionItemKind kind) => new(c.CardId, c.WordSenseId, c.Direction, kind);

    /// <summary>
    /// Makes sure the two directions of a word never follow each other (spec SRS03). For each clash the later item
    /// is moved further down to a place whose neighbours are other words; if there is none, the earlier item is moved
    /// further up instead, so a new word is still asked Korean → Uzbek first. Only a queue made of a single word
    /// can keep a clash.
    /// </summary>
    public static List<SessionItem> SeparateReverses(List<SessionItem> items)
    {
        var result = items.ToList();
        for (var i = 1; i < result.Count; i++)
        {
            if (result[i].WordSenseId != result[i - 1].WordSenseId)
            {
                continue;
            }

            var later = result[i];
            result.RemoveAt(i);
            var down = FindSlot(result, later.WordSenseId, from: i, to: result.Count);
            if (down >= 0)
            {
                result.Insert(down, later);
                i = 0; // re-check from the start; lists are short
                continue;
            }

            result.Insert(i, later);
            var earlier = result[i - 1];
            result.RemoveAt(i - 1);
            var up = FindSlot(result, earlier.WordSenseId, from: 0, to: i - 2);
            if (up >= 0)
            {
                result.Insert(up, earlier);
                i = 0;
                continue;
            }

            result.Insert(i - 1, earlier); // unavoidable: nothing else to put in between
        }

        return result;
    }

    /// <summary>First insertion index in [from, to] whose neighbours both belong to other words.</summary>
    private static int FindSlot(List<SessionItem> list, Guid sense, int from, int to)
    {
        for (var p = Math.Max(0, from); p <= Math.Min(to, list.Count); p++)
        {
            var prevOk = p == 0 || list[p - 1].WordSenseId != sense;
            var nextOk = p == list.Count || list[p].WordSenseId != sense;
            if (prevOk && nextOk)
            {
                return p;
            }
        }

        return -1;
    }
}
