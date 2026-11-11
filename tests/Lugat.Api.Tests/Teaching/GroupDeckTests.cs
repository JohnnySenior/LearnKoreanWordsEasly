using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Catalog;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Teaching;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Premium;
using Lugat.Domain.Teaching;
using Lugat.Domain.Users;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Teaching;

/// <summary>
/// L-67: a teacher's own vocabulary list for a group — uploaded as two columns, matched against the catalog, filled
/// by the AI where the catalog has nothing, and given to every member as cards. The learners pay nothing.
/// </summary>
public class GroupDeckTests
{
    private const long Teacher = 6001;
    private const long OtherTeacher = 6002;
    private const long LearnerA = 6003;
    private const long LearnerB = 6004;
    private const long Latecomer = 6005;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FakeAiProvider _ai = new(AiProviderKind.OpenAi);

    private ApiFactory NewFactory() => new() { Premium = false, AiProviders = [_ai] };

    private static async Task<T> OkAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(ApiFactory.Json))?.Detail;
    }

    /// <summary>A-1 published with three words; the teacher with the Teacher role; a group; the learners who joined.</summary>
    private static async Task<(HttpClient Teacher, TeacherGroupDto Group)> SetupAsync(ApiFactory factory, params long[] learners)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await ApiFactory.PublishUnitAsync(scope.ServiceProvider, 1, [("학교", "maktab"), ("물", "suv"), ("사랑", "sevgi")]);
        }

        var teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
        await TeachingTests.GrantAsync(factory, Teacher, UserRoles.Teacher);
        var group = await OkAsync<TeacherGroupDto>(await teacher.PostAsJsonAsync("/api/teacher/groups", new CreateGroupRequest(Guid.NewGuid(), "Ansan"), ApiFactory.Json));
        foreach (var id in learners)
        {
            var learner = await factory.SignedInClientAsync(id, $"O‘quvchi {id}");
            await OkAsync<MyGroupDto>(await learner.PostAsync($"/api/groups/join/{group.InviteCode}", null));
        }

        return (teacher, group);
    }

    private static async Task GivePremiumAsync(ApiFactory factory, long telegramId)
    {
        var userId = await TeachingTests.UserIdAsync(factory, telegramId);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = factory.Time.GetUtcNow();
        db.Subscriptions.Add(new Subscription { Id = Guid.CreateVersion7(), UserId = userId, Plan = SubscriptionPlan.Monthly, StartsAt = now.AddDays(-1), EndsAt = now.AddDays(29), Source = SubscriptionSource.Admin, CreatedAt = now });
        await db.SaveChangesAsync();
    }

    private static async Task<GroupDeckDto> CreateDeckAsync(HttpClient teacher, Guid groupId, string name = "3-dars") =>
        await OkAsync<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/groups/{groupId}/decks", new CreateGroupDeckRequest(Guid.NewGuid(), name), ApiFactory.Json));

    private static Task<HttpResponseMessage> ImportAsync(HttpClient teacher, Guid deckId, byte[] file, string name = "lugat.xlsx") =>
        teacher.PostAsync($"/api/teacher/decks/{deckId}/import", CatalogFiles.Upload(file, name));

    private static string Cards(params (string Korean, string Trap)[] words) => JsonSerializer.Serialize(new
    {
        cards = words.Select(w => new
        {
            korean = w.Korean, romanization = "romani", exampleKo = $"{w.Korean} 있어요.", exampleUz = "Bor.", trapUz = w.Trap, trapCue = (string?)null, trapWrong = (string?)null, trapRight = (string?)null,
        }),
    }, Json);

    [Fact]
    public async Task The_file_is_matched_against_the_catalog_filled_by_the_ai_and_published_to_the_group()
    {
        await using var factory = NewFactory();
        var (teacher, group) = await SetupAsync(factory, LearnerA, LearnerB);
        var learnerA = await factory.SignedInClientAsync(LearnerA);
        var deck = await CreateDeckAsync(teacher, group.Id);

        var file = CatalogFiles.XlsxOf(
        [
            ["Koreyscha", "O‘zbekcha"],
            ["학교", "maktab"],                     // in the catalog with this meaning: the course's item is shared
            ["물", "ichimlik"],                     // in the catalog with another meaning: a copy with the teacher's text
            ["회사원", "kompaniya xodimi"],         // not in the catalog: the AI fills it
            ["사과 - olma"],                         // one cell
            ["학교", "o‘quv yurti"],                 // repeated
            ["faqat o‘zbekcha"],
        ]);
        var import = await OkAsync<GroupDeckImportDto>(await ImportAsync(teacher, deck.Id, file));
        Assert.Equal((4, 2), (import.Added, import.Skipped));
        Assert.Contains(import.Problems, p => p.Contains("takrorlangan"));
        Assert.Contains(import.Problems, p => p.Contains("koreyscha so‘z topilmadi"));

        var words = import.Deck.Words;
        Assert.Equal(["학교", "물", "회사원", "사과"], words.Select(w => w.Korean));
        Assert.Equal((GroupWordSource.Catalog, true, "A-1", "maktab"), (words[0].Source, words[0].Shared, words[0].CatalogUnit, words[0].Uzbek));
        Assert.Equal((GroupWordSource.Catalog, false, "A-1", "ichimlik"), (words[1].Source, words[1].Shared, words[1].CatalogUnit, words[1].Uzbek));
        Assert.Equal((GroupWordSource.Teacher, false, null), (words[2].Source, words[2].Shared, words[2].CatalogUnit));
        Assert.Equal(2, import.Deck.AiPending);
        Assert.False(import.Deck.CanPublish);

        // Importing the same file again adds nothing: every word is already there.
        var again = await OkAsync<GroupDeckImportDto>(await ImportAsync(teacher, deck.Id, file));
        Assert.Equal((0, 6), (again.Added, again.Skipped));

        // Without the paid plan neither the AI nor publishing works; the learners see nothing yet.
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/fill", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/publish", null)).StatusCode);
        Assert.Empty((await learnerA.GetFromJsonAsync<MyWordsDto>("/api/my-words", ApiFactory.Json))!.GroupDecks ?? []);

        await GivePremiumAsync(factory, Teacher);
        _ai.Answer = _ => Cards(("회사원", "O‘zbekcha «kompaniya ishchisi» deb 회사 일꾼 deyilmaydi."), ("사과", null!));
        var fill = await OkAsync<GroupDeckFillDto>(await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/fill", null));
        Assert.Equal((2, 0, 0), (fill.Filled, fill.Left, fill.Deck.AiPending));
        var request = Assert.Single(_ai.Requests);
        Assert.Equal(GroupDeckService.Feature, request.Feature);
        Assert.Contains("korean=\"회사원\" uzbek=\"kompaniya xodimi\"", request.UserContent); // the teacher's translation goes along
        Assert.DoesNotContain("학교", request.UserContent); // catalog words are not sent
        var filled = fill.Deck.Words.Single(w => w.Korean == "회사원");
        Assert.Equal((GroupWordSource.Ai, "romani", "kompaniya xodimi"), (filled.Source, filled.Romanization, filled.Uzbek));
        Assert.StartsWith("O‘zbekcha «kompaniya ishchisi»", filled.TrapUz);

        var published = await OkAsync<GroupDeckDto>(await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/publish", null));
        Assert.Equal(GroupDeckStatus.Published, published.Status);
        Assert.Equal(GroupDeckStatus.Published, (await OkAsync<GroupDeckDto>(await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/publish", null))).Status); // again: no change
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.PostAsJsonAsync($"/api/teacher/decks/{deck.Id}/words", new AddGroupWordRequest("책", "kitob"), ApiFactory.Json)).StatusCode);

        // Every member has the deck: the shared course item for 학교, a copy with "ichimlik" for 물.
        var key = GroupDeckService.Key(deck.Id);
        var mine = (await learnerA.GetFromJsonAsync<MyWordsDto>($"/api/my-words?deck={Uri.EscapeDataString(key)}", ApiFactory.Json))!;
        var option = Assert.Single(mine.GroupDecks!);
        Assert.Equal((key, "3-dars", "Ansan", 4), (option.Key, option.Name, option.GroupName, option.Words));
        Assert.Equal(4, mine.Words.Count);
        Assert.All(mine.Words, w => Assert.Equal(deck.Id, w.GroupDeckId));
        Assert.Equal(0, mine.Unsorted); // not "Umumiy": they are the teacher's
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var schoolItem = await db.LearningItems.SingleAsync(i => i.Korean == "학교" && i.CatalogWordId != null);
            Assert.Equal(schoolItem.Id, mine.Words.Single(w => w.Korean == "학교").LearningItemId);
            Assert.Equal("ichimlik", mine.Words.Single(w => w.Korean == "물").Uzbek);
            var learnerId = await TeachingTests.UserIdAsync(factory, LearnerA);
            var learnerBId = await TeachingTests.UserIdAsync(factory, LearnerB);
            Assert.Equal(8, await db.Cards.CountAsync(c => c.OwnerId == learnerId)); // two cards per word
            Assert.Equal(4, await db.PersonalWords.CountAsync(p => p.OwnerId == learnerBId && p.GroupDeckId == deck.Id));
        }

        // The deck is a choice in the exercises, and a card session can be started from it.
        var options = await learnerA.GetFromJsonAsync<TestOptionsDto>("/api/tests/options", ApiFactory.Json);
        Assert.Contains(options!.Decks!, d => d.Key == key && d.Name == "3-dars (Ansan)" && d.Words == 4);
        var session = await OkAsync<StartSessionResponse>(await learnerA.PostAsJsonAsync("/api/sessions",
            new StartSessionRequest(null, true, LearningItemIds: mine.Words.Select(w => w.LearningItemId).ToList()), ApiFactory.Json));
        Assert.NotNull(session.Session);

        // The learner cannot remove or move the teacher's word.
        var word = mine.Words.First();
        Assert.Contains("o‘qituvchingiz", await DetailAsync(await learnerA.DeleteAsync($"/api/my-words/{word.Id}"), HttpStatusCode.Conflict));
        Assert.Equal(HttpStatusCode.Conflict, (await learnerA.PutAsJsonAsync($"/api/my-words/{word.Id}/deck", new MoveMyWordRequest(null), ApiFactory.Json)).StatusCode);

        // A learner who joins later gets it too; one who leaves loses it, the shared course item untouched.
        var latecomer = await factory.SignedInClientAsync(Latecomer, "Kech kelgan");
        await OkAsync<MyGroupDto>(await latecomer.PostAsync($"/api/groups/join/{group.InviteCode}", null));
        Assert.Equal(4, (await latecomer.GetFromJsonAsync<MyWordsDto>("/api/my-words", ApiFactory.Json))!.Words.Count);
        Assert.Equal(HttpStatusCode.NoContent, (await latecomer.DeleteAsync($"/api/groups/{group.Id}/membership")).StatusCode);
        Assert.Empty((await latecomer.GetFromJsonAsync<MyWordsDto>("/api/my-words", ApiFactory.Json))!.Words);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lateId = await TeachingTests.UserIdAsync(factory, Latecomer);
            Assert.True(await db.LearningItems.AnyAsync(i => i.Korean == "학교" && i.CatalogWordId != null && i.IsActive));
            Assert.False(await db.LearningItems.AnyAsync(i => i.OwnerId == lateId && i.IsActive)); // the copies stopped
            Assert.Equal(8, await db.Cards.CountAsync(c => c.OwnerId == lateId)); // history kept
        }

        // The teacher takes the deck back: gone from the remaining learners too.
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/teacher/decks/{deck.Id}")).StatusCode);
        Assert.Empty((await learnerA.GetFromJsonAsync<MyWordsDto>("/api/my-words", ApiFactory.Json))!.Words);
        Assert.Empty(await OkAsync<List<GroupDeckSummaryDto>>(await teacher.GetAsync($"/api/teacher/groups/{group.Id}/decks")));
    }

    [Fact]
    public async Task A_word_the_learner_already_had_is_linked_not_doubled_and_stays_when_the_deck_goes()
    {
        await using var factory = NewFactory();
        var (teacher, group) = await SetupAsync(factory, LearnerA);
        await GivePremiumAsync(factory, Teacher);
        var learner = await factory.SignedInClientAsync(LearnerA);
        var learnerId = await TeachingTests.UserIdAsync(factory, LearnerA);

        // The learner added 사랑 from the catalog themselves (L-51).
        var lookup = await OkAsync<MyWordLookupDto>(await learner.PostAsJsonAsync("/api/my-words/lookup", new MyWordLookupRequest("사랑"), ApiFactory.Json));
        var c = lookup.Candidates.Single();
        var own = await OkAsync<MyWordDto>(await learner.PostAsJsonAsync("/api/my-words",
            new AddMyWordRequest(Guid.NewGuid(), c.Korean, c.Uzbek, c.Romanization, c.ExampleKo, c.ExampleUz, c.TrapUz, c.Source, c.CatalogWordId, null), ApiFactory.Json));

        var deck = await CreateDeckAsync(teacher, group.Id);
        await OkAsync<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/decks/{deck.Id}/words", new AddGroupWordRequest("사랑", "sevgi"), ApiFactory.Json));
        await OkAsync<GroupDeckDto>(await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/publish", null));

        var mine = (await learner.GetFromJsonAsync<MyWordsDto>("/api/my-words", ApiFactory.Json))!;
        var word = Assert.Single(mine.Words);
        Assert.Equal((own.Id, deck.Id, 1), (word.Id, word.GroupDeckId, mine.Unsorted)); // the same row, now in the deck too, still "Umumiy"
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Cards.CountAsync(x => x.OwnerId == learnerId));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/teacher/decks/{deck.Id}")).StatusCode);
        var after = Assert.Single((await learner.GetFromJsonAsync<MyWordsDto>("/api/my-words", ApiFactory.Json))!.Words);
        Assert.Equal((own.Id, (Guid?)null), (after.Id, after.GroupDeckId)); // their own word stays
    }

    [Fact]
    public async Task Another_teacher_cannot_see_the_deck_and_a_closed_group_takes_its_decks_back()
    {
        await using var factory = NewFactory();
        var (teacher, group) = await SetupAsync(factory, LearnerA);
        await GivePremiumAsync(factory, Teacher);
        var other = await factory.SignedInClientAsync(OtherTeacher, "Boshqa");
        await TeachingTests.GrantAsync(factory, OtherTeacher, UserRoles.Teacher);
        var learner = await factory.SignedInClientAsync(LearnerA);

        var deck = await CreateDeckAsync(teacher, group.Id);
        await OkAsync<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/decks/{deck.Id}/words", new AddGroupWordRequest("학교", "maktab"), ApiFactory.Json));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/teacher/decks/{deck.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/teacher/groups/{group.Id}/decks")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/teacher/decks/{deck.Id}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync($"/api/teacher/decks/{deck.Id}")).StatusCode);

        await OkAsync<GroupDeckDto>(await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/publish", null));
        Assert.Single((await learner.GetFromJsonAsync<MyWordsDto>("/api/my-words", ApiFactory.Json))!.Words);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/teacher/groups/{group.Id}")).StatusCode);
        Assert.Empty((await learner.GetFromJsonAsync<MyWordsDto>("/api/my-words", ApiFactory.Json))!.Words);
    }

    [Fact]
    public async Task Edits_are_checked_and_a_changed_word_is_matched_again()
    {
        await using var factory = NewFactory();
        var (teacher, group) = await SetupAsync(factory);
        var deck = await CreateDeckAsync(teacher, group.Id);
        var added = await OkAsync<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/decks/{deck.Id}/words", new AddGroupWordRequest("책", "kitob"), ApiFactory.Json));
        var word = Assert.Single(added.Words);
        Assert.Equal(GroupWordSource.Teacher, word.Source);

        Assert.Contains("Hangul", await DetailAsync(await teacher.PostAsJsonAsync($"/api/teacher/decks/{deck.Id}/words", new AddGroupWordRequest("kitob", "kitob"), ApiFactory.Json), HttpStatusCode.UnprocessableEntity));
        Assert.Contains("lotin", await DetailAsync(await teacher.PostAsJsonAsync($"/api/teacher/decks/{deck.Id}/words", new AddGroupWordRequest("물", "сув"), ApiFactory.Json), HttpStatusCode.UnprocessableEntity));
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.PostAsJsonAsync($"/api/teacher/decks/{deck.Id}/words", new AddGroupWordRequest("책", "daftar"), ApiFactory.Json)).StatusCode);

        // The teacher changes the word to one the catalog has: it is matched and takes the catalog's content.
        var updated = await OkAsync<GroupDeckDto>(await teacher.PutAsJsonAsync($"/api/teacher/decks/{deck.Id}/words/{word.Id}",
            new UpdateGroupWordRequest("학교", "maktab", null, null, null, null), ApiFactory.Json));
        Assert.Equal((GroupWordSource.Catalog, true, "A-1"), (updated.Words[0].Source, updated.Words[0].Shared, updated.Words[0].CatalogUnit));

        // Their own example must be a pair, in the catalog's limits.
        Assert.Contains("birga", await DetailAsync(await teacher.PutAsJsonAsync($"/api/teacher/decks/{deck.Id}/words/{word.Id}",
            new UpdateGroupWordRequest("학교", "maktab", "hakgyo", "학교에 가요.", null, null), ApiFactory.Json), HttpStatusCode.UnprocessableEntity));
        var withExample = await OkAsync<GroupDeckDto>(await teacher.PutAsJsonAsync($"/api/teacher/decks/{deck.Id}/words/{word.Id}",
            new UpdateGroupWordRequest("학교", "maktab", "hakgyo", "학교에 가요.", "Maktabga boraman.", null), ApiFactory.Json));
        Assert.Equal(("hakgyo", "학교에 가요.", "Maktabga boraman."), (withExample.Words[0].Romanization, withExample.Words[0].ExampleKo, withExample.Words[0].ExampleUz));

        Assert.Empty((await OkAsync<GroupDeckDto>(await teacher.DeleteAsync($"/api/teacher/decks/{deck.Id}/words/{word.Id}"))).Words);
        Assert.Contains("so‘zlarni qo‘shing", await DetailAsync(await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/publish", null), HttpStatusCode.UnprocessableEntity));
    }

    /// <summary>A published deck of six words the catalog does not have, for one learner.</summary>
    private async Task<(HttpClient Learner, GroupDeckDto Deck)> PublishedSixAsync(ApiFactory factory)
    {
        var (teacher, group) = await SetupAsync(factory, LearnerA);
        await GivePremiumAsync(factory, Teacher);
        var deck = await CreateDeckAsync(teacher, group.Id, "1-dars");
        var file = CatalogFiles.XlsxOf([["대학", "universitet"], ["학생", "talaba"], ["생일", "tug‘ilgan kun"], ["일본", "Yaponiya"], ["본사", "bosh ofis"], ["사과", "olma"]]);
        await OkAsync<GroupDeckImportDto>(await ImportAsync(teacher, deck.Id, file));
        var published = await OkAsync<GroupDeckDto>(await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/publish", null));
        return (await factory.SignedInClientAsync(LearnerA), published);
    }

    [Fact]
    public async Task The_learner_sees_the_deck_as_its_own_folder_and_a_test_of_it_is_kept_with_the_deck()
    {
        await using var factory = NewFactory();
        var (learner, deck) = await PublishedSixAsync(factory);

        var list = (await learner.GetFromJsonAsync<MyWordsDto>("/api/my-words", ApiFactory.Json))!;
        var folder = Assert.Single(list.GroupDecks!);
        Assert.Equal((deck.Id, "1-dars", "Ansan", "Ustoz", 6, 0), (folder.Id, folder.Name, folder.GroupName, folder.TeacherName, folder.Words, folder.Learned));
        Assert.All(list.Words, w => Assert.True(w.FromGroupDeck));

        var page = await OkAsync<GroupDeckLearnerDto>(await learner.GetAsync($"/api/my-words/group-decks/{deck.Id}"));
        Assert.Equal(["대학", "학생", "생일", "일본", "본사", "사과"], page.Words.Select(w => w.Korean)); // the teacher's order
        Assert.Equal((0, 0, 6, (GroupDeckTestDto?)null), (page.Learned, page.Learning, page.New, page.LastTest));

        // Another learner, or a deck not given: 404.
        var stranger = await factory.SignedInClientAsync(LearnerB);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/my-words/group-decks/{deck.Id}")).StatusCode);

        // The test of exactly this deck is named after it and kept with it; no pass mark.
        var test = await OkAsync<TestDto>(await learner.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), null, 10, Decks: [page.Key]), ApiFactory.Json));
        Assert.Equal("1-dars", test.Title);
        foreach (var q in test.Questions)
        {
            await learner.PostAsJsonAsync($"/api/tests/{test.Id}/answers", new TestAnswerRequest(q.Index, q.Options.FirstOrDefault() ?? "x"), ApiFactory.Json);
        }

        await OkAsync<TestDto>(await learner.PostAsync($"/api/tests/{test.Id}/finish", null));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var run = await scope.ServiceProvider.GetRequiredService<AppDbContext>().TestRuns.SingleAsync(r => r.Id == test.Id);
            Assert.Equal((deck.Id, (int?)null, (bool?)null), (run.GroupDeckId, run.PassMark, run.Passed));
        }

        var after = await OkAsync<GroupDeckLearnerDto>(await learner.GetAsync($"/api/my-words/group-decks/{deck.Id}"));
        Assert.Equal(test.Id, after.LastTest!.TestId);
        Assert.Equal(test.Questions.Count, after.LastTest.Questions);

        // Mixed with a unit, the test is not the deck's.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await ApiFactory.OpenUnitAsync(scope.ServiceProvider, await TeachingTests.UserIdAsync(factory, LearnerA), 1, [("학교", "maktab"), ("물", "suv"), ("사랑", "sevgi")]);
        }

        var mixed = await OkAsync<TestDto>(await learner.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [ApiFactory.Unit(1)], 10, Decks: [page.Key]), ApiFactory.Json));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.Null((await scope.ServiceProvider.GetRequiredService<AppDbContext>().TestRuns.SingleAsync(r => r.Id == mixed.Id)).GroupDeckId);
        }
    }

    [Fact]
    public async Task A_crossword_can_be_made_from_the_deck_without_any_open_unit()
    {
        await using var factory = NewFactory();
        var (learner, deck) = await PublishedSixAsync(factory);
        var key = GroupDeckService.Key(deck.Id);

        var home = await OkAsync<CrosswordHomeDto>(await learner.GetAsync("/api/crosswords"));
        Assert.True(home.OpenUnits < home.UnitsNeeded); // only A-1 is open: too few units for a crossword of units
        Assert.Contains(home.Decks!, d => d.Key == key && d.Words == 6);
        Assert.True(home.Sizes.First(s => s.Size == Lugat.Domain.Games.CrosswordSize.Small).Available);

        var game = await OkAsync<CrosswordDto>(await learner.PostAsJsonAsync("/api/crosswords", new StartCrosswordRequest(Guid.NewGuid(), Lugat.Domain.Games.CrosswordSize.Small, null, key), ApiFactory.Json));
        Assert.InRange(game.Clues.Count, 2, 6);
        var deckUzbek = new[] { "universitet", "talaba", "tug‘ilgan kun", "Yaponiya", "bosh ofis", "olma" };
        Assert.All(game.Clues, c => Assert.Contains(c.Uzbek, deckUzbek));

        // Without a deck the units rule stays.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await learner.PostAsJsonAsync("/api/crosswords", new StartCrosswordRequest(Guid.NewGuid(), Lugat.Domain.Games.CrosswordSize.Small), ApiFactory.Json)).StatusCode);
    }

    [Fact]
    public async Task The_teacher_sees_each_learners_progress_tests_and_mistakes_on_the_deck_named_after_it()
    {
        await using var factory = NewFactory();
        var (teacher, group) = await SetupAsync(factory, LearnerA, LearnerB);
        await GivePremiumAsync(factory, Teacher);
        var deck = await CreateDeckAsync(teacher, group.Id, "1-dars");
        // 학교 is also the course's A-1 word: a mistake on it must read "1-dars", not "A-1".
        await OkAsync<GroupDeckImportDto>(await ImportAsync(teacher, deck.Id, CatalogFiles.XlsxOf([["학교", "maktab"], ["의자", "stul"], ["책상", "parta"], ["창문", "deraza"]])));
        await OkAsync<GroupDeckDto>(await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/publish", null));
        var learnerA = await factory.SignedInClientAsync(LearnerA);
        var learnerAId = await TeachingTests.UserIdAsync(factory, LearnerA);

        // Learner A forgets 학교 on a card and takes the deck test.
        var page = await OkAsync<GroupDeckLearnerDto>(await learnerA.GetAsync($"/api/my-words/group-decks/{deck.Id}"));
        var school = page.Words.Single(w => w.Korean == "학교");
        var session = (await OkAsync<StartSessionResponse>(await learnerA.PostAsJsonAsync("/api/sessions",
            new StartSessionRequest(null, true, LearningItemIds: [school.LearningItemId]), ApiFactory.Json))).Session!;
        await OkAsync<object>(await learnerA.PostAsJsonAsync($"/api/sessions/{session.Id}/answers",
            new AnswerRequest(Guid.NewGuid(), session.Position, Lugat.Domain.Learning.ReviewAnswer.Forgot, false, 2000), ApiFactory.Json));
        var test = await OkAsync<TestDto>(await learnerA.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), null, 10, Decks: [page.Key]), ApiFactory.Json));
        foreach (var q in test.Questions)
        {
            await learnerA.PostAsJsonAsync($"/api/tests/{test.Id}/answers", new TestAnswerRequest(q.Index, q.Options.FirstOrDefault() ?? "x"), ApiFactory.Json);
        }

        var finished = await OkAsync<TestDto>(await learnerA.PostAsync($"/api/tests/{test.Id}/finish", null));

        var report = await OkAsync<GroupDeckReportDto>(await teacher.GetAsync($"/api/teacher/decks/{deck.Id}/report"));
        Assert.Equal(("1-dars", 4, 2), (report.Name, report.Words, report.Students.Count));
        var a = report.Students.Single(x => x.UserId == learnerAId);
        Assert.Equal((4, 0, 1, 3, 1, 1), (a.Words, a.Learned, a.Learning, a.New, a.Reviewed7, a.Tests));
        Assert.Equal(finished.Score!.Value * 100 / test.Questions.Count, a.LastTestPercent!.Value, 1.0);
        var b = report.Students.Single(x => x.UserId != learnerAId);
        Assert.Equal((4, 0, 0, 4, 0, (int?)null), (b.Words, b.Reviewed7, b.Tests, b.New, b.Learned, b.LastTestPercent));
        // The test answers are random, so other words may be missed too; 학교 was certainly missed on its card.
        var schoolStat = report.WordStats.Single(w => w.Korean == "학교");
        Assert.True(schoolStat.Mistakes >= 1);
        Assert.Equal(1, schoolStat.Learners);
        Assert.Equal(report.WordStats.OrderByDescending(w => w.Mistakes).Select(w => w.Mistakes), report.WordStats.Select(w => w.Mistakes)); // most missed first

        // On the group page and the learner page the mistake is the deck's, and the deck shows with its last test.
        var groupPage = await OkAsync<GroupDetailDto>(await teacher.GetAsync($"/api/teacher/groups/{group.Id}"));
        Assert.Equal("1-dars", groupPage.HardestWords.Single(w => w.Korean == "학교").Unit);
        var student = await OkAsync<StudentDetailDto>(await teacher.GetAsync($"/api/teacher/students/{learnerAId}"));
        Assert.Equal("1-dars", student.TopMistakes.Single(w => w.Korean == "학교").Unit);
        var studentDeck = Assert.Single(student.Decks!);
        Assert.Equal((deck.Id, "1-dars", 4, a.LastTestPercent), (studentDeck.DeckId, studentDeck.Name, studentDeck.Words, studentDeck.LastTestPercent));

        // Another teacher: 404; a learner: 403.
        var other = await factory.SignedInClientAsync(OtherTeacher, "Boshqa");
        await TeachingTests.GrantAsync(factory, OtherTeacher, UserRoles.Teacher);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/teacher/decks/{deck.Id}/report")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await learnerA.GetAsync($"/api/teacher/decks/{deck.Id}/report")).StatusCode);
    }

    [Fact]
    public async Task A_photo_or_pdf_of_the_list_is_read_by_the_ai_and_checked_like_a_spreadsheet()
    {
        await using var factory = NewFactory();
        var (teacher, group) = await SetupAsync(factory);
        var deck = await CreateDeckAsync(teacher, group.Id);
        _ai.Answer = _ => """
            {"rows":[{"korean":"학교","uzbek":"maktab"},{"korean":"회사원","uzbek":"kompaniya xodimi"},{"korean":"학교","uzbek":"o‘quv yurti"},{"korean":"","uzbek":"sahifa 12"},{"korean":"물","uzbek":"сув"}]}
            """;

        // Without the paid plan the AI is not called and nothing is added.
        Assert.Equal(HttpStatusCode.Forbidden, (await ImportAsync(teacher, deck.Id, [0xFF, 0xD8, 0xFF], "lugat.jpg")).StatusCode);
        Assert.Empty(_ai.Requests);

        await GivePremiumAsync(factory, Teacher);
        var import = await OkAsync<GroupDeckImportDto>(await ImportAsync(teacher, deck.Id, [0xFF, 0xD8, 0xFF], "lugat.jpg"));
        Assert.Equal(["학교", "회사원"], import.Deck.Words.Select(w => w.Korean));
        Assert.Equal("kompaniya xodimi", import.Deck.Words[1].Uzbek); // the teacher's text, as read
        Assert.Equal(2, import.Added);
        Assert.Contains(import.Problems, p => p.Contains("takrorlangan"));
        Assert.Contains(import.Problems, p => p.Contains("lotin"));
        var request = Assert.Single(_ai.Requests);
        Assert.Equal(GroupDeckService.ReadFeature, request.Feature);
        var photo = Assert.Single(request.Attachments!);
        Assert.Equal(("image/jpeg", 3), (photo.MediaType, photo.Content.Length));

        // A PDF goes as a PDF; a file the AI finds no list in says so.
        _ai.Answer = _ => """{"rows":[]}""";
        Assert.Contains("lug‘at topilmadi", await DetailAsync(await ImportAsync(teacher, deck.Id, [0x25, 0x50, 0x44, 0x46], "darslik.pdf"), HttpStatusCode.UnprocessableEntity));
        Assert.Equal("application/pdf", _ai.Requests[^1].Attachments!.Single().MediaType);
        Assert.Equal(2, (await OkAsync<GroupDeckDto>(await teacher.GetAsync($"/api/teacher/decks/{deck.Id}"))).Words.Count);

        // Too big for the AI: refused before any call.
        var calls = _ai.Requests.Count;
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await ImportAsync(teacher, deck.Id, new byte[GroupDeckService.MaxAiFileBytes + 1], "katta.png")).StatusCode);
        Assert.Equal(calls, _ai.Requests.Count);
    }
}
