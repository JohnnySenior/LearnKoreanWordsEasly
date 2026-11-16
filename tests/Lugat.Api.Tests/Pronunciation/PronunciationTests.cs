using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Api.Tests.Teaching;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Users;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Pronunciation;

public class PronunciationTests
{
    private static readonly (string Korean, string Uzbek)[] Words =
        [("국물", "sho‘rva suvi"), ("같이", "birga"), ("학교", "maktab"), ("한국말", "koreys tili"),
         ("독립", "mustaqillik"), ("연락", "aloqa"), ("좋다", "yaxshi"), ("읽다", "o‘qimoq"), ("사람", "odam")];
    private static readonly string[] Readings = ["궁물", "가치", "학꾜", "한궁말", "동닙", "열락", "조타", "익따"];

    private static async Task<T> Ok<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }

    private static ApiFactory Factory(FakeAiProvider? ai = null) => new()
    {
        Premium = false, AdminTelegramIds = [ApiFactory.UserA], TableWords = Words,
        AiProviders = ai is null ? [] : [ai],
    };

    [Theory]
    [InlineData("좋다", "조타", "ㄷ tovushi ㅎ bilan ㅌ ga aylanadi (거센소리되기).", "ㄷ tovushi ㅎ bilan ㅌ ga aylanadi (undoshning kuchli nafas bilan aytilishi).")]
    [InlineData("들어가다", "드러가다", "받침 ’ㄹ’ 다음 모음 ’ㅓ’가 이어지며 연음화: ㄹ이 다음 음절 초성으로 이동", "Bo‘g‘in oxiridagi undosh keyingi unliga ulanib o‘qiladi. «들어가다» so‘zi «드러가다» deb o‘qiladi.")]
    public async Task Legacy_notes_are_readable_for_editor_and_learner_without_rewriting_the_database(string korean, string reading, string oldNote, string readable)
    {
        await using var f = new ApiFactory { Premium = false, AdminTelegramIds = [ApiFactory.UserA], TableWords = [(korean, "ma’no")] };
        var admin = await f.SignedInClientAsync(ApiFactory.UserA);
        var learner = await f.SignedInClientAsync(ApiFactory.UserB);
        await learner.PostAsync(ApiFactory.TestWordsPath, null);
        uint version;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var word = await db.CatalogWords.SingleAsync(w => w.Word == korean);
            (word.PronunciationKo, word.PronunciationNoteUz, word.PronunciationReviewed) = (reading, oldNote, true);
            await db.SaveChangesAsync();
            version = word.Version;
        }
        var unit = (await admin.GetFromJsonAsync<UnitAdminDto>($"/api/admin/units/{ApiFactory.Unit(1)}", ApiFactory.Json))!;
        Assert.Equal(readable, unit.Words.Single(w => w.Word == korean).PronunciationReview!.ExplanationUz);
        var page = (await learner.GetFromJsonAsync<UnitDto>($"/api/units/{ApiFactory.Unit(1)}", ApiFactory.Json))!;
        Assert.Equal(readable, page.Words.Single(w => w.Word == korean).Pronunciation!.ExplanationUz);
        await using var check = f.Services.CreateAsyncScope();
        var stored = await check.ServiceProvider.GetRequiredService<AppDbContext>().CatalogWords.SingleAsync(w => w.Word == korean);
        Assert.Equal(oldNote, stored.PronunciationNoteUz);
        Assert.True(stored.PronunciationReviewed);
        Assert.Equal(reading, stored.PronunciationKo);
        Assert.Equal(version, stored.Version);
    }

    [Fact]
    public async Task Unit_counts_zero_one_eight_and_free_learners_only_receive_confirmed_differences()
    {
        await using var f = Factory();
        var admin = await f.SignedInClientAsync(ApiFactory.UserA);
        var learner = await f.SignedInClientAsync(ApiFactory.UserB);
        await learner.PostAsync(ApiFactory.TestWordsPath, null);
        var unit = await learner.GetFromJsonAsync<UnitDto>($"/api/units/{ApiFactory.Unit(1)}", ApiFactory.Json);
        Assert.Equal(0, unit!.PronunciationCount);
        var edits = (await admin.GetFromJsonAsync<UnitAdminDto>($"/api/admin/units/{unit.Unit.Id}", ApiFactory.Json))!.Words;
        for (var i = 0; i < Readings.Length; i++)
        {
            var word = edits.Single(w => w.Word == Words[i].Korean);
            var url = $"/api/admin/catalog/words/{word.Id}/pronunciation";
            var draft = await Ok<PronunciationReviewDto>(await admin.PutAsJsonAsync(url,
                new ReviewPronunciationRequest(Readings[i], "Tovush o‘zgaradi.", false, word.Version), ApiFactory.Json));
            Assert.False(draft.Reviewed);
            Assert.Equal(i, (await learner.GetFromJsonAsync<UnitDto>($"/api/units/{unit.Unit.Id}", ApiFactory.Json))!.PronunciationCount);
            var confirmed = await Ok<PronunciationReviewDto>(await admin.PutAsJsonAsync(url,
                new ReviewPronunciationRequest(Readings[i], "Tovush o‘zgaradi.", true, draft.Version), ApiFactory.Json));
            Assert.True(confirmed.Reviewed);
            var stale = await admin.PutAsJsonAsync(url, new ReviewPronunciationRequest(Readings[i], "Tovush o‘zgaradi.", true, draft.Version), ApiFactory.Json);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }
        unit = (await learner.GetFromJsonAsync<UnitDto>($"/api/units/{unit.Unit.Id}", ApiFactory.Json))!;
        Assert.Equal(8, unit.PronunciationCount);
        Assert.Equal("궁물", unit.Words.Single(w => w.Word == "국물").Pronunciation!.Reading);
        Assert.Null(unit.Words.Single(w => w.Word == "사람").Pronunciation);
        var written = await Ok<WritingResultDto>(await learner.PostAsJsonAsync("/api/writing/answers",
            new WritingCheckRequest(Guid.NewGuid(), unit.Words.Single(w => w.Word == "국물").LearningItemId,
                Lugat.Domain.Learning.ReviewDirection.UzbekToKorean, "궁물"), ApiFactory.Json));
        Assert.Equal("국물", written.Expected);
        Assert.NotEqual(Lugat.Domain.Writing.WritingVerdict.Correct, written.Verdict);
        Assert.False((await learner.GetFromJsonAsync<PremiumStatusDto>("/api/me/premium", ApiFactory.Json))!.IsPremium);
        var person = edits.Single(w => w.Word == "사람");
        await Ok<PronunciationReviewDto>(await admin.PutAsJsonAsync($"/api/admin/catalog/words/{person.Id}/pronunciation",
            new ReviewPronunciationRequest("사람", null, true, person.Version), ApiFactory.Json));
        Assert.Equal(8, (await learner.GetFromJsonAsync<UnitDto>($"/api/units/{unit.Unit.Id}", ApiFactory.Json))!.PronunciationCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.PutAsJsonAsync($"/api/admin/catalog/words/{person.Id}/pronunciation",
            new ReviewPronunciationRequest("사람", null, true, person.Version), ApiFactory.Json)).StatusCode);
    }

    [Fact]
    public async Task Teacher_AI_draft_needs_confirmation_and_published_corrections_reach_cards_without_editing_catalog()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi)
        {
            Answer = _ => """{"korean":"국물","reading":"궁물","explanationUz":"ㄱ tovushi ㅁ oldida ㅇ kabi eshitiladi."}""",
        };
        await using var f = Factory(ai);
        var teacher = await f.SignedInClientAsync(ApiFactory.UserA);
        await TeachingTests.GrantAsync(f, ApiFactory.UserA, UserRoles.Teacher);
        var learner = await f.SignedInClientAsync(ApiFactory.UserB);
        await learner.PostAsync(ApiFactory.TestWordsPath, null);
        var group = await Ok<TeacherGroupDto>(await teacher.PostAsJsonAsync("/api/teacher/groups", new CreateGroupRequest(Guid.NewGuid(), "Talaffuz"), ApiFactory.Json));
        await Ok<MyGroupDto>(await learner.PostAsync($"/api/groups/join/{group.InviteCode}", null));
        var deck = await Ok<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/decks", new CreateGroupDeckRequest(Guid.NewGuid(), "1-dars"), ApiFactory.Json));
        deck = await Ok<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/decks/{deck.Id}/words", new AddGroupWordRequest("국물", "sho‘rva suvi"), ApiFactory.Json));
        var word = Assert.Single(deck.Words);
        var url = $"/api/teacher/decks/{deck.Id}/words/{word.Id}/pronunciation";
        var draft = await Ok<PronunciationReviewDto>(await teacher.PostAsJsonAsync(url + "/suggest", new PronunciationVersionRequest(word.PronunciationReview!.Version), ApiFactory.Json));
        Assert.False(draft.Reviewed);
        Assert.Equal("궁물", draft.Reading);
        await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/publish", null);
        var page = (await learner.GetFromJsonAsync<GroupDeckLearnerDto>($"/api/my-words/group-decks/{deck.Id}", ApiFactory.Json))!;
        Assert.Equal(0, page.PronunciationCount);
        Assert.Null(Assert.Single(page.Words).Pronunciation);
        var review = await Ok<PronunciationReviewDto>(await teacher.PutAsJsonAsync(url,
            new ReviewPronunciationRequest(draft.Reading, draft.ExplanationUz, true, draft.Version), ApiFactory.Json));
        page = (await learner.GetFromJsonAsync<GroupDeckLearnerDto>($"/api/my-words/group-decks/{deck.Id}", ApiFactory.Json))!;
        Assert.Equal(1, page.PronunciationCount);
        Assert.Equal("궁물", Assert.Single(page.Words).Pronunciation!.Reading);
        var session = await Ok<StartSessionResponse>(await learner.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true, LearningItemIds: [page.Words[0].LearningItemId]), ApiFactory.Json));
        Assert.Equal("궁물", session.Session!.Current!.Pronunciation!.Reading);
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False((await db.CatalogWords.SingleAsync(w => w.Word == "국물")).PronunciationReviewed);
        Assert.All(await db.PersonalWords.Where(w => w.GroupDeckId == deck.Id).ToListAsync(), w => Assert.True(w.PronunciationReviewed));
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.PutAsJsonAsync(url, new ReviewPronunciationRequest("궁물", "Izoh.", true, review.Version), ApiFactory.Json)).StatusCode);
        // A teacher who owns another group cannot write this pronunciation.
        var other = await f.SignedInClientAsync(1003);
        await TeachingTests.GrantAsync(f, 1003, UserRoles.Teacher);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(url, new ReviewPronunciationRequest("궁물", "Izoh.", true, review.Version), ApiFactory.Json)).StatusCode);
        await Ok<PronunciationReviewDto>(await teacher.PutAsJsonAsync(url, new ReviewPronunciationRequest("궁물", "Izoh.", false, review.Version), ApiFactory.Json));
        Assert.Equal(0, (await learner.GetFromJsonAsync<GroupDeckLearnerDto>($"/api/my-words/group-decks/{deck.Id}", ApiFactory.Json))!.PronunciationCount);
    }

    [Fact]
    public async Task Editing_Korean_invalidates_approval_and_AI_requires_paid_access()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi);
        await using var f = Factory(ai);
        var teacher = await f.SignedInClientAsync(ApiFactory.UserB);
        await TeachingTests.GrantAsync(f, ApiFactory.UserB, UserRoles.Teacher);
        var group = await Ok<TeacherGroupDto>(await teacher.PostAsJsonAsync("/api/teacher/groups", new CreateGroupRequest(Guid.NewGuid(), "Guruh"), ApiFactory.Json));
        var deck = await Ok<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/decks", new CreateGroupDeckRequest(Guid.NewGuid(), "Lug‘at"), ApiFactory.Json));
        deck = await Ok<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/decks/{deck.Id}/words", new AddGroupWordRequest("국물", "sho‘rva suvi"), ApiFactory.Json));
        var word = Assert.Single(deck.Words);
        var url = $"/api/teacher/decks/{deck.Id}/words/{word.Id}";
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsJsonAsync(url + "/pronunciation/suggest", new PronunciationVersionRequest(word.PronunciationReview!.Version), ApiFactory.Json)).StatusCode);
        Assert.Empty(ai.Requests);
        await Ok<PronunciationReviewDto>(await teacher.PutAsJsonAsync(url + "/pronunciation", new ReviewPronunciationRequest("궁물", "Tovush o‘zgaradi.", true, word.PronunciationReview.Version), ApiFactory.Json));
        deck = await Ok<GroupDeckDto>(await teacher.PutAsJsonAsync(url, new UpdateGroupWordRequest("학교", "maktab", null, null, null, null), ApiFactory.Json));
        Assert.False(Assert.Single(deck.Words).PronunciationReview!.Reviewed);
        Assert.Null(Assert.Single(deck.Words).PronunciationReview!.Reading);
    }
    [Fact]
    public async Task Catalog_matching_copies_only_unambiguous_reviewed_readings()
    {
        await using var f = Factory();
        var teacher = await f.SignedInClientAsync(ApiFactory.UserA);
        await TeachingTests.GrantAsync(f, ApiFactory.UserA, UserRoles.Teacher);
        await teacher.PostAsync(ApiFactory.TestWordsPath, null);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var original = await db.CatalogWords.SingleAsync(w => w.Word == "국물");
            original.PronunciationKo = "궁물";
            original.PronunciationNoteUz = "Tovush o‘zgaradi.";
            original.PronunciationReviewed = true;
            var clone = new Lugat.Domain.Catalog.CatalogWord
            {
                Id = Guid.NewGuid(), SourceKey = "ambiguous-pronunciation", Word = original.Word,
                Pos = original.Pos, Level = original.Level, TranslationUz = "boshqa ma’no",
                Status = Lugat.Domain.Catalog.CatalogWordStatus.Reviewed,
                PronunciationKo = "궁물", PronunciationNoteUz = "Boshqa izoh.", PronunciationReviewed = true,
            };
            db.CatalogWords.Add(clone);
            await db.SaveChangesAsync();
        }
        var group = await Ok<TeacherGroupDto>(await teacher.PostAsJsonAsync("/api/teacher/groups", new CreateGroupRequest(Guid.NewGuid(), "Moslik"), ApiFactory.Json));
        async Task<GroupDeckDto> Add(string translation)
        {
            var deck = await Ok<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/decks", new CreateGroupDeckRequest(Guid.NewGuid(), translation), ApiFactory.Json));
            return await Ok<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/decks/{deck.Id}/words", new AddGroupWordRequest("국물", translation), ApiFactory.Json));
        }
        Assert.True(Assert.Single((await Add("sho‘rva suvi")).Words).PronunciationReview!.Reviewed);
        Assert.False(Assert.Single((await Add("noaniq tarjima")).Words).PronunciationReview!.Reviewed);
        Assert.Null(Assert.Single((await Add("aniq emas")).Words).PronunciationReview!.Reading);
    }

    [Fact]
    public async Task Invalid_AI_and_invalid_human_readings_never_replace_a_valid_draft()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi)
        {
            Answer = _ => """{"korean":"다른말","reading":"궁물","explanationUz":"Izoh."}""",
        };
        await using var f = Factory(ai);
        var editor = await f.SignedInClientAsync(ApiFactory.UserA);
        await editor.PostAsync(ApiFactory.TestWordsPath, null);
        var unit = (await editor.GetFromJsonAsync<UnitAdminDto>($"/api/admin/units/{ApiFactory.Unit(1)}", ApiFactory.Json))!;
        var word = unit.Words.Single(w => w.Word == "국물");
        var url = $"/api/admin/catalog/words/{word.Id}/pronunciation";
        var draft = await Ok<PronunciationReviewDto>(await editor.PutAsJsonAsync(url, new ReviewPronunciationRequest("궁물", "Izoh.", false, word.Version), ApiFactory.Json));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await editor.PostAsJsonAsync(url + "/suggest", new PronunciationVersionRequest(draft.Version), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await editor.PutAsJsonAsync(url, new ReviewPronunciationRequest("gungmul", "Izoh.", true, draft.Version), ApiFactory.Json)).StatusCode);
        unit = (await editor.GetFromJsonAsync<UnitAdminDto>($"/api/admin/units/{ApiFactory.Unit(1)}", ApiFactory.Json))!;
        Assert.Equal(draft, unit.Words.Single(w => w.Id == word.Id).PronunciationReview);
    }

    [Fact]
    public async Task Invalid_AI_response_keeps_draft_and_a_concurrent_word_edit_is_a_conflict()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi);
        await using var f = Factory(ai);
        var teacher = await f.SignedInClientAsync(ApiFactory.UserA);
        await TeachingTests.GrantAsync(f, ApiFactory.UserA, UserRoles.Teacher);
        var group = await Ok<TeacherGroupDto>(await teacher.PostAsJsonAsync("/api/teacher/groups", new CreateGroupRequest(Guid.NewGuid(), "Tekshiruv"), ApiFactory.Json));
        var deck = await Ok<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/decks", new CreateGroupDeckRequest(Guid.NewGuid(), "1-dars"), ApiFactory.Json));
        deck = await Ok<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/decks/{deck.Id}/words", new AddGroupWordRequest("국물", "sho‘rva suvi"), ApiFactory.Json));
        var word = Assert.Single(deck.Words);
        var url = $"/api/teacher/decks/{deck.Id}/words/{word.Id}/pronunciation";
        var draft = await Ok<PronunciationReviewDto>(await teacher.PutAsJsonAsync(url, new ReviewPronunciationRequest("궁물", "Izoh.", false, word.PronunciationReview!.Version), ApiFactory.Json));
        ai.Answer = _ => """{"korean":"국물","reading":"gungmul","explanationUz":"Izoh."}""";
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await teacher.PostAsJsonAsync(url + "/suggest", new PronunciationVersionRequest(draft.Version), ApiFactory.Json)).StatusCode);
        deck = (await teacher.GetFromJsonAsync<GroupDeckDto>($"/api/teacher/decks/{deck.Id}", ApiFactory.Json))!;
        Assert.Equal("궁물", Assert.Single(deck.Words).PronunciationReview!.Reading);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await teacher.PutAsJsonAsync(url, new ReviewPronunciationRequest("[궁물]", "Izoh.", true, draft.Version), ApiFactory.Json)).StatusCode);
        ai.Answer = _ =>
        {
            // Another request commits while the provider is responding.
            using var scope = f.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var changed = db.GroupDeckWords.Single(w => w.Id == word.Id);
            changed.Korean = "학교";
            changed.PronunciationKo = null;
            changed.PronunciationNoteUz = null;
            db.SaveChanges();
            return """{"korean":"국물","reading":"궁물","explanationUz":"Izoh."}""";
        };
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.PostAsJsonAsync(url + "/suggest", new PronunciationVersionRequest(draft.Version), ApiFactory.Json)).StatusCode);
        deck = (await teacher.GetFromJsonAsync<GroupDeckDto>($"/api/teacher/decks/{deck.Id}", ApiFactory.Json))!;
        Assert.Equal("학교", Assert.Single(deck.Words).Korean);
        Assert.Null(Assert.Single(deck.Words).PronunciationReview!.Reading);
    }

}
