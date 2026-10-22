using Lugat.Domain.Catalog;

namespace Lugat.Domain.Tests.Catalog;

public class UnitSplitterTests
{
    private static List<int> Words(int count) => Enumerable.Range(1, count).ToList();

    [Fact]
    public void Words_go_into_units_of_25_in_the_given_order()
    {
        var plan = UnitSplitter.Plan(Words(75), 1);

        Assert.Equal([1, 2, 3], plan.Units.Select(u => u.Number));
        Assert.All(plan.Units, u => Assert.Equal(25, u.Words.Count));
        Assert.Equal(Words(75), plan.Units.SelectMany(u => u.Words));
        Assert.Empty(plan.Unplaced);
    }

    [Theory]
    [InlineData(59, new[] { 25, 34 })] // 9 left over: joined to the unit before
    [InlineData(60, new[] { 25, 25, 10 })] // 10 is enough for a unit of its own
    [InlineData(982, new[] { 25 })] // checked below
    public void A_short_last_unit_is_joined_to_the_one_before(int count, int[] sizes)
    {
        var plan = UnitSplitter.Plan(Words(count), 1);

        if (count == 982)
        {
            // 39 × 25 = 975, 7 left: the 39th unit takes them.
            Assert.Equal(39, plan.Units.Count);
            Assert.Equal(32, plan.Units[^1].Words.Count);
        }
        else
        {
            Assert.Equal(sizes, plan.Units.Select(u => u.Words.Count));
        }

        Assert.Equal(count, plan.Units.Sum(u => u.Words.Count));
    }

    [Fact]
    public void Numbering_continues_after_published_units()
    {
        var plan = UnitSplitter.Plan(Words(30), 4);

        Assert.Equal([4], plan.Units.Select(u => u.Number));
        Assert.Equal(30, plan.Units[0].Words.Count);
    }

    [Fact]
    public void Too_few_words_without_a_new_unit_before_them_wait()
    {
        var plan = UnitSplitter.Plan(Words(7), 3);

        Assert.Empty(plan.Units);
        Assert.Equal(Words(7), plan.Unplaced);
    }

    [Theory]
    [InlineData("동", true)]
    [InlineData("명", true)]
    [InlineData("보", false)]
    [InlineData("의", false)]
    [InlineData("관", false)]
    [InlineData("명/의", true)]
    [InlineData("관/의", false)]
    public void Auxiliaries_bound_nouns_and_determiners_are_not_studied(string pos, bool studyable) =>
        Assert.Equal(studyable, CatalogWord.StudyablePos(pos));
}
