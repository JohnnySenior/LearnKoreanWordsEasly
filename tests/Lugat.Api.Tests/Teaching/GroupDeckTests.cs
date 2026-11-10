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
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
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
}
