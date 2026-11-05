using Lugat.Application.Ladder;

namespace Lugat.Application.Tests.Ladder;

/// <summary>Johnny, 2026-11-05: the translation step showed "길이 많이 막혀서… – tarjima: Yo‘l …", the Korean answer included.</summary>
public class LadderUzbekTests
{
    [Theory]
    [InlineData("길이 많이 막혀서 그래서 늦게 도착했어요 – tarjima: Yo‘l qattiq tirbandligi sababli kech yetib keldim.", "Yo‘l qattiq tirbandligi sababli kech yetib keldim.")]
    [InlineData("Yo‘l tirband bo‘lgani uchun kech qoldim.", "Yo‘l tirband bo‘lgani uchun kech qoldim.")]
    [InlineData("  Ertaga ishxonaga boraman.  ", "Ertaga ishxonaga boraman.")]
    public void The_uzbek_sentence_keeps_only_uzbek(string given, string expected) =>
        Assert.Equal(expected, LadderService.UzbekOnly(given));

    [Theory]
    [InlineData("Yo‘l 막혀서 kech qoldim.")] // Korean inside the Uzbek: nothing safe to keep
    [InlineData("길이 막혀서 늦었어요")]
    [InlineData("")]
    public void A_sentence_with_korean_left_in_it_is_dropped(string given) =>
        Assert.Null(LadderService.UzbekOnly(given));
}
