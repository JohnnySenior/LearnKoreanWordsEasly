using Lugat.Application.Pronunciation;

namespace Lugat.Application.Tests.Pronunciation;

/// <summary>L-83: the standard rules between a spelling and its reviewed reading.</summary>
public class HangulRulesTests
{
    [Theory]
    [InlineData("음악", "으막", "liaison")]
    [InlineData("국물", "궁물", "nasal")]
    [InlineData("입니다", "임니다", "nasal")]
    [InlineData("한국말", "한궁말", "nasal")]
    [InlineData("끝나다", "끈나다", "neutral,nasal")]
    [InlineData("같이", "가치", "palatal")]
    [InlineData("학교", "학꾜", "tense")]
    [InlineData("익다", "익따", "tense")]
    [InlineData("좋다", "조타", "aspirate")]
    [InlineData("축하", "추카", "aspirate")]
    [InlineData("좋아요", "조아요", "hdrop")]
    [InlineData("많아요", "마나요", "hdrop,liaison")]
    [InlineData("연락", "열락", "lateral")]
    [InlineData("신라", "실라", "lateral")]
    [InlineData("종로", "종노", "rton")]
    [InlineData("독립", "동닙", "rton,nasal")]
    [InlineData("앉다", "안따", "cluster,tense")]
    [InlineData("읽다", "익따", "cluster,tense")]
    [InlineData("닭", "닥", "cluster")]
    [InlineData("꽃잎", "꼰닙", "ninsert,neutral,nasal")]
    [InlineData("희망", "히망", "ui")]
    [InlineData("들어가다", "드러가다", "liaison")]
    public void Each_change_is_named_by_its_rule(string korean, string reading, string keys)
    {
        var result = HangulRules.Explain(korean, reading);
        Assert.True(result.Complete, string.Join(" | ", result.Rules.Select(r => r.Detail)));
        Assert.Equal(keys.Split(',').OrderBy(k => k), result.Rules.Select(r => r.Key).Distinct().OrderBy(k => k));
        Assert.All(result.Rules, r => Assert.False(string.IsNullOrWhiteSpace(r.NameKo)));
    }

    [Fact]
    public void The_rule_has_an_uzbek_name_its_korean_name_and_where_it_happens()
    {
        var rule = Assert.Single(HangulRules.Explain("국물", "궁물").Rules);
        Assert.Equal(("Burunlashish", "비음화", "국물: ㄱ ㅁ oldidan ㅇ bo‘ladi"), (rule.NameUz, rule.NameKo, rule.Detail));
    }

    [Theory]
    [InlineData("학교", "학교")]
    [InlineData("사람", "사람")]
    public void A_word_read_as_written_has_no_rule(string korean, string reading)
    {
        var result = HangulRules.Explain(korean, reading);
        Assert.True(result.Complete);
        Assert.Empty(result.Rules);
    }

    [Theory]
    [InlineData("국물", "국무")]     // a reading no rule gives
    [InlineData("학교", "학교요")]   // syllables do not line up
    [InlineData("사과", "")]
    public void A_change_no_rule_explains_is_reported_as_incomplete(string korean, string reading)
    {
        Assert.False(HangulRules.Explain(korean, reading).Complete);
    }
}
