using Lugat.Application.Catalog;

namespace Lugat.Application.Tests.Catalog;

/// <summary>L-62: a lone jamo or a dash-led ending is given to the Korean voice in a form it can read.</summary>
public class IntroductionSpeakableTests
{
    [Theory]
    [InlineData("-ㄹ", "리을")]     // 살다: the ㄹ of the stem, Azure returned empty audio for it every time
    [InlineData("ㄹ", "리을")]
    [InlineData("ㅂ", "비읍")]
    [InlineData("ㅎ", "히읗")]
    [InlineData("ㅏ", "아")]
    [InlineData("ㅣ", "이")]
    [InlineData("-다", "다")]
    [InlineData("~아요", "아요")]
    [InlineData("살다", "살다")]     // ordinary text is unchanged
    [InlineData("살아요.", "살아요.")]
    [InlineData("-", "-")]          // nothing left to say: the text stays as it was
    public void Jamo_are_said_by_name_and_dashes_dropped(string text, string spoken) =>
        Assert.Equal(spoken, IntroductionAudioService.Speakable(text));
}
