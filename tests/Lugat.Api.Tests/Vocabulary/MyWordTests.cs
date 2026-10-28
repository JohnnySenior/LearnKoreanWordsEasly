using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Vocabulary;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Vocabulary;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Vocabulary;

/// <summary>"Mening so‘zlarim" (L-51): own words, looked up in the catalog first, written by the AI only when missing.</summary>
public class MyWordTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FakeAiProvider _ai = new(AiProviderKind.OpenAi);

    private static readonly (string, string)[] UnitTwo = [("병원", "kasalxona; shifoxona"), ("약", "dori")];

    private ApiFactory NewFactory(bool premium = true) => new() { AiProviders = [_ai], Premium = premium };

    private static string Card(string korean, string uzbek, string? trap = null) => $$"""
        {"korean":"{{korean}}","translationUz":"{{uzbek}}","romanization":"x","exampleKo":"저는 {{korean}} 좋아요.","exampleUz":"Misol gap.","trapUz":{{(trap is null ? "null" : $"\"{trap}\"")}}}
        """;

    private static string Words(params string[] cards) => $$"""{"words":[{{string.Join(",", cards)}}]}""";

    private static async Task<MyWordLookupDto> LookupAsync(HttpClient client, string text)
    {
        var response = await client.PostAsJsonAsync("/api/my-words/lookup", new MyWordLookupRequest(text), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MyWordLookupDto>(Json))!;
    }

    private static AddMyWordRequest Add(MyWordCandidateDto c, Guid? deck = null) =>
        new(Guid.NewGuid(), c.Korean, c.Uzbek, c.Romanization, c.ExampleKo, c.ExampleUz, c.TrapUz, c.Source, c.CatalogWordId, deck);

    private static async Task<MyWordDto> AddAsync(HttpClient client, AddMyWordRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/my-words", request, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MyWordDto>(Json))!;
    }

    private static async Task<HttpClient> SignedInWithUnitAsync(ApiFactory factory)
    {
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await using var scope = factory.Services.CreateAsyncScope();
        await ApiFactory.PublishUnitAsync(scope.ServiceProvider, 2, UnitTwo);
        return client;
    }

    [Fact]
    public async Task A_catalog_word_is_found_without_the_ai_in_korean_and_in_uzbek_and_joins_the_daily_review()
    {
        _ai.Answer = _ => Words(Card("치유", "shifo"));
        await using var factory = NewFactory();
        var client = await SignedInWithUnitAsync(factory);

        var korean = await LookupAsync(client, "병원");
        var uzbek = await LookupAsync(client, "shifoxona"); // the second meaning, as a whole meaning only
        var part = await client.PostAsJsonAsync("/api/my-words/lookup", new MyWordLookupRequest("shifo"), Json);

        Assert.False(korean.UsedAi);
        var found = Assert.Single(korean.Candidates);
        Assert.Equal((PersonalWordSource.Catalog, "A-2"), (found.Source, found.Unit));
        Assert.Equal("병원", Assert.Single(uzbek.Candidates).Korean);
        Assert.DoesNotContain(_ai.Requests, r => r.UserContent.Contains("병원"));
        Assert.Equal(HttpStatusCode.OK, part.StatusCode); // "shifo" is not a meaning: the AI was asked
        Assert.Single(_ai.Requests);

        var added = await AddAsync(client, Add(found));
        var again = await LookupAsync(client, "병원");
        var today = (await client.GetFromJsonAsync<TodayDto>("/api/today", Json))!;
        var start = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true), Json);
        var session = (await start.Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;

        Assert.Equal((MyWordState.New, "kasalxona; shifoxona"), (added.State, added.Uzbek));
        Assert.True(Assert.Single(again.Candidates).AlreadyAdded);
        Assert.Equal(2, today.NewAvailable); // its unit is not opened, the own word still comes today
        Assert.Equal(2, session.Total);
        Assert.Contains("병원", new[] { session.Current!.Prompt, session.Current.Answer });
    }

    [Fact]
    public async Task Adding_is_idempotent_and_the_same_word_twice_is_refused()
    {
        await using var factory = NewFactory();
        var client = await SignedInWithUnitAsync(factory);
        var found = (await LookupAsync(client, "약")).Candidates[0];
        var request = Add(found);

        var first = await AddAsync(client, request);
        var retry = await AddAsync(client, request);
        var twice = await client.PostAsJsonAsync("/api/my-words", Add(found), Json);
        var list = (await client.GetFromJsonAsync<MyWordsDto>("/api/my-words", Json))!;

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Single(list.Words);
    }

    [Fact]
    public async Task A_free_learner_gets_three_ai_words_a_day_and_catalog_words_are_unlimited()
    {
        _ai.Answer = r => Words(Card("감자", "kartoshka"));
        await using var factory = NewFactory(premium: false);
        var client = await SignedInWithUnitAsync(factory);

        var left = new List<int?>();
        foreach (var text in new[] { "kartoshka", "piyoz", "sabzi" })
        {
            left.Add((await LookupAsync(client, text)).AiLeftToday);
        }

        var fourth = await client.PostAsJsonAsync("/api/my-words/lookup", new MyWordLookupRequest("olma"), Json);
        var catalog = await LookupAsync(client, "dori");
        factory.Time.Advance(TimeSpan.FromDays(1));
        client = await factory.SignedInClientAsync(ApiFactory.UserA); // the access token has expired meanwhile
        var tomorrow = await LookupAsync(client, "olma");

        Assert.Equal(new int?[] { 2, 1, 0 }, left);
        Assert.Equal((HttpStatusCode)429, fourth.StatusCode);
        Assert.Contains("Premium", await fourth.Content.ReadAsStringAsync());
        Assert.Equal((false, 0), (catalog.UsedAi, catalog.AiLeftToday));
        Assert.Equal(2, tomorrow.AiLeftToday);
        Assert.Equal(4, _ai.Requests.Count);
    }

    [Fact]
    public async Task Premium_has_no_ai_limit_and_a_failed_ai_call_is_not_counted()
    {
        _ai.Answer = _ => Words(Card("감자", "kartoshka"));
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        for (var i = 0; i < 4; i++)
        {
            Assert.Null((await LookupAsync(client, "kartoshka")).AiLeftToday);
        }

        _ai.Answer = _ => """{"words":[{"korean":"no hangul","translationUz":"x","romanization":"x","exampleKo":"x","exampleUz":"x","trapUz":null}]}""";
        var broken = await client.PostAsJsonAsync("/api/my-words/lookup", new MyWordLookupRequest("piyoz"), Json);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, broken.StatusCode);
    }

    [Fact]
    public async Task An_ai_word_is_checked_shows_its_trap_and_at_most_ten_new_own_words_come_a_day()
    {
        _ai.Answer = r => Words(Card("월급", "oylik maosh", "Maosh — 월급; 봉급 kam ishlatiladi."), Card("급여", "ish haqi"));
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var lookup = await LookupAsync(client, "oylik");
        var cyrillic = await client.PostAsJsonAsync("/api/my-words", Add(lookup.Candidates[0]) with { Uzbek = "ойлик" }, Json);
        var salary = await AddAsync(client, Add(lookup.Candidates[0]));
        var start = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true), Json);
        var session = (await start.Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;

        Assert.True(lookup.UsedAi);
        Assert.Equal(2, lookup.Candidates.Count);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cyrillic.StatusCode);
        Assert.Equal(PersonalWordSource.Ai, salary.Source);
        Assert.Contains("<uzbek>oylik</uzbek>", _ai.Requests[0].UserContent);
        Assert.Contains("봉급", session.Current!.UsageTrap);

        for (var i = 0; i < 10; i++)
        {
            var c = lookup.Candidates[0] with { Korean = $"단어{i}" };
            await AddAsync(client, Add(c));
        }

        var list = (await client.GetFromJsonAsync<MyWordsDto>("/api/my-words", Json))!;
        var today = (await client.GetFromJsonAsync<TodayDto>("/api/today", Json))!;
        var day = DateOnly.FromDateTime(ApiFactory.Start.UtcDateTime);

        Assert.Equal(10, list.Words.Count(w => w.IntroduceOn == day));
        Assert.Equal(day.AddDays(1), list.Words.Single(w => w.IntroduceOn != day).IntroduceOn);
        Assert.Equal(20, today.NewAvailable); // two cards for each of today's ten
    }

    [Fact]
    public async Task Decks_hold_words_can_be_renamed_and_deleting_one_keeps_its_words()
    {
        _ai.Answer = _ => Words(Card("감자", "kartoshka"), Card("양파", "piyoz"));
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var other = await factory.SignedInClientAsync(ApiFactory.UserB);
        var candidates = (await LookupAsync(client, "sabzavot")).Candidates;

        var deck = (await (await client.PostAsJsonAsync("/api/word-decks", new DeckNameRequest(" Bozor "), Json)).Content.ReadFromJsonAsync<WordDeckDto>(Json))!;
        var duplicate = await client.PostAsJsonAsync("/api/word-decks", new DeckNameRequest("Bozor"), Json);
        var potato = await AddAsync(client, Add(candidates[0], deck.Id));
        var onion = await AddAsync(client, Add(candidates[1]));
        var foreignDeck = await other.PostAsJsonAsync("/api/my-words", Add(candidates[0], deck.Id), Json);
        var inDeck = (await client.GetFromJsonAsync<MyWordsDto>($"/api/my-words?deck={deck.Id}", Json))!;
        var general = (await client.GetFromJsonAsync<MyWordsDto>("/api/my-words?deck=umumiy", Json))!;
        var search = (await client.GetFromJsonAsync<MyWordsDto>("/api/my-words?q=PIYOZ", Json))!;

        Assert.Equal("Bozor", deck.Name);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignDeck.StatusCode);
        Assert.Equal(potato.Id, Assert.Single(inDeck.Words).Id);
        Assert.Equal(onion.Id, Assert.Single(general.Words).Id);
        Assert.Equal((1, 1), (inDeck.Decks.Single().Words, inDeck.Unsorted));
        Assert.Equal(onion.Id, Assert.Single(search.Words).Id);

        var moved = (await (await client.PutAsJsonAsync($"/api/my-words/{onion.Id}/deck", new MoveMyWordRequest(deck.Id), Json)).Content.ReadFromJsonAsync<MyWordDto>(Json))!;
        var renamed = (await (await client.PutAsJsonAsync($"/api/word-decks/{deck.Id}", new DeckNameRequest("Do‘kon"), Json)).Content.ReadFromJsonAsync<WordDeckDto>(Json))!;
        var foreignDelete = await other.DeleteAsync($"/api/word-decks/{deck.Id}");
        var deleted = await client.DeleteAsync($"/api/word-decks/{deck.Id}");
        var after = (await client.GetFromJsonAsync<MyWordsDto>("/api/my-words", Json))!;

        Assert.Equal(deck.Id, moved.DeckId);
        Assert.Equal(("Do‘kon", 2), (renamed.Name, renamed.Words));
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(after.Decks);
        Assert.Equal(2, after.Unsorted);
        Assert.All(after.Words, w => Assert.Null(w.DeckId));
    }

    [Fact]
    public async Task A_removed_own_word_leaves_the_review_and_another_learner_cannot_touch_it()
    {
        _ai.Answer = _ => Words(Card("감자", "kartoshka"));
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var other = await factory.SignedInClientAsync(ApiFactory.UserB);
        var word = await AddAsync(client, Add((await LookupAsync(client, "kartoshka")).Candidates[0]));

        var foreign = await other.DeleteAsync($"/api/my-words/{word.Id}");
        var removed = await client.DeleteAsync($"/api/my-words/{word.Id}");
        var today = (await client.GetFromJsonAsync<TodayDto>("/api/today", Json))!;
        var list = (await client.GetFromJsonAsync<MyWordsDto>("/api/my-words", Json))!;
        var readd = await client.PostAsJsonAsync("/api/my-words", Add((await LookupAsync(client, "kartoshka")).Candidates[0]), Json);

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(0, today.NewAvailable);
        Assert.Empty(list.Words);
        Assert.Equal(HttpStatusCode.OK, readd.StatusCode); // it can be added again later
    }

    [Fact]
    public async Task Exercises_can_be_built_from_own_words_by_deck_and_the_unit_list_is_unchanged()
    {
        string[] words = ["감자", "양파", "당근", "오이", "배추"];
        _ai.Answer = r => r.Feature == PersonalWordService.Feature ? Words(words.Select(w => Card(w, "sabzavot " + w)).Take(3).ToArray()) : "{}";
        await using var factory = NewFactory();
        var client = await SignedInWithUnitAsync(factory);
        var noDecks = (await client.GetFromJsonAsync<TestOptionsDto>("/api/tests/options", Json))!;
        var deck = (await (await client.PostAsJsonAsync("/api/word-decks", new DeckNameRequest("Bozor"), Json)).Content.ReadFromJsonAsync<WordDeckDto>(Json))!;
        foreach (var c in (await LookupAsync(client, "sabzavot")).Candidates)
        {
            await AddAsync(client, Add(c, deck.Id));
        }

        foreach (var w in words[3..])
        {
            await AddAsync(client, Add(new MyWordCandidateDto(w, "sabzavot " + w, "x", $"저는 {w} 좋아요.", "Misol gap.", null, PersonalWordSource.Ai, null, null, false)));
        }

        var options = (await client.GetFromJsonAsync<TestOptionsDto>("/api/tests/options", Json))!;
        var test = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [], 10, Decks: [deck.Id.ToString(), PersonalWordService.Unsorted]), Json);
        var oral = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), null, 0, Oral: true, Decks: [deck.Id.ToString()]), Json);
        var nothing = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [], 10, Decks: []), Json);
        var writing = (await client.GetFromJsonAsync<List<WritingPromptDto>>($"/api/writing/queue?count=30&deck={PersonalWordService.Unsorted}", Json))!;

        Assert.Empty(noDecks.Decks!);
        Assert.Empty(options.Tables); // own words never make a unit appear
        Assert.Equal(new[] { ("Umumiy", 2), ("Bozor", 3) }, options.Decks!.Select(d => (d.Name, d.Words)));
        Assert.Equal(HttpStatusCode.Created, test.StatusCode);
        var vocabulary = (await test.Content.ReadFromJsonAsync<TestDto>(Json))!;
        Assert.Equal("Mening so‘zlarim", vocabulary.Title);
        Assert.Equal(HttpStatusCode.Created, oral.StatusCode);
        var spoken = (await oral.Content.ReadFromJsonAsync<TestDto>(Json))!;
        Assert.Equal("Talaffuz · Mening so‘zlarim", spoken.Title);
        Assert.Equal(3, spoken.Questions.Count); // only the "Bozor" deck
        Assert.Equal(HttpStatusCode.UnprocessableEntity, nothing.StatusCode);
        Assert.Equal(2, writing.Count); // the two words in "Umumiy", each once
        Assert.All(writing, p => Assert.Contains(p.Prompt, new[] { "오이", "배추", "sabzavot 오이", "sabzavot 배추" }));
    }

    [Theory]
    [InlineData("""{"words":[{"korean":"감자","translationUz":"картошка","romanization":"gamja","exampleKo":"감자 좋아요.","exampleUz":"Kartoshka yaxshi.","trapUz":null}]}""")]
    [InlineData("""{"words":[{"korean":"potato","translationUz":"kartoshka","romanization":"gamja","exampleKo":"감자 좋아요.","exampleUz":"Kartoshka yaxshi.","trapUz":null}]}""")]
    [InlineData("""{"words":[{"korean":"감자","translationUz":"kartoshka","romanization":"gamja","exampleKo":"","exampleUz":"Kartoshka yaxshi.","trapUz":null}]}""")]
    [InlineData("not json")]
    public void An_ai_card_that_breaks_the_content_rules_is_dropped(string json) =>
        Assert.Empty(PersonalWordService.Parse(json));

    [Fact]
    public void At_most_ten_new_own_words_are_planned_for_one_day()
    {
        var today = new DateOnly(2026, 10, 28);
        var perDay = new Dictionary<DateOnly, int> { [today] = 10, [today.AddDays(1)] = 9 };

        Assert.Equal(today.AddDays(1), PersonalWord.NextFreeDay(today, perDay));
        Assert.Equal(today, PersonalWord.NextFreeDay(today, new Dictionary<DateOnly, int>()));
    }
}
