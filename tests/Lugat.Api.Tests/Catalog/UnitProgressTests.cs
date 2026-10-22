using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Catalog;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Lugat.Domain.Learning;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Catalog;

/// <summary>Stage 3 (spec §13.4): unit locks, opening a unit and its cards, daily review across opened units.</summary>
public class UnitProgressTests
{
    private static ApiFactory Seeded() => new() { Settings = { ["Catalog:SeedSample"] = "true" } };

    private static async Task<Guid> UnitIdAsync(ApiFactory factory, int number)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Units.Where(u => u.Level == CatalogLevel.A && u.Number == number).Select(u => u.Id).SingleAsync();
    }

    private static async Task<Guid> UserIdAsync(ApiFactory factory, long telegramId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.UserLogins.Where(l => l.ProviderUserId == telegramId.ToString()).Select(l => l.UserId).SingleAsync();
    }

    private static async Task PassAsync(ApiFactory factory, Guid userId, Guid unitId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<UnitProgressService>().PassAsync(userId, unitId, UnitPassedBy.Test, 92, CancellationToken.None);
    }

    [Fact]
    public async Task A_new_learner_has_only_the_first_unit_open_and_cannot_get_a_locked_unit()
    {
        await using var factory = Seeded();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var second = await UnitIdAsync(factory, 2);

        var units = (await client.GetFromJsonAsync<LevelUnitsDto>("/api/levels/A/units", ApiFactory.Json))!;
        var words = await client.GetAsync($"/api/units/{second}");
        var open = await client.PostAsync($"/api/units/{second}/open", null);
        var drill = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true, second), ApiFactory.Json);
        var today = (await client.GetFromJsonAsync<TodayDto>("/api/today", ApiFactory.Json))!;

        Assert.Equal([(1, UnitState.Open), (2, UnitState.Locked)], units.Units.Select(u => (u.Number, u.State)));
        Assert.Equal(HttpStatusCode.Forbidden, words.StatusCode);
        Assert.Contains("A-1-bo‘limni tugating", await words.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, open.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, drill.StatusCode);
        Assert.Equal("A-1", today.CurrentUnit?.Label);
        Assert.Equal(0, today.NewAvailable); // no cards before the unit is opened
        Assert.Contains("Asosiy fe’l", units.Units[0].Themes);
    }

    [Fact]
    public async Task Opening_a_unit_creates_its_cards_once()
    {
        await using var factory = Seeded();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var first = await UnitIdAsync(factory, 1);

        var before = (await client.GetFromJsonAsync<UnitDto>($"/api/units/{first}", ApiFactory.Json))!;
        var opened = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => client.PostAsync($"/api/units/{first}/open", null)));
        var again = (await (await client.PostAsync($"/api/units/{first}/open", null)).Content.ReadFromJsonAsync<UnitDto>(ApiFactory.Json))!;
        var today = (await client.GetFromJsonAsync<TodayDto>("/api/today", ApiFactory.Json))!;

        Assert.False(before.CardsReady);
        Assert.All(opened, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.True(again.CardsReady);
        Assert.Equal(25, again.Words.Count);
        Assert.Equal(("하다", "qilmoq"), (again.Words[0].Word, again.Words[0].TranslationUz));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(50, await db.Cards.CountAsync()); // 25 words × 2 directions, however often it was opened
        Assert.Equal(50, today.NewAvailable);
    }

    [Fact]
    public async Task Passing_a_unit_opens_the_next_one()
    {
        await using var factory = Seeded();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await client.GetAsync("/api/levels/A/units");
        var (first, second) = (await UnitIdAsync(factory, 1), await UnitIdAsync(factory, 2));

        await PassAsync(factory, await UserIdAsync(factory, ApiFactory.UserA), first);
        var units = (await client.GetFromJsonAsync<LevelUnitsDto>("/api/levels/A/units", ApiFactory.Json))!;
        var words = await client.GetAsync($"/api/units/{second}");
        var other = (await (await factory.SignedInClientAsync(ApiFactory.UserB)).GetFromJsonAsync<LevelUnitsDto>("/api/levels/A/units", ApiFactory.Json))!;

        Assert.Equal([(UnitState.Passed, (int?)92), (UnitState.Open, null)], units.Units.Select(u => (u.State, u.BestScorePercent)));
        Assert.Equal(HttpStatusCode.OK, words.StatusCode);
        Assert.Equal([UnitState.Open, UnitState.Locked], other.Units.Select(u => u.State)); // someone else's progress is their own
    }

    [Fact]
    public async Task Daily_review_brings_due_cards_of_earlier_units_and_new_words_only_from_the_current_one()
    {
        await using var factory = new ApiFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var userId = await UserIdAsync(factory, ApiFactory.UserA);
        await factory.AddWordsAsync(ApiFactory.UserA, 1, ApiFactory.Words("가", 3));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await ApiFactory.PublishUnitAsync(scope.ServiceProvider, 2, ApiFactory.Words("나", 3));
            await ApiFactory.PublishUnitAsync(scope.ServiceProvider, 3, ApiFactory.Words("다", 3));
        }

        // Unit 1 is learned on the cards, then passed; unit 2 is passed too ("Bilaman") and unit 3 is opened.
        var drill = (await (await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true, ApiFactory.Unit(1)), ApiFactory.Json))
            .Content.ReadFromJsonAsync<StartSessionResponse>(ApiFactory.Json))!.Session!;
        while (drill.Current is { } card)
        {
            drill = (await (await client.PostAsJsonAsync($"/api/sessions/{drill.Id}/answers",
                new AnswerRequest(Guid.NewGuid(), drill.Position, ReviewAnswer.Easy, false, 3000), ApiFactory.Json))
                .Content.ReadFromJsonAsync<AnswerResultDto>(ApiFactory.Json))!.Session;
        }

        await PassAsync(factory, userId, ApiFactory.Unit(1));
        await PassAsync(factory, userId, ApiFactory.Unit(2));
        (await client.PostAsync($"/api/units/{ApiFactory.Unit(3)}/open", null)).EnsureSuccessStatusCode();
        factory.Time.Advance(TimeSpan.FromDays(3)); // unit 1's cards are due again
        client = await factory.SignedInClientAsync(ApiFactory.UserA); // the old access token has expired

        var today = (await client.GetFromJsonAsync<TodayDto>("/api/today", ApiFactory.Json))!;
        var review = (await (await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true), ApiFactory.Json))
            .Content.ReadFromJsonAsync<StartSessionResponse>(ApiFactory.Json))!.Session!;
        var units = new List<string?>();
        var kinds = new List<Lugat.Domain.Study.SessionItemKind>();
        while (review.Current is { } card)
        {
            (units, kinds) = ([.. units, card.Unit], [.. kinds, card.Kind]);
            review = (await (await client.PostAsJsonAsync($"/api/sessions/{review.Id}/answers",
                new AnswerRequest(Guid.NewGuid(), review.Position, ReviewAnswer.Easy, false, 3000), ApiFactory.Json))
                .Content.ReadFromJsonAsync<AnswerResultDto>(ApiFactory.Json))!.Session;
        }

        Assert.Equal("A-3", today.CurrentUnit?.Label);
        Assert.Equal(6, today.DueNow); // unit 1: 3 words × 2
        Assert.Equal(6, today.NewAvailable); // unit 3 only
        Assert.Equal(6, units.Count(u => u == "A-1")); // a learner on unit 3 still reviews unit 1
        Assert.Equal(6, units.Count(u => u == "A-3"));
        Assert.DoesNotContain("A-2", units); // passed without studying: no cards, no new words from it
    }

    [Fact]
    public async Task Correcting_a_published_word_updates_what_learners_practise()
    {
        await using var factory = new ApiFactory { AdminTelegramIds = [ApiFactory.UserB], Settings = { ["Catalog:SeedSample"] = "true" } };
        var admin = await factory.SignedInClientAsync(ApiFactory.UserB);
        var unit = (await admin.GetFromJsonAsync<UnitAdminDto>($"/api/admin/units/{await UnitIdAsync(factory, 1)}", ApiFactory.Json))!;
        var word = unit.Words[0];

        var saved = await admin.PutAsJsonAsync($"/api/admin/catalog/words/{word.Id}", new UpdateCatalogWordRequest(
            "qilmoq; bajarmoq", word.Romanization, word.ExampleKo, word.ExampleUz, word.TrapUz, word.Theme, null, word.Version), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var item = await scope.ServiceProvider.GetRequiredService<AppDbContext>().LearningItems.SingleAsync(i => i.CatalogWordId == word.Id);
        Assert.Equal(("qilmoq; bajarmoq", 2), (item.Uzbek, item.ContentVersion));
    }
}
