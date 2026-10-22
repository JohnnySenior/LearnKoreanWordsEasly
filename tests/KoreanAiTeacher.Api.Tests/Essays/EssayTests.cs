using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KoreanAiTeacher.Api.Tests.Ai;
using KoreanAiTeacher.Api.Tests.Infrastructure;
using KoreanAiTeacher.Application.Essays;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Domain.Ai;
using KoreanAiTeacher.Domain.Essays;

namespace KoreanAiTeacher.Api.Tests.Essays;

/// <summary>TOPIK II writing 51–54 (D-49): AI-made or own questions, marked with the official criteria.</summary>
public class EssayTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FakeAiProvider _ai = new(AiProviderKind.OpenAi);

    private ApiFactory NewFactory() => new() { AiProviders = [_ai] };

    private const string Prompt53 = """
        {"instructionKo":"다음을 참고하여 '여가 시간 활용'에 대한 글을 200~300자로 쓰십시오.",
         "text":"20대 이상 성인 3,000명 대상 조사\n2015년 30% → 2025년 55%\n원인: 주 5일 근무, 취미 증가",
         "modelAnswers":["최근 여가 시간을 활용하는 사람이 늘고 있다."]}
        """;

    /// <summary>Scores above the maximum (9 of 7) must be clamped; a correction without change is dropped.</summary>
    private const string Check53 = """
        {"criteria":[{"score":9,"commentUz":"Barcha ma’lumot bor."},{"score":5,"commentUz":"Bog‘lovchilar kam."},{"score":10,"commentUz":"Uslub aralash."}],
         "corrections":[{"original":"늘었어요","corrected":"늘었다","explanationUz":"Yozma uslubda -았/었다."},{"original":"같다","corrected":"같다","explanationUz":"O‘zgarish yo‘q."}],
         "summaryUz":"Yaxshi boshlanish, yozma uslubga e’tibor bering.","improved":"최근 여가 시간을 활용하는 사람이 크게 늘었다."}
        """;

    private static async Task<EssayDto> CreateAsync(HttpClient client, CreateEssayRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/essays", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EssayDto>(Json))!;
    }

    [Fact]
    public async Task An_ai_question_is_written_checked_with_the_official_criteria_and_can_be_written_again()
    {
        _ai.Answer = r => r.Feature == EssayService.PromptFeature ? Prompt53 : Check53;
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var id = Guid.NewGuid();
        var essay = await CreateAsync(client, new CreateEssayRequest(id, EssayTask.Q53, EssaySource.Ai));
        var again = await CreateAsync(client, new CreateEssayRequest(id, EssayTask.Q53, EssaySource.Ai));

        Assert.Equal(essay.Id, again.Id);
        Assert.Equal(1, _ai.Requests.Count(r => r.Feature == EssayService.PromptFeature));
        Assert.Equal((200, 300, 30), (essay.MinChars!.Value, essay.MaxChars!.Value, essay.MaxScore));
        Assert.Contains("여가 시간", essay.InstructionKo);
        Assert.Contains("2025년 55%", essay.Text);
        Assert.Empty(essay.ModelAnswers); // hidden until checked

        var tooShort = await client.PostAsJsonAsync($"/api/essays/{id}/submit", new SubmitEssayRequest(["짧다"]), Json);
        var text = "최근 여가 시간을 활용하는 사람이 늘었어요. 2015년에는 30%였지만 2025년에는 55%로 늘었다.";
        var checkedEssay = (await (await client.PostAsJsonAsync($"/api/essays/{id}/submit", new SubmitEssayRequest([text]), Json)).Content.ReadFromJsonAsync<EssayDto>(Json))!;
        var resubmit = (await (await client.PostAsJsonAsync($"/api/essays/{id}/submit", new SubmitEssayRequest(["다른 글을 다시 보냅니다. 이것은 스무 자가 넘는 글이다."]), Json)).Content.ReadFromJsonAsync<EssayDto>(Json))!;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooShort.StatusCode);
        Assert.Equal(EssayStatus.Checked, checkedEssay.Status);
        var fb = checkedEssay.Feedback!;
        Assert.Equal(["Mazmun va topshiriq", "Matn tuzilishi", "Til ishlatish"], fb.Criteria.Select(c => c.Name));
        Assert.Equal([7, 5, 10], fb.Criteria.Select(c => c.Score)); // 9 clamped to 7
        Assert.Equal((22, 30), (fb.Score, fb.Max));
        Assert.Equal("늘었다", Assert.Single(fb.Corrections).Corrected);
        Assert.Single(checkedEssay.ModelAnswers);
        Assert.Contains($"characters counted as on the answer sheet: {EssayService.CountChars(text)}", _ai.Requests.Last(r => r.Feature == EssayService.CheckFeature).UserContent);
        Assert.Equal(text, Assert.Single(resubmit.Answers)); // already marked: no second check
        Assert.Equal(1, _ai.Requests.Count(r => r.Feature == EssayService.CheckFeature));

        var retryId = Guid.NewGuid();
        var retry = (await (await client.PostAsJsonAsync($"/api/essays/{id}/retry", new RetryEssayRequest(retryId), Json)).Content.ReadFromJsonAsync<EssayDto>(Json))!;
        var list = (await client.GetFromJsonAsync<List<EssaySummaryDto>>("/api/essays", Json))!;
        var foreign = await (await factory.SignedInClientAsync(ApiFactory.UserB)).GetAsync($"/api/essays/{id}");

        Assert.Equal((retryId, EssayStatus.Writing, essay.Text), (retry.Id, retry.Status, retry.Text));
        Assert.Empty(retry.Answers);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, e => e.Score == 22);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task An_own_gap_question_needs_both_gaps_and_a_failed_check_keeps_the_answers()
    {
        _ai.Answer = _ => "not json";
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var noGaps = await client.PostAsJsonAsync("/api/essays", new CreateEssayRequest(Guid.NewGuid(), EssayTask.Q51, EssaySource.Own, null, "안녕하세요. 다음 주에 모임이 있습니다."), Json);
        var brokenAi = await client.PostAsJsonAsync("/api/essays", new CreateEssayRequest(Guid.NewGuid(), EssayTask.Q54, EssaySource.Ai), Json);
        var essay = await CreateAsync(client, new CreateEssayRequest(Guid.NewGuid(), EssayTask.Q51, EssaySource.Own, null, "안녕하세요. 이번 모임에 ( ㉠ ). 그래서 ( ㉡ )."));

        var oneGap = await client.PostAsJsonAsync($"/api/essays/{essay.Id}/submit", new SubmitEssayRequest(["참석하지 못합니다"]), Json);
        var failed = await client.PostAsJsonAsync($"/api/essays/{essay.Id}/submit", new SubmitEssayRequest(["참석하지 못합니다", "다음에 뵙겠습니다"]), Json);
        var saved = (await client.GetFromJsonAsync<EssayDto>($"/api/essays/{essay.Id}", Json))!;

        _ai.Answer = _ => """
            {"criteria":[{"score":5,"commentUz":"To‘g‘ri."},{"score":3,"commentUz":"Uslub sal norasmiy."}],
             "corrections":[],"summaryUz":"Yaxshi.","improved":"㉠: 참석하지 못할 것 같습니다\n㉡: 다음에 뵙겠습니다"}
            """;
        var marked = (await (await client.PostAsJsonAsync($"/api/essays/{essay.Id}/submit", new SubmitEssayRequest(saved.Answers), Json)).Content.ReadFromJsonAsync<EssayDto>(Json))!;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, noGaps.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, brokenAi.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, oneGap.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal(EssayStatus.Writing, saved.Status);
        Assert.Equal(["참석하지 못합니다", "다음에 뵙겠습니다"], saved.Answers); // kept for the next try (REL01)
        Assert.Equal(EssaySource.Own, saved.Source);
        Assert.StartsWith("다음 글의 ㉠과 ㉡", saved.InstructionKo);
        Assert.Equal(["㉠", "㉡"], marked.Feedback!.Criteria.Select(c => c.Name));
        Assert.Equal((8, 10), (marked.Feedback.Score, marked.Feedback.Max));
        Assert.Empty(marked.ModelAnswers); // own question: no AI model answer
    }
    [Theory]
    [InlineData("[null,null,null]")]
    [InlineData("[{\"commentUz\":\"Yaxshi.\"},{\"score\":5,\"commentUz\":\"Yaxshi.\"},{\"score\":5,\"commentUz\":\"Yaxshi.\"}]")]
    [InlineData("[{\"score\":5},{\"score\":5,\"commentUz\":\"Yaxshi.\"},{\"score\":5,\"commentUz\":\"Yaxshi.\"}]")]
    public void Missing_criteria_are_technical_failures_not_zero_marks(string criteria)
    {
        var json = "{\"criteria\":" + criteria + ",\"summaryUz\":\"Yaxshi.\",\"corrections\":[],\"improved\":null}";
        Assert.Null(EssayService.ParseCheck(json, EssayTask.Q53));
    }

    [Fact]
    public async Task Drafts_survive_reloading_and_stale_or_foreign_updates_cannot_overwrite_them()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var essay = await CreateAsync(client, new CreateEssayRequest(Guid.NewGuid(), EssayTask.Q53, EssaySource.Own, null, "여가 시간 활용에 대한 글을 쓰십시오."));
        var draft = new SaveEssayDraftRequest(["아직 쓰고 있는 글"], essay.Version);
        var response = await client.PutAsJsonAsync($"/api/essays/{essay.Id}/draft", draft, Json);
        response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<EssayDto>(Json))!;
        var loaded = (await client.GetFromJsonAsync<EssayDto>($"/api/essays/{essay.Id}", Json))!;
        Assert.Equal("아직 쓰고 있는 글", Assert.Single(loaded.Answers));
        Assert.NotEqual(essay.Version, saved.Version);
        Assert.Empty(_ai.Requests);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/essays/{essay.Id}/draft", draft with { Answers = ["이전 창"] }, Json)).StatusCode);
        var other = await factory.SignedInClientAsync(ApiFactory.UserB);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/essays/{essay.Id}/draft", draft with { Version = saved.Version }, Json)).StatusCode);
    }

}

