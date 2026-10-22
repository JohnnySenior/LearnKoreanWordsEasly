using Lugat.Domain.Learning;
using Lugat.Domain.Lessons;

namespace Lugat.Domain.Tests.Lessons;

public class SessionPlannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 1, 0, 0, TimeSpan.Zero);

    private int _order;

    private IEnumerable<PlanCandidate> NewWord(bool inLesson = true)
    {
        var sense = Guid.NewGuid();
        var order = ++_order;
        yield return new(Guid.NewGuid(), sense, ReviewDirection.KoreanToUzbek, LearningState.New(), inLesson, order);
        yield return new(Guid.NewGuid(), sense, ReviewDirection.UzbekToKorean, LearningState.New(), inLesson, order);
    }

    private PlanCandidate Due(TimeSpan overdueBy, LearningPhase phase = LearningPhase.Review, bool inLesson = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), ReviewDirection.KoreanToUzbek,
            new LearningState { Phase = phase, Stage = 1, Interval = TimeSpan.FromDays(1), DueAt = Now - overdueBy },
            inLesson, ++_order);

    private static PlanRequest Request(int? capacity, bool newOnlyFromLesson = true) => new(Now, capacity, newOnlyFromLesson);

    [Fact]
    public void Forgotten_cards_come_first()
    {
        var review = Due(TimeSpan.FromHours(1));
        var forgotten = Due(TimeSpan.FromMinutes(1), LearningPhase.Relearning);

        var plan = SessionPlanner.Plan([review, forgotten], Request(10));

        Assert.Equal([forgotten.CardId, review.CardId], plan.Select(i => i.CardId));
        Assert.Equal(SessionItemKind.Relearning, plan[0].Kind);
    }

    [Fact]
    public void Cards_not_yet_due_are_left_out()
    {
        var notDue = Due(TimeSpan.FromHours(-3));

        Assert.Empty(SessionPlanner.Plan([notDue], Request(10)));
    }

    [Fact]
    public void Lesson_reviews_come_before_other_reviews()
    {
        var other = Due(TimeSpan.FromHours(5));
        var lesson = Due(TimeSpan.FromHours(1), inLesson: true);

        var plan = SessionPlanner.Plan([other, lesson], Request(10));

        Assert.Equal([lesson.CardId, other.CardId], plan.Select(i => i.CardId));
    }

    [Fact]
    public void Long_overdue_cards_keep_about_a_fifth_of_the_session()
    {
        var lesson = Enumerable.Range(0, 20).Select(_ => Due(TimeSpan.FromHours(2), inLesson: true)).ToList();
        var old = Enumerable.Range(0, 20).Select(_ => Due(TimeSpan.FromDays(10))).ToList();

        var plan = SessionPlanner.Plan([.. lesson, .. old], Request(10));

        Assert.Equal(10, plan.Count);
        Assert.Equal(2, plan.Count(i => i.Kind == SessionItemKind.Overdue));
        Assert.Equal(8, plan.Count(i => i.Kind == SessionItemKind.LessonReview));
    }

    [Fact]
    public void Empty_categories_give_their_share_to_others()
    {
        var old = Enumerable.Range(0, 20).Select(_ => Due(TimeSpan.FromDays(10))).ToList();

        var plan = SessionPlanner.Plan(old, Request(10));

        Assert.Equal(10, plan.Count);
        Assert.All(plan, i => Assert.Equal(SessionItemKind.Overdue, i.Kind));
    }

    [Fact]
    public void New_words_bring_both_directions_without_back_to_back_reverses()
    {
        var words = Enumerable.Range(0, 6).SelectMany(_ => NewWord()).ToList();

        var plan = SessionPlanner.Plan(words, Request(null));

        Assert.Equal(12, plan.Count);
        Assert.All(plan, i => Assert.Equal(SessionItemKind.New, i.Kind));
        for (var i = 1; i < plan.Count; i++)
        {
            Assert.NotEqual(plan[i - 1].WordSenseId, plan[i].WordSenseId);
        }

        // Each word is first asked Korean → Uzbek.
        foreach (var sense in plan.GroupBy(i => i.WordSenseId))
        {
            Assert.Equal(ReviewDirection.KoreanToUzbek, sense.First().Direction);
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void Reverse_comes_several_prompts_later_and_in_a_mixed_order(int count)
    {
        var words = Enumerable.Range(0, count).SelectMany(_ => NewWord()).ToList();

        var plan = SessionPlanner.Plan(words, Request(null));

        var position = plan.Select((item, index) => (item, index)).ToLookup(x => x.item.WordSenseId, x => x.index);
        var senses = words.Select(w => w.WordSenseId).Distinct().ToList();
        // Away from the very end there are enough other words to keep each reverse well apart (D-45).
        foreach (var sense in senses.Take(count - SessionPlanner.MinReverseGap))
        {
            var (korean, uzbek) = (position[sense].First(), position[sense].Last());
            Assert.True(uzbek - korean >= SessionPlanner.MinReverseGap, $"reverse only {uzbek - korean} prompts later");
        }

        // The Uzbek → Korean prompts do not simply repeat the Korean order (before: K1 K2 K3 U1 U2 U3).
        var koreanOrder = plan.Where(i => i.Direction == ReviewDirection.KoreanToUzbek).Select(i => i.WordSenseId).ToList();
        var uzbekOrder = plan.Where(i => i.Direction == ReviewDirection.UzbekToKorean).Select(i => i.WordSenseId).ToList();
        Assert.Equal(senses, koreanOrder); // new words still come in book order
        Assert.NotEqual(koreanOrder, uzbekOrder);
    }

    [Fact]
    public void Same_moment_gives_the_same_queue()
    {
        var words = Enumerable.Range(0, 12).SelectMany(_ => NewWord()).ToList();

        Assert.Equal(SessionPlanner.Plan(words, Request(null)), SessionPlanner.Plan(words, Request(null)));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(28)]
    [InlineData(58)]
    [InlineData(60)]
    [InlineData(100)]
    public void Reverse_never_follows_its_own_card_for_any_number_of_new_words(int count)
    {
        var words = Enumerable.Range(0, count).SelectMany(_ => NewWord()).ToList();

        var plan = SessionPlanner.Plan(words, Request(null));

        Assert.Equal(Math.Min(count * 2, SessionPlanner.MaxItems), plan.Count);
        AssertNoBackToBack(plan);
        foreach (var sense in plan.GroupBy(i => i.WordSenseId))
        {
            Assert.Equal(ReviewDirection.KoreanToUzbek, sense.First().Direction);
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    [InlineData(5, 2)]
    [InlineData(13, 1)]
    public void Reverses_stay_apart_when_new_words_follow_reviews(int newWords, int reviews)
    {
        var candidates = Enumerable.Range(0, newWords).SelectMany(_ => NewWord())
            .Concat(Enumerable.Range(0, reviews).Select(_ => Due(TimeSpan.FromHours(1), inLesson: true)))
            .ToList();

        AssertNoBackToBack(SessionPlanner.Plan(candidates, Request(null)));
    }

    [Fact]
    public void Separation_moves_an_earlier_item_up_when_nothing_follows()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        SessionItem Item(Guid s, ReviewDirection d) => new(Guid.NewGuid(), s, d, SessionItemKind.New);
        var items = new List<SessionItem>
        {
            Item(a, ReviewDirection.KoreanToUzbek),
            Item(a, ReviewDirection.UzbekToKorean),
            Item(b, ReviewDirection.KoreanToUzbek),
            Item(b, ReviewDirection.UzbekToKorean),
        };

        var result = SessionPlanner.SeparateReverses(items);

        AssertNoBackToBack(result);
        Assert.Equal(4, result.Count);
    }

    private static void AssertNoBackToBack(IReadOnlyList<SessionItem> plan)
    {
        for (var i = 1; i < plan.Count; i++)
        {
            Assert.True(plan[i - 1].WordSenseId != plan[i].WordSenseId, $"Items {i - 1} and {i} are the same word");
        }
    }

    [Fact]
    public void New_words_follow_book_order_and_never_split_a_word()
    {
        var words = Enumerable.Range(0, 5).SelectMany(_ => NewWord()).ToList();

        var plan = SessionPlanner.Plan(words, Request(5));

        Assert.Equal(4, plan.Count); // two whole words; a third would need 6 slots
        var firstTwoSenses = words.Select(w => w.WordSenseId).Distinct().Take(2).ToHashSet();
        Assert.All(plan, i => Assert.Contains(i.WordSenseId, firstTwoSenses));
    }

    [Fact]
    public void There_is_no_daily_cap_on_new_words()
    {
        var words = Enumerable.Range(0, 60).SelectMany(_ => NewWord()).ToList();

        Assert.Equal(120, SessionPlanner.Plan(words, Request(null)).Count);
    }

    [Fact]
    public void Only_lesson_words_are_new_when_a_lesson_exists()
    {
        var lessonWord = NewWord(inLesson: true).ToList();
        var laterWord = NewWord(inLesson: false).ToList();

        var withLesson = SessionPlanner.Plan([.. lessonWord, .. laterWord], Request(null, newOnlyFromLesson: true));
        var withoutLesson = SessionPlanner.Plan([.. lessonWord, .. laterWord], Request(null, newOnlyFromLesson: false));

        Assert.Equal(2, withLesson.Count);
        Assert.Equal(4, withoutLesson.Count);
    }

    [Fact]
    public void Unlimited_is_still_capped()
    {
        var old = Enumerable.Range(0, 300).Select(_ => Due(TimeSpan.FromHours(2))).ToList();

        Assert.Equal(SessionPlanner.MaxItems, SessionPlanner.Plan(old, Request(null)).Count);
    }

    [Fact]
    public void New_words_of_lessons_planned_ahead_follow_the_nearest_lesson()
    {
        PlanCandidate[] Word(int? lesson)
        {
            var sense = Guid.NewGuid();
            var order = ++_order;
            return
            [
                new(Guid.NewGuid(), sense, ReviewDirection.KoreanToUzbek, LearningState.New(), lesson == 0, order, lesson),
                new(Guid.NewGuid(), sense, ReviewDirection.UzbekToKorean, LearningState.New(), lesson == 0, order, lesson),
            ];
        }

        var later = Word(1); // earlier in the book, but its lesson comes second
        var nearest = Word(0);
        var none = Word(null);

        var plan = SessionPlanner.Plan([.. later, .. nearest, .. none], Request(null));

        Assert.Equal(4, plan.Count);
        Assert.Equal(nearest[0].WordSenseId, plan[0].WordSenseId);
        Assert.DoesNotContain(plan, i => i.WordSenseId == none[0].WordSenseId);
    }

    [Fact]
    public void Whole_lesson_drill_also_asks_cards_that_are_not_due()
    {
        var due = Due(TimeSpan.FromHours(1), inLesson: true);
        var notDue = Due(TimeSpan.FromDays(-2), inLesson: true);
        var fresh = NewWord().ToList();

        var normal = SessionPlanner.Plan([due, notDue, .. fresh], Request(null));
        var drill = SessionPlanner.Plan([due, notDue, .. fresh], Request(null) with { DrillLesson = true });

        Assert.DoesNotContain(normal, i => i.CardId == notDue.CardId);
        Assert.Equal(4, drill.Count);
        Assert.Equal([due.CardId, notDue.CardId], drill.Where(i => i.Kind == SessionItemKind.LessonReview).Select(i => i.CardId));
    }
}
