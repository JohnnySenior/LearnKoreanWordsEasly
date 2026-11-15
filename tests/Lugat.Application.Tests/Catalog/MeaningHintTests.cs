using Lugat.Application.Catalog;
using Lugat.Application.Vocabulary;

namespace Lugat.Application.Tests.Catalog;

/// <summary>L-74: a hint in parentheses after a shared meaning tells two words apart (아주 / 너무 "juda").</summary>
public class MeaningHintTests
{
    [Fact]
    public void Meanings_are_split_outside_parentheses_and_the_hint_is_not_part_of_the_meaning()
    {
        Assert.Equal(["juda (ijobiy, me’yorda)", " ko‘p"], UnitTestGenerator.Segments("juda (ijobiy, me’yorda); ko‘p"));
        Assert.Equal(new HashSet<string> { "juda", "ko'p" }, UnitTestGenerator.Meanings("juda (ijobiy, me’yorda); ko‘p"));
        Assert.Equal(new HashSet<string> { "ko'p" }, UnitTestGenerator.BareMeanings("juda (ijobiy, me’yorda); ko‘p"));
        Assert.Equal(new HashSet<string> { "men" }, UnitTestGenerator.Meanings("men (do‘stona)")); // as before
    }

    [Fact]
    public void Only_a_shared_meaning_without_a_hint_needs_one()
    {
        var neomu = new MeaningTwins.Twin(Guid.NewGuid(), "너무", "juda", "부");
        var aju = new MeaningTwins.Twin(Guid.NewGuid(), "아주", "juda (ijobiy)", "부");
        List<MeaningTwins.Twin> pool = [neomu, aju];

        Assert.Empty(MeaningTwins.Unhinted(aju.Id, aju.Korean, aju.Uzbek, pool));           // 아주 has its hint
        Assert.Equal([aju], MeaningTwins.Unhinted(neomu.Id, neomu.Korean, neomu.Uzbek, pool)); // 너무 still needs one
        Assert.Equal([neomu], MeaningTwins.Of(aju.Id, aju.Korean, aju.Uzbek, pool));        // still twins: typing 너무 is not a mistake
        Assert.Empty(MeaningTwins.Unhinted(neomu.Id, neomu.Korean, "juda (ortiqcha)", pool));
    }
}
