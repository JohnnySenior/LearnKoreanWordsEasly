using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Catalog;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Api.Tests.Me;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Learning;
using Lugat.Domain.Premium;
using Lugat.Domain.Study;
using Lugat.Domain.Users;
using Lugat.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Teaching;

/// <summary>
/// L-71: a teacher's deck can have a due day. Done = every word of the deck seen on a card AND the deck test taken,
/// both by the end of that day; the score is shown, there is no pass mark. A learner not done gets one calm note a day before.
/// </summary>
public class DeckDueTests
{
    private const long Teacher = 6101;
    private const long LearnerA = 6103;
    private const long LearnerB = 6104;

    /// <summary>1 September 2026 is "today" for the factory; the deck is due on the 3rd, i.e. 4 Sept 00:00 in Seoul.</summary>
    private static readonly DateOnly DueDay = new(2026, 9, 3);
    private static readonly DateTimeOffset DueAt = new(2026, 9, 3, 15, 0, 0, TimeSpan.Zero);

    private readonly FakeMessenger _telegram = new();

    private ApiFactory NewFactory()
    {
        var factory = new ApiFactory { Premium = false, AiProviders = [new FakeAiProvider(AiProviderKind.OpenAi)], Messenger = _telegram };
        factory.Settings["Reminders:PollInterval"] = "01:00:00";
        return factory;
    }

