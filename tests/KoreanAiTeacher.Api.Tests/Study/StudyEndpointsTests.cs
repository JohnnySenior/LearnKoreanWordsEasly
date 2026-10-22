using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KoreanAiTeacher.Api.Tests.Imports;
using KoreanAiTeacher.Api.Tests.Infrastructure;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Domain.Imports;
using KoreanAiTeacher.Domain.Learning;
using KoreanAiTeacher.Domain.Lessons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KoreanAiTeacher.Api.Tests.Study;

public class StudyEndpointsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Monday 5 October 2026, 10:00 in Seoul.</summary>
    private static readonly DateTimeOffset MondayMorningKst = new(2026, 10, 5, 10, 0, 0, TimeSpan.FromHours(9));

    private readonly FakePdfLayoutReader _pdf = new FakePdfLayoutReader()
        .AddPage(FakePdfLayoutReader.Words("가", 5))
        .AddPage(FakePdfLayoutReader.Words("나", 5))
        .AddPage(FakePdfLayoutReader.Words("다", 5))
        .AddPage(FakePdfLayoutReader.Words("라", 5));

    private ApiFactory NewFactory()
    {
        var factory = new ApiFactory { PdfReader = _pdf };
        factory.Time.SetUtcNow(MondayMorningKst.ToUniversalTime()); // the system clock is always UTC
        return factory;
    }

    /// <summary>Uploads the 4-table book, approves the given tables and returns the import.</summary>
    private static async Task<ImportJobDto> ImportAsync(HttpClient client, params int[] approveTables)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.7 study"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "lugat.pdf");
        var job = (await (await client.PostAsync("/api/imports", form)).Content.ReadFromJsonAsync<ImportJobDto>(Json))!;
        for (var i = 0; i < 100 && job.Status != ImportStatus.NeedsReview; i++)
        {
            await Task.Delay(100);
            job = (await client.GetFromJsonAsync<ImportJobDto>($"/api/imports/{job.Id}", Json))!;
        }

        foreach (var t in approveTables)
        {
            (await client.PostAsync($"/api/imports/{job.Id}/tables/{t}/approve", null)).EnsureSuccessStatusCode();
        }

        return job;
    }

    private static async Task<AssignmentDto> AssignSuggestedAsync(HttpClient client)
    {
        var suggestion = (await client.GetFromJsonAsync<AssignmentSuggestionDto>("/api/assignments/suggestion", Json))!;
        var response = await client.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest(suggestion.SourceDocumentId, suggestion.Tables, suggestion.DueAt, null), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssignmentDto>(Json))!;
    }

    private static async Task<SessionDto> StartAsync(HttpClient client, int? minutes = null, bool restart = false)
    {
        var response = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(minutes, restart), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;
    }

    private static Task<HttpResponseMessage> AnswerAsync(HttpClient client, SessionDto session, ReviewAnswer answer, Guid? eventId = null, int? position = null) =>
        client.PostAsJsonAsync($"/api/sessions/{session.Id}/answers", new AnswerRequest(eventId ?? Guid.NewGuid(), position ?? session.Position, answer, false, 3000), Json);

    private static async Task<AnswerResultDto> AnswerOkAsync(HttpClient client, SessionDto session, ReviewAnswer answer, Guid? eventId = null)
    {
        var response = await AnswerAsync(client, session, answer, eventId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AnswerResultDto>(Json))!;
    }

    [Fact]
    public async Task Suggestion_is_the_next_two_tables_on_the_next_lesson_evening()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1, 2);

        var first = await AssignSuggestedAsync(client);
        var second = (await client.GetFromJsonAsync<AssignmentSuggestionDto>("/api/assignments/suggestion", Json))!;

        Assert.Equal([1, 2], first.Tables);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 23, 0, 0, TimeSpan.FromHours(9)), first.DueAt); // Tuesday 23:00 KST
        Assert.Equal((10, 10, 10), (first.WordCount, first.ApprovedWords, first.NewWords));
        Assert.Equal("1–2-jadvallar", first.Title);
        Assert.Equal([3, 4], second.Tables);
        Assert.Equal(new DateTimeOffset(2026, 10, 8, 23, 0, 0, TimeSpan.FromHours(9)), second.DueAt); // Thursday
        Assert.Equal(0, second.ApprovedWords);
    }

    [Fact]
    public async Task Suggestion_follows_the_learners_own_lesson_plan()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1);

        // One table on Monday and Friday: 2 tables (60 words) a week instead of 6 (D-43).
        var saved = await client.PatchAsJsonAsync("/api/me/preferences",
            new UpdatePreferencesRequest(null, null, null, 1, [DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Friday]), Json);
        var prefs = (await saved.Content.ReadFromJsonAsync<PreferencesDto>(Json))!;
        var first = await AssignSuggestedAsync(client);
        var second = (await client.GetFromJsonAsync<AssignmentSuggestionDto>("/api/assignments/suggestion", Json))!;

        Assert.Equal(1, prefs.TablesPerLesson);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], prefs.LessonDays);
        Assert.Equal([1], first.Tables);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 23, 0, 0, TimeSpan.FromHours(9)), first.DueAt); // Monday 23:00 KST
        Assert.Equal([2], second.Tables);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 23, 0, 0, TimeSpan.FromHours(9)), second.DueAt); // Friday
    }

    [Fact]
    public async Task New_plan_redistributes_upcoming_lessons_but_not_one_already_due()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1);
        await AssignSuggestedAsync(client); // 1–2, Tuesday 6 Oct
        await AssignSuggestedAsync(client); // 3–4, Thursday 8 Oct
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.FromHours(9)).ToUniversalTime()); // Wednesday
        client = await factory.SignedInClientAsync(ApiFactory.UserA); // a fresh sign-in after two days

        var saved = await client.PatchAsJsonAsync("/api/me/preferences",
            new UpdatePreferencesRequest(null, null, null, 1, [DayOfWeek.Monday, DayOfWeek.Friday]), Json);
        var prefs = (await saved.Content.ReadFromJsonAsync<PreferencesDto>(Json))!;
        var lessons = (await client.GetFromJsonAsync<List<AssignmentDto>>("/api/assignments", Json))!;

        Assert.Equal(2, prefs.ReplannedLessons);
        Assert.Equal(
            [
                ("1–2-jadvallar", new DateTimeOffset(2026, 10, 6, 23, 0, 0, TimeSpan.FromHours(9))), // already due, kept
                ("3-jadval", new DateTimeOffset(2026, 10, 9, 23, 0, 0, TimeSpan.FromHours(9))),      // Friday
                ("4-jadval", new DateTimeOffset(2026, 10, 12, 23, 0, 0, TimeSpan.FromHours(9))),     // Monday
            ],
            lessons.Select(l => (l.Title, l.DueAt)).ToList());

        // Back to more tables per lesson: the split lessons are merged again and the spare one removed.
        saved = await client.PatchAsJsonAsync("/api/me/preferences", new UpdatePreferencesRequest(null, null, null, 2, null), Json);
        lessons = (await client.GetFromJsonAsync<List<AssignmentDto>>("/api/assignments", Json))!;

        Assert.Equal(2, (await saved.Content.ReadFromJsonAsync<PreferencesDto>(Json))!.ReplannedLessons);
        Assert.Equal([[1, 2], [3, 4]], lessons.Select(l => l.Tables.ToArray()).ToList());
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 23, 0, 0, TimeSpan.FromHours(9)), lessons[1].DueAt);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(7, null)]
    [InlineData(null, "none")]
    public async Task Lesson_plan_rejects_out_of_range_tables_and_no_days(int? tables, string? days)
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var response = await client.PatchAsJsonAsync("/api/me/preferences",
            new UpdatePreferencesRequest(null, null, null, tables, days is null ? null : []), Json);
        var prefs = (await client.GetFromJsonAsync<PreferencesDto>("/api/me/preferences", Json))!;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(2, prefs.TablesPerLesson); // the teacher's default stays
        Assert.Equal([DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday], prefs.LessonDays);
    }

    [Fact]
    public async Task Session_teaches_the_lesson_words_in_both_directions()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1, 2, 3);
        await AssignSuggestedAsync(client);

        var session = await StartAsync(client);

        Assert.Equal(20, session.Total); // tables 1–2: 10 words × 2 directions; table 3 waits for its lesson
        Assert.Equal(SessionItemKind.New, session.Current!.Kind);
        Assert.Equal(ReviewDirection.KoreanToUzbek, session.Current.Direction);
        Assert.Equal("가가", session.Current.Prompt);
        Assert.Equal("ma’no 1", session.Current.Answer);
        Assert.Equal([ReviewAnswer.Forgot, ReviewAnswer.Hard, ReviewAnswer.Easy], session.Current.Previews.Select(p => p.Answer));
        Assert.Equal([15 * 60, 5 * 3600, 24 * 3600], session.Current.Previews.Select(p => p.IntervalSeconds));
    }

    [Fact]
    public async Task Unapproved_words_never_reach_practice()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client); // nothing approved

        var response = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, false), Json);
        var body = (await response.Content.ReadFromJsonAsync<StartSessionResponse>(Json))!;

        Assert.Null(body.Session);
        Assert.Contains("tasdiqlang", body.Message);
    }

    [Fact]
    public async Task Excluding_an_approved_word_removes_it_from_new_sessions()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var job = await ImportAsync(client, 1);
        var entry = (await client.GetFromJsonAsync<List<SourceEntryDto>>($"/api/imports/{job.Id}/entries?table=1", Json))![0];

        (await client.PatchAsJsonAsync($"/api/entries/{entry.Id}", new UpdateEntryRequest(null, null, null, EntryStatus.Excluded, entry.Version), Json)).EnsureSuccessStatusCode();
        var session = await StartAsync(client);

        Assert.Equal(8, session.Total);
    }

    [Fact]
    public async Task Short_session_fits_the_chosen_minutes()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1, 2);

        var session = await StartAsync(client, minutes: 1);

        Assert.Equal(4, session.Total); // 60s / 12s = 5 prompts → two whole new words
    }

    [Fact]
    public async Task Unfinished_session_is_resumed_and_can_be_restarted()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1);
        var session = await StartAsync(client);
        await AnswerOkAsync(client, session, ReviewAnswer.Easy);

        var resumed = await StartAsync(client);
        var current = (await client.GetFromJsonAsync<SessionDto>("/api/sessions/current", Json))!;
        var restarted = await StartAsync(client, restart: true);

        Assert.Equal(session.Id, resumed.Id);
        Assert.Equal(1, resumed.Position);
        Assert.Equal(session.Id, current.Id);
        Assert.NotEqual(session.Id, restarted.Id);
        Assert.Equal(StudySessionStatus.Abandoned, (await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{session.Id}", Json))!.Status);
    }

    [Fact]
    public async Task Sending_the_same_answer_twice_changes_the_schedule_once()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1);
        var session = await StartAsync(client);
        var eventId = Guid.NewGuid();

        var first = await AnswerOkAsync(client, session, ReviewAnswer.Easy, eventId);
        var retry = await AnswerOkAsync(client, session, ReviewAnswer.Easy, eventId);

        Assert.Equal(first.NextDueAt, retry.NextDueAt);
        Assert.Equal(1, retry.Session.Position);
        Assert.Equal(1, retry.Session.Summary.Answered);
        Assert.Equal(24 * 3600, first.IntervalSeconds);
    }

    [Fact]
    public async Task Answer_for_a_stale_position_is_a_conflict()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1);
        var session = await StartAsync(client);
        await AnswerOkAsync(client, session, ReviewAnswer.Easy);

        var stale = await AnswerAsync(client, session, ReviewAnswer.Hard, position: 0);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Forgotten_word_comes_back_later_in_the_session_and_undo_takes_it_back()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1, 2);
        var session = await StartAsync(client);
        var forgottenCard = session.Current!.CardId;

        var forgot = await AnswerOkAsync(client, session, ReviewAnswer.Forgot);

        Assert.Equal(session.Total + 1, forgot.Session.Total);
        Assert.Equal(15 * 60, forgot.IntervalSeconds);
        Assert.NotNull(forgot.Session.UndoableEventId);

        var undone = await client.PostAsync($"/api/reviews/{forgot.EventId}/undo", null);
        Assert.Equal(HttpStatusCode.OK, undone.StatusCode);
        var afterUndo = (await undone.Content.ReadFromJsonAsync<SessionDto>(Json))!;
        Assert.Equal(session.Total, afterUndo.Total);
        Assert.Equal(0, afterUndo.Position);
        Assert.Equal(forgottenCard, afterUndo.Current!.CardId);
        Assert.Equal(0, afterUndo.Summary.Answered);

        // The card is new again: Easy gives the first-success interval, not a relearning one.
        var easy = await AnswerOkAsync(client, afterUndo, ReviewAnswer.Easy);
        Assert.Equal(24 * 3600, easy.IntervalSeconds);
    }

    [Fact]
    public async Task Undo_is_refused_after_the_window()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1);
        var session = await StartAsync(client);
        var answer = await AnswerOkAsync(client, session, ReviewAnswer.Easy);

        factory.Time.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/reviews/{answer.EventId}/undo", null)).StatusCode);
    }

    [Fact]
    public async Task Finishing_the_queue_completes_the_session_and_words_come_back_when_due()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1);
        var session = await StartAsync(client);

        while (session.Current is not null)
        {
            session = (await AnswerOkAsync(client, session, ReviewAnswer.Easy)).Session;
        }

        Assert.Equal(StudySessionStatus.Completed, session.Status);
        Assert.Equal(new SessionSummaryDto(10, 0, 0, 10, 10), session.Summary);
        Assert.Equal(0, (await client.GetFromJsonAsync<TodayDto>("/api/today", Json))!.DueNow);

        factory.Time.Advance(TimeSpan.FromDays(1));
        client = await factory.SignedInClientAsync(ApiFactory.UserA); // a day later the app is reopened
        var today = (await client.GetFromJsonAsync<TodayDto>("/api/today", Json))!;
        var next = await StartAsync(client);

        Assert.Equal(10, today.DueNow);
        Assert.Equal(SessionItemKind.Review, next.Current!.Kind);
        Assert.Equal(10, next.Total);
    }

    [Fact]
    public async Task Other_user_cannot_touch_my_session_or_lessons()
    {
        await using var factory = NewFactory();
        var mine = await factory.SignedInClientAsync(ApiFactory.UserA);
        var theirs = await factory.SignedInClientAsync(ApiFactory.UserB);
        await ImportAsync(mine, 1);
        var lesson = await AssignSuggestedAsync(mine);
        var session = await StartAsync(mine);
        var answer = await AnswerOkAsync(mine, session, ReviewAnswer.Easy);

        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/sessions/{session.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AnswerAsync(theirs, answer.Session, ReviewAnswer.Easy)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"/api/reviews/{answer.EventId}/undo", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PatchAsJsonAsync($"/api/assignments/{lesson.Id}", new UpdateAssignmentRequest(null, null, AssignmentStatus.Done, null), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await theirs.GetAsync("/api/sessions/current")).StatusCode);
        Assert.Empty((await theirs.GetFromJsonAsync<List<AssignmentDto>>("/api/assignments", Json))!);

        // Replaying my event id from another account must not reveal or change anything.
        var replay = await theirs.PostAsJsonAsync($"/api/sessions/{session.Id}/answers", new AnswerRequest(answer.EventId, 0, ReviewAnswer.Forgot, false, null), Json);
        Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
    }

    [Fact]
    public async Task Handing_in_a_lesson_keeps_its_words_in_review()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1);
        var lesson = await AssignSuggestedAsync(client);
        var session = await StartAsync(client);
        while (session.Current is not null)
        {
            session = (await AnswerOkAsync(client, session, ReviewAnswer.Hard)).Session;
        }

        var done = await client.PatchAsJsonAsync($"/api/assignments/{lesson.Id}", new UpdateAssignmentRequest(null, null, AssignmentStatus.Done, null), Json);
        factory.Time.Advance(TimeSpan.FromHours(6));
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var next = await StartAsync(client);

        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        Assert.Null((await client.GetFromJsonAsync<TodayDto>("/api/today", Json))!.NextLesson);
        Assert.Equal(10, next.Total);
    }

    [Fact]
    public async Task Undo_in_a_finished_session_is_refused_once_a_new_session_is_running()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1, 2);
        var first = await StartAsync(client, minutes: 1); // 4 prompts
        AnswerResultDto last = null!;
        while (first.Current is not null)
        {
            last = await AnswerOkAsync(client, first, ReviewAnswer.Easy);
            first = last.Session;
        }

        var second = await StartAsync(client, minutes: 1);
        var undo = await client.PostAsync($"/api/reviews/{last.EventId}/undo", null);

        Assert.Equal(StudySessionStatus.Completed, first.Status);
        Assert.Equal(StudySessionStatus.Active, second.Status);
        Assert.Equal(HttpStatusCode.Conflict, undo.StatusCode);
        Assert.Equal(second.Id, (await client.GetFromJsonAsync<SessionDto>("/api/sessions/current", Json))!.Id);
        Assert.Equal(StudySessionStatus.Completed, (await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{first.Id}", Json))!.Status);
    }

    [Fact]
    public async Task Undo_is_refused_after_a_restart()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1);
        var session = await StartAsync(client);
        var answer = await AnswerOkAsync(client, session, ReviewAnswer.Easy);

        await StartAsync(client, restart: true);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/reviews/{answer.EventId}/undo", null)).StatusCode);
    }

    [Fact]
    public async Task Undo_does_not_overwrite_a_later_result_for_the_same_card()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1);
        var session = await StartAsync(client);
        var answer = await AnswerOkAsync(client, session, ReviewAnswer.Easy);

        // The same card is answered again elsewhere (another device) a moment later.
        factory.Time.Advance(TimeSpan.FromSeconds(30));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KoreanAiTeacher.Infrastructure.Persistence.AppDbContext>();
            var card = await db.Cards.SingleAsync(c => c.Id == session.Current!.CardId);
            card.LastReviewedAt = factory.Time.GetUtcNow();
            card.DueAt = factory.Time.GetUtcNow().AddDays(3);
            await db.SaveChangesAsync();
        }

        var undo = await client.PostAsync($"/api/reviews/{answer.EventId}/undo", null);

        Assert.Equal(HttpStatusCode.Conflict, undo.StatusCode);
    }

    [Fact]
    public async Task Word_removed_from_the_vocabulary_leaves_a_running_session()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var job = await ImportAsync(client, 1);
        var session = await StartAsync(client);
        var removedWord = session.Current!.WordSenseId;
        var entry = (await client.GetFromJsonAsync<List<SourceEntryDto>>($"/api/imports/{job.Id}/entries?table=1", Json))!
            .Single(e => e.Korean == session.Current.Prompt);

        (await client.PatchAsJsonAsync($"/api/entries/{entry.Id}", new UpdateEntryRequest(null, null, null, EntryStatus.Excluded, entry.Version), Json)).EnsureSuccessStatusCode();
        var staleAnswer = await AnswerAsync(client, session, ReviewAnswer.Easy);
        var current = (await client.GetFromJsonAsync<SessionDto>("/api/sessions/current", Json))!;

        Assert.Equal(HttpStatusCode.Conflict, staleAnswer.StatusCode);
        Assert.NotEqual(removedWord, current.Current!.WordSenseId);

        // Its reverse, still queued further down, is stepped over as well.
        var seen = new List<Guid>();
        while (current.Current is not null)
        {
            seen.Add(current.Current.WordSenseId);
            current = (await AnswerOkAsync(client, current, ReviewAnswer.Easy)).Session;
        }

        Assert.DoesNotContain(removedWord, seen);
        Assert.Equal(8, seen.Count);
        Assert.Equal(StudySessionStatus.Completed, current.Status);
    }

    [Fact]
    public async Task Lessons_planned_ahead_give_new_words_nearest_lesson_first()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1, 2, 3, 4);
        await AssignSuggestedAsync(client); // tables 1–2, Tuesday
        await AssignSuggestedAsync(client); // tables 3–4, Thursday, planned before 1–2 is handed in

        var today = (await client.GetFromJsonAsync<TodayDto>("/api/today", Json))!;
        var session = await StartAsync(client);

        Assert.Equal(40, today.NewAvailable);
        Assert.Equal(40, session.Total);
        Assert.Equal("가가", session.Current!.Prompt);
        Assert.Equal(1, session.Current.TableNumber);
    }

    [Fact]
    public async Task Whole_lesson_practice_asks_both_tables_also_when_not_due()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1, 2, 3);
        var lesson = await AssignSuggestedAsync(client);
        var session = await StartAsync(client);
        while (session.Current is not null)
        {
            session = (await AnswerOkAsync(client, session, ReviewAnswer.Easy)).Session;
        }

        // Nothing is due now, yet the learner wants to go over the lesson before the test.
        var response = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(1, false, lesson.Id), Json);
        var drill = (await response.Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;
        DateTimeOffset? dueBefore;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KoreanAiTeacher.Infrastructure.Persistence.AppDbContext>();
            dueBefore = (await db.Cards.AsNoTracking().SingleAsync(c => c.Id == drill.Current!.CardId)).DueAt;
        }

        var answer = await AnswerOkAsync(client, drill, ReviewAnswer.Easy);

        Assert.Equal(20, drill.Total); // 2 tables × 5 words × 2 directions; the minute limit does not cut a lesson
        Assert.Contains(drill.Current!.TableNumber, lesson.Tables);
        Assert.Equal(SessionItemKind.LessonReview, drill.Current.Kind);
        Assert.False(drill.Current.IsDue);
        Assert.Equal(dueBefore, answer.NextDueAt); // an early answer does not move the card up the ladder
        Assert.Equal(1, answer.Session.Position);
    }

    [Fact]
    public async Task Whole_lesson_practice_of_another_users_lesson_is_not_found()
    {
        await using var factory = NewFactory();
        var a = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(a, 1, 2);
        var lesson = await AssignSuggestedAsync(a);
        var b = await factory.SignedInClientAsync(ApiFactory.UserB);

        var response = await b.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, false, lesson.Id), Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Approved_tables_are_listed_and_a_table_comes_in_book_order_for_studying()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1, 3);

        var tables = (await client.GetFromJsonAsync<List<VocabTableDto>>("/api/vocabulary/tables", Json))!;
        var third = (await client.GetFromJsonAsync<VocabTableWordsDto>($"/api/vocabulary/tables/{tables[0].SourceDocumentId}/3", Json))!;
        var unapproved = await client.GetAsync($"/api/vocabulary/tables/{tables[0].SourceDocumentId}/2");
        var other = await (await factory.SignedInClientAsync(ApiFactory.UserB)).GetAsync($"/api/vocabulary/tables/{tables[0].SourceDocumentId}/3");

        Assert.Equal([(1, 5, 0), (3, 5, 0)], tables.Select(t => (t.TableNumber, t.Words, t.Started)));
        Assert.Equal("lugat.pdf", tables[0].FileName);
        Assert.Equal(["다가", "다각", "다갂", "다갃", "다간"], third.Words.Select(w => w.Korean));
        Assert.Equal([1, 2, 3, 4, 5], third.Words.Select(w => w.Number));
        Assert.Equal("ma’no 1", third.Words[0].Uzbek);
        Assert.Equal(HttpStatusCode.NotFound, unapproved.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task After_studying_a_table_the_cards_drill_only_that_table_without_making_a_lesson()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1, 3);
        var document = (await client.GetFromJsonAsync<List<VocabTableDto>>("/api/vocabulary/tables", Json))![0].SourceDocumentId;

        var response = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true, null, document, 3), Json);
        var session = (await response.Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;
        var first = await AnswerOkAsync(client, session, ReviewAnswer.Easy);
        var tables = (await client.GetFromJsonAsync<List<VocabTableDto>>("/api/vocabulary/tables", Json))!;
        var lessons = (await client.GetFromJsonAsync<List<AssignmentDto>>("/api/assignments", Json))!;
        var noDocument = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true, null, null, 3), Json);
        var empty = (await (await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true, null, document, 2), Json))
            .Content.ReadFromJsonAsync<StartSessionResponse>(Json))!;

        Assert.Equal(10, session.Total); // 5 words × 2 directions, all of table 3
        Assert.Equal(3, session.Current!.TableNumber);
        Assert.Equal(3, first.Session.Current!.TableNumber);
        Assert.Equal([0, 1], tables.Select(t => t.Started));
        Assert.Empty(lessons);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noDocument.StatusCode);
        Assert.Null(empty.Session);
        Assert.Contains("jadvalda", empty.Message);
    }

    [Fact]
    public async Task The_mistakes_notebook_gathers_card_and_test_mistakes_and_drills_only_those_words()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(client, 1);
        var empty = (await client.GetFromJsonAsync<MistakesDto>("/api/mistakes", Json))!;

        // Forget the first card, then know the second card of the same session.
        var session = await StartAsync(client);
        var forgotten = session.Current!.WordSenseId;
        session = (await AnswerOkAsync(client, session, ReviewAnswer.Forgot)).Session;
        var known = session.Current!.WordSenseId;
        await AnswerOkAsync(client, session, ReviewAnswer.Easy);

        // A word test finished without answers: every question counts as a mistake, as in its score.
        var created = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [1], null, false, 10), Json);
        var test = (await created.Content.ReadFromJsonAsync<TestDto>(Json))!;
        await client.PostAsync($"/api/tests/{test.Id}/finish", null);

        var notebook = (await client.GetFromJsonAsync<MistakesDto>("/api/mistakes", Json))!;
        var top = notebook.Words[0];

        Assert.Empty(empty.Words);
        Assert.Equal(5, notebook.Words.Count); // every word was in the unanswered test
        Assert.Equal(forgotten, top.WordSenseId); // forgotten on a card and wrong in the test: most mistakes
        Assert.Equal(["kartochka", "sinov"], top.Sources.Order());
        Assert.True(top.Mistakes >= 2);
        Assert.All(notebook.Words, w => Assert.False(w.Improving)); // the test came after the right card answer
        Assert.Equal(1, top.TableNumber);

        var drill = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true, WordSenseIds: [forgotten, known]), Json);
        var drilled = (await drill.Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;
        var tooMany = await client.PostAsJsonAsync("/api/sessions",
            new StartSessionRequest(null, true, WordSenseIds: Enumerable.Range(0, 61).Select(_ => Guid.NewGuid()).ToList()), Json);

        Assert.Equal(4, drilled.Total); // two words × two directions
        Assert.Contains(drilled.Current!.WordSenseId, new[] { forgotten, known });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMany.StatusCode);
    }
}
