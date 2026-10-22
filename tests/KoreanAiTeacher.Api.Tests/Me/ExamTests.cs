using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KoreanAiTeacher.Api.Tests.Imports;
using KoreanAiTeacher.Api.Tests.Infrastructure;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Domain.Exams;
using KoreanAiTeacher.Domain.Learning;

namespace KoreanAiTeacher.Api.Tests.Me;

/// <summary>Exam date, countdown and pace (D-34).</summary>
public class ExamTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Monday 5 October 2026, 09:31 in Seoul.</summary>
    private static readonly DateTimeOffset MondayMorning = new DateTimeOffset(2026, 10, 5, 9, 31, 0, TimeSpan.FromHours(9)).ToUniversalTime();

    private readonly FakeMessenger _telegram = new();

    private ApiFactory NewFactory()
    {
        var factory = new ApiFactory
        {
            PdfReader = new FakePdfLayoutReader().AddPage(FakePdfLayoutReader.Words("가", 5)),
            Messenger = _telegram,
        };
        factory.Settings["Reminders:PollInterval"] = "01:00:00";
        factory.Time.SetUtcNow(MondayMorning.AddHours(-1));
        return factory;
    }

    [Fact]
    public async Task Without_a_date_the_plan_asks_for_one()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var exam = (await client.GetFromJsonAsync<ExamDto>("/api/exam", Json))!;

        Assert.Equal(ExamPhase.None, exam.Phase);
        Assert.Null(exam.ExamDate);
    }

    [Fact]
    public async Task The_date_gives_a_countdown_in_korea_time_and_a_daily_word_pace()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await RemindersTests.ApproveOneTableAsync(client);

        var saved = await client.PutAsJsonAsync("/api/exam", new UpdateExamRequest(new DateOnly(2026, 11, 15), 4), Json);
        var exam = (await saved.Content.ReadFromJsonAsync<ExamDto>(Json))!;

        // 08:31 on 5 October in Seoul: 41 days to 15 November, 34 of them for new material.
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal((ExamPhase.Study, 41, 34), (exam.Phase, exam.DaysLeft, exam.StudyDays));
        Assert.Equal(4, exam.TargetLevel);
        Assert.Equal(5, exam.NewWords);
        Assert.Equal(1, exam.WordsPerDay);
        Assert.Equal(0, exam.WordsStartedToday);
        Assert.Equal(0, exam.MocksLast7Days);
        Assert.Null(exam.LastMockScore);
    }

    [Fact]
    public async Task Words_started_today_are_counted_once_per_word()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await RemindersTests.ApproveOneTableAsync(client);
        await client.PutAsJsonAsync("/api/exam", new UpdateExamRequest(new DateOnly(2026, 11, 15), null), Json);
        var session = (await (await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, false), Json))
            .Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;

        for (var i = 0; i < 4 && session.Current is not null; i++)
        {
            session = (await (await client.PostAsJsonAsync($"/api/sessions/{session.Id}/answers", new AnswerRequest(Guid.NewGuid(), session.Position, ReviewAnswer.Easy, false, 2000), Json))
                .Content.ReadFromJsonAsync<AnswerResultDto>(Json))!.Session;
        }

        var exam = (await client.GetFromJsonAsync<ExamDto>("/api/exam", Json))!;

        Assert.InRange(exam.WordsStartedToday, 2, 4); // four answers, both directions of a word count as one
        Assert.Equal(5 - exam.WordsStartedToday, exam.NewWords);
    }

    [Fact]
    public async Task Dates_in_the_past_or_far_ahead_and_odd_levels_are_refused_and_the_date_can_be_cleared()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var past = await client.PutAsJsonAsync("/api/exam", new UpdateExamRequest(new DateOnly(2026, 10, 1), null), Json);
        var far = await client.PutAsJsonAsync("/api/exam", new UpdateExamRequest(new DateOnly(2030, 1, 1), null), Json);
        var level = await client.PutAsJsonAsync("/api/exam", new UpdateExamRequest(new DateOnly(2026, 11, 15), 2), Json);
        await client.PutAsJsonAsync("/api/exam", new UpdateExamRequest(new DateOnly(2026, 11, 15), 3), Json);
        var cleared = (await (await client.PutAsJsonAsync("/api/exam", new UpdateExamRequest(null, null), Json)).Content.ReadFromJsonAsync<ExamDto>(Json))!;
        var other = (await (await factory.SignedInClientAsync(ApiFactory.UserB)).GetFromJsonAsync<ExamDto>("/api/exam", Json))!;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, past.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, far.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, level.StatusCode);
        Assert.Contains("3 dan 6 gacha", await level.Content.ReadAsStringAsync());
        Assert.Equal((ExamPhase.None, (DateOnly?)null, (int?)null), (cleared.Phase, cleared.ExamDate, cleared.TargetLevel));
        Assert.Null(other.ExamDate);
    }

    [Fact]
    public async Task The_countdown_moves_with_the_clock_into_the_final_week_and_exam_day()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await client.PutAsJsonAsync("/api/exam", new UpdateExamRequest(new DateOnly(2026, 11, 15), null), Json);

        factory.Time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.FromHours(9)).ToUniversalTime());
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var finalWeek = (await client.GetFromJsonAsync<ExamDto>("/api/exam", Json))!;
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 11, 15, 0, 30, 0, TimeSpan.FromHours(9)).ToUniversalTime()); // just after midnight in Seoul
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var examDay = (await client.GetFromJsonAsync<ExamDto>("/api/exam", Json))!;

        Assert.Equal((ExamPhase.FinalWeek, 5, 0), (finalWeek.Phase, finalWeek.DaysLeft, finalWeek.WordsPerDay));
        Assert.Equal(ExamPhase.ExamDay, examDay.Phase);
    }

    [Fact]
    public async Task The_morning_reminder_mentions_the_days_left()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await RemindersTests.ApproveOneTableAsync(client);
        await client.PutAsJsonAsync("/api/exam", new UpdateExamRequest(new DateOnly(2026, 11, 15), null), Json);
        factory.Time.SetUtcNow(MondayMorning);

        await RemindersTests.RunAsync(factory);

        Assert.Contains("Imtihongacha 41 kun.", Assert.Single(_telegram.Sent).Text);
    }
}