    private static async Task<T> OkAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }

    private static async Task<(HttpClient Teacher, GroupDeckDto Deck)> PublishedAsync(ApiFactory factory)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await ApiFactory.PublishUnitAsync(scope.ServiceProvider, 1, [("학교", "maktab"), ("물", "suv"), ("사랑", "sevgi")]);
        }

        var teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
        await TeachingTests.GrantAsync(factory, Teacher, UserRoles.Teacher);
        var group = await OkAsync<TeacherGroupDto>(await teacher.PostAsJsonAsync("/api/teacher/groups", new CreateGroupRequest(Guid.NewGuid(), "Ansan"), ApiFactory.Json));
        foreach (var id in new[] { LearnerA, LearnerB })
        {
            var learner = await factory.SignedInClientAsync(id, $"O‘quvchi {id}");
            await OkAsync<MyGroupDto>(await learner.PostAsync($"/api/groups/join/{group.InviteCode}", null));
        }

        var teacherId = await TeachingTests.UserIdAsync(factory, Teacher);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = factory.Time.GetUtcNow();
            db.Subscriptions.Add(new Subscription { Id = Guid.CreateVersion7(), UserId = teacherId, Plan = SubscriptionPlan.Monthly, StartsAt = now.AddDays(-1), EndsAt = now.AddDays(29), Source = SubscriptionSource.Admin, CreatedAt = now });
            await db.SaveChangesAsync();
        }

        var deck = await OkAsync<GroupDeckDto>(await teacher.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/decks", new CreateGroupDeckRequest(Guid.NewGuid(), "1-dars"), ApiFactory.Json));
        await OkAsync<GroupDeckImportDto>(await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/import",
            CatalogFiles.Upload(CatalogFiles.XlsxOf([["학교", "maktab"], ["의자", "stul"], ["책상", "parta"], ["창문", "deraza"]]), "lugat.xlsx")));
        return (teacher, await OkAsync<GroupDeckDto>(await teacher.PostAsync($"/api/teacher/decks/{deck.Id}/publish", null)));
    }

    private static Task<HttpResponseMessage> SetDueAsync(HttpClient teacher, Guid deckId, DateOnly? day) =>
        teacher.PutAsJsonAsync($"/api/teacher/decks/{deckId}/due", new SetDeckDueRequest(day), ApiFactory.Json);

    /// <summary>Shows the learner these words on cards, one answer each.</summary>
    private static async Task SeeAsync(HttpClient learner, IEnumerable<Guid> items)
    {
        var session = (await OkAsync<StartSessionResponse>(await learner.PostAsJsonAsync("/api/sessions",
            new StartSessionRequest(null, true, LearningItemIds: items.ToList()), ApiFactory.Json))).Session!;
        while (session.Status == StudySessionStatus.Active && session.Current is not null)
        {
            session = (await OkAsync<AnswerResultDto>(await learner.PostAsJsonAsync($"/api/sessions/{session.Id}/answers",
                new AnswerRequest(Guid.NewGuid(), session.Position, ReviewAnswer.Easy, false, 2000), ApiFactory.Json))).Session;
        }
    }

    private static async Task<TestDto> TestAsync(HttpClient learner, string key)
    {
        var test = await OkAsync<TestDto>(await learner.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), null, 4, Decks: [key]), ApiFactory.Json));
        foreach (var q in test.Questions)
        {
            await learner.PostAsJsonAsync($"/api/tests/{test.Id}/answers", new TestAnswerRequest(q.Index, q.Options.FirstOrDefault() ?? "x"), ApiFactory.Json);
        }

        return await OkAsync<TestDto>(await learner.PostAsync($"/api/tests/{test.Id}/finish", null));
    }

    [Fact]
    public async Task The_due_day_is_checked_and_can_be_moved_or_removed()
    {
        await using var factory = NewFactory();
        var (teacher, deck) = await PublishedAsync(factory);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetDueAsync(teacher, deck.Id, new DateOnly(2026, 8, 31))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetDueAsync(teacher, deck.Id, new DateOnly(2027, 1, 1))).StatusCode);
        Assert.Equal(DueDay, (await OkAsync<GroupDeckDto>(await SetDueAsync(teacher, deck.Id, DueDay))).DueOn);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().GroupDecks.FindAsync(deck.Id);
            Assert.Equal(DueAt, saved!.DueAt); // the end of the 3rd in the teacher's zone (Seoul)
        }

        var learner = await factory.SignedInClientAsync(LearnerA);
        var folder = Assert.Single((await learner.GetFromJsonAsync<MyWordsDto>("/api/my-words", ApiFactory.Json))!.GroupDecks!);
        Assert.Equal((DueDay, false), (folder.DueOn, folder.Done));

        // Another teacher cannot touch it.
        var other = await factory.SignedInClientAsync(6102, "Boshqa");
        await TeachingTests.GrantAsync(factory, 6102, UserRoles.Teacher);
        Assert.Equal(HttpStatusCode.NotFound, (await SetDueAsync(other, deck.Id, DueDay)).StatusCode);

        Assert.Null((await OkAsync<GroupDeckDto>(await SetDueAsync(teacher, deck.Id, null))).DueOn);
        Assert.Null((await OkAsync<GroupDeckReportDto>(await teacher.GetAsync($"/api/teacher/decks/{deck.Id}/report"))).DueOn);
    }

    [Fact]
    public async Task Done_means_every_word_seen_and_the_test_taken_by_the_due_time_with_the_score_and_no_pass_mark()
    {
        await using var factory = NewFactory();
        var (teacher, deck) = await PublishedAsync(factory);
        await OkAsync<GroupDeckDto>(await SetDueAsync(teacher, deck.Id, DueDay));
        var learnerA = await factory.SignedInClientAsync(LearnerA);
        var learnerAId = await TeachingTests.UserIdAsync(factory, LearnerA);
        var page = await OkAsync<GroupDeckLearnerDto>(await learnerA.GetAsync($"/api/my-words/group-decks/{deck.Id}"));

        // The test alone is not enough, nor three words of four.
        factory.Time.Advance(TimeSpan.FromMinutes(20)); // the access token lives an hour
        var test = await TestAsync(learnerA, page.Key);
        await SeeAsync(learnerA, page.Words.Take(3).Select(w => w.LearningItemId));
        var report = await OkAsync<GroupDeckReportDto>(await teacher.GetAsync($"/api/teacher/decks/{deck.Id}/report"));
        Assert.Equal((DueDay, false), (report.DueOn, report.DuePassed));
        var a = report.Students.Single(s => s.UserId == learnerAId).Done!;
        Assert.Equal((4, 3, true, false), (a.Words, a.Seen, a.TestTaken, a.Done));

        // The last word: done, at that moment, with the test's score.
        factory.Time.Advance(TimeSpan.FromMinutes(20));
        var doneAt = factory.Time.GetUtcNow();
        await SeeAsync(learnerA, [page.Words[3].LearningItemId]);
        a = (await OkAsync<GroupDeckReportDto>(await teacher.GetAsync($"/api/teacher/decks/{deck.Id}/report"))).Students.Single(s => s.UserId == learnerAId).Done!;
        Assert.Equal((4, true, true), (a.Seen, a.TestTaken, a.Done));
        Assert.Equal(doneAt, a.DoneAt!.Value, TimeSpan.FromMinutes(1));
        Assert.Equal(test.Score!.Value * 100 / test.Questions.Count, a.TestPercent!.Value, 1.0);

        var mine = await OkAsync<GroupDeckLearnerDto>(await learnerA.GetAsync($"/api/my-words/group-decks/{deck.Id}"));
        Assert.Equal((4, true, (DateOnly?)DueDay), (mine.Seen, mine.Done, mine.DueOn));
        var student = await OkAsync<StudentDetailDto>(await teacher.GetAsync($"/api/teacher/students/{learnerAId}"));
        Assert.True(Assert.Single(student.Decks!).Done!.Done);

        // Learner B does it all after the due time: it does not count, and the report says the day has passed.
        var learnerBId = await TeachingTests.UserIdAsync(factory, LearnerB);
        factory.Time.SetUtcNow(DueAt.AddHours(1));
        var learnerB = await factory.SignedInClientAsync(LearnerB); // a fresh sign-in: the clock jumped past the token's life
        teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
        var pageB = await OkAsync<GroupDeckLearnerDto>(await learnerB.GetAsync($"/api/my-words/group-decks/{deck.Id}"));
        await SeeAsync(learnerB, pageB.Words.Select(w => w.LearningItemId));
        await TestAsync(learnerB, pageB.Key);
        report = await OkAsync<GroupDeckReportDto>(await teacher.GetAsync($"/api/teacher/decks/{deck.Id}/report"));
        Assert.True(report.DuePassed);
        Assert.Equal((0, false, false), (report.Students.Single(s => s.UserId == learnerBId).Done!.Seen, report.Students.Single(s => s.UserId == learnerBId).Done!.TestTaken, report.Students.Single(s => s.UserId == learnerBId).Done!.Done));
        Assert.True(report.Students.Single(s => s.UserId == learnerAId).Done!.Done);

        // Moving the day on gives B the time: now B is done too.
        await OkAsync<GroupDeckDto>(await SetDueAsync(teacher, deck.Id, new DateOnly(2026, 9, 10)));
        report = await OkAsync<GroupDeckReportDto>(await teacher.GetAsync($"/api/teacher/decks/{deck.Id}/report"));
        Assert.Equal((4, true, true), (report.Students.Single(s => s.UserId == learnerBId).Done!.Seen, report.Students.Single(s => s.UserId == learnerBId).Done!.TestTaken, report.Students.Single(s => s.UserId == learnerBId).Done!.Done));
    }

    [Fact]
    public async Task A_learner_not_done_gets_one_calm_note_a_day_before_and_not_when_done()
    {
        await using var factory = NewFactory();
        var (teacher, deck) = await PublishedAsync(factory);
        await OkAsync<GroupDeckDto>(await SetDueAsync(teacher, deck.Id, DueDay));
        var learnerA = await factory.SignedInClientAsync(LearnerA);
        var page = await OkAsync<GroupDeckLearnerDto>(await learnerA.GetAsync($"/api/my-words/group-decks/{deck.Id}"));
        await SeeAsync(learnerA, page.Words.Select(w => w.LearningItemId));
        await TestAsync(learnerA, page.Key); // A is done
        var learnerB = await factory.SignedInClientAsync(LearnerB);
        var pageB = await OkAsync<GroupDeckLearnerDto>(await learnerB.GetAsync($"/api/my-words/group-decks/{deck.Id}"));
        await SeeAsync(learnerB, pageB.Words.Take(1).Select(w => w.LearningItemId)); // B saw one word, no test

        // Two days before: nothing yet.
        factory.Time.SetUtcNow(DueAt.AddHours(-30));
        await RemindersTests.RunAsync(factory);
        Assert.DoesNotContain(_telegram.Sent, s => s.Text.Contains("1-dars"));

        // A day before: one note to B, twice run, still once; none to A nor to the teacher.
        factory.Time.SetUtcNow(DueAt.AddHours(-21));
        await RemindersTests.RunAsync(factory);
        await RemindersTests.RunAsync(factory);
        var note = Assert.Single(_telegram.Sent, s => s.Text.Contains("1-dars"));
        Assert.Equal(LearnerB, note.ChatId);
        Assert.Equal("«1-dars» lug‘ati muddati ertaga. Qoldi: 3 ta so‘z va sinov.", note.Text);

        // Reminders off: no note.
        teacher = await factory.SignedInClientAsync(Teacher, "Ustoz"); // the clock jumped past the token's life
        learnerB = await factory.SignedInClientAsync(LearnerB);
        await OkAsync<GroupDeckDto>(await SetDueAsync(teacher, deck.Id, new DateOnly(2026, 9, 5)));
        await (await learnerB.PatchAsJsonAsync("/api/me/preferences", new UpdatePreferencesRequest(false, null), ApiFactory.Json)).Content.ReadAsStringAsync();
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 9, 5, 15, 0, 0, TimeSpan.Zero).AddHours(-21));
        await RemindersTests.RunAsync(factory);
        Assert.Single(_telegram.Sent, s => s.Text.Contains("1-dars"));
    }
}
