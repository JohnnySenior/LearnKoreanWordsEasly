using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Api.Tests.Speech;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Testing;

namespace Lugat.Api.Tests.Testing;

/// <summary>Practice tests (D-30): mixed questions from the learner's words, answers hidden until the end.</summary>
public class TestEndpointsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FakeAiProvider _ai = new(AiProviderKind.OpenAi);

    private ApiFactory NewFactory(bool withAi = true) => new()
    {
        TableWords = [("학교", "maktab"), ("가다", "bormoq"), ("먹다", "yemoq"), ("보다", "ko‘rmoq"), ("자다", "uxlamoq"), ("읽다", "o‘qimoq")],
        AiProviders = withAi ? [_ai] : [],
    };

    private static async Task ApproveAsync(HttpClient client)
    {
        (await client.PostAsync(ApiFactory.TestWordsPath, null)).EnsureSuccessStatusCode();
    }

    /// <summary>A gap for every task line in the request: "학교" fills the blank.</summary>
    private static string Gaps(string userContent)
    {
        var ids = userContent.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && char.IsDigit(l[0])).Select(l => int.Parse(l[..l.IndexOf('.')]));
        var items = ids.Select(id => $$"""
            {"id":{{id}},"sentence":"저는 매일 ___에 가요.","translationUz":"Men har kuni maktabga boraman.","answer":"학교",
             "options":["학교","가다","먹다","보다"],"explanationUz":"Maktab — boriladigan joy."}
            """);
        return $$"""{"items":[{{string.Join(",", items)}}]}""";
    }

    private static async Task<TestDto> CreateAsync(HttpClient client, int count = 10, Guid? id = null)
    {
        var response = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(id ?? Guid.NewGuid(), [1], count), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TestDto>(Json))!;
    }

    [Fact]
    public async Task A_test_mixes_question_types_and_hides_answers_until_the_end()
    {
        _ai.Answer = r => Gaps(r.UserContent);
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);

        var options = (await client.GetFromJsonAsync<TestOptionsDto>("/api/tests/options", Json))!;
        var id = Guid.NewGuid();
        var test = await CreateAsync(client, 10, id);
        var again = await CreateAsync(client, 10, id);

        Assert.Equal(6, Assert.Single(options.Tables).Words);
        Assert.Equal(test.Id, again.Id);
        Assert.Equal(1, _ai.Requests.Count(r => r.Feature == "test-gaps")); // one AI call per test, not per retry
        Assert.Equal(10, test.Questions.Count);
        Assert.Equal("1-jadval", test.Title);
        Assert.Contains(test.Questions, q => q.Kind == TestQuestionKind.WordMeaning);
        Assert.True(test.Questions.Any(q => q.Kind == TestQuestionKind.WordCloze), test.Warning + string.Join(",", test.Questions.Select(q => q.Kind)));
        Assert.Contains(test.Questions, q => q.Kind == TestQuestionKind.WordClozeTyped);
        Assert.All(test.Questions, q => Assert.Null(q.Correct));
        Assert.All(test.Questions.Where(q => q.Kind == TestQuestionKind.WordCloze), q => Assert.Null(q.Translation)); // would give it away
        Assert.All(test.Questions.Where(q => q.Kind == TestQuestionKind.WordClozeTyped), q => Assert.NotNull(q.Translation));

        // Answer everything: gaps right, choices by picking the first option.
        foreach (var q in test.Questions)
        {
            var answer = q.Kind is TestQuestionKind.WordCloze or TestQuestionKind.WordClozeTyped ? "학교" : q.Options[0];
            (await client.PostAsJsonAsync($"/api/tests/{test.Id}/answers", new TestAnswerRequest(q.Index, answer), Json)).EnsureSuccessStatusCode();
        }

        var badOption = await client.PostAsJsonAsync($"/api/tests/{test.Id}/answers", new TestAnswerRequest(test.Questions.First(q => q.Options.Count > 0).Index, "없는 답"), Json);
        var finished = (await (await client.PostAsync($"/api/tests/{test.Id}/finish", null)).Content.ReadFromJsonAsync<TestDto>(Json))!;
        var late = await client.PostAsJsonAsync($"/api/tests/{test.Id}/answers", new TestAnswerRequest(0, "학교"), Json);
        var foreign = await (await factory.SignedInClientAsync(ApiFactory.UserB)).GetAsync($"/api/tests/{test.Id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, badOption.StatusCode);
        Assert.Equal(TestRunStatus.Finished, finished.Status);
        Assert.All(finished.Questions, q => Assert.NotNull(q.Correct));
        Assert.All(finished.Questions.Where(q => q.Kind is TestQuestionKind.WordCloze or TestQuestionKind.WordClozeTyped), q => Assert.True(q.IsCorrect));
        Assert.Equal(finished.Questions.Count(q => q.IsCorrect == true), finished.Score);
        Assert.Equal(finished.Questions.Count, finished.ByKind.Sum(k => k.Total));
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Without_ai_the_test_has_only_choice_questions_and_says_so()
    {
        await using var factory = NewFactory(withAi: false);
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);

        var test = await CreateAsync(client, 20);

        Assert.Equal(20, test.Questions.Count);
        Assert.All(test.Questions, q => Assert.Contains(q.Kind, new[] { TestQuestionKind.WordMeaning, TestQuestionKind.WordKorean }));
        Assert.All(test.Questions, q => Assert.Equal(4, q.Options.Distinct().Count()));
        Assert.Contains("AI", test.Warning);
    }

    [Fact]
    public async Task Mistakes_can_be_practised_again_and_bad_ai_gaps_are_replaced()
    {
        // The AI returns a gap whose answer also stands outside the blank: it must be dropped.
        _ai.Answer = r => Gaps(r.UserContent).Replace("저는 매일 ___에 가요.", "학교: 저는 매일 ___에 가요.");
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);
        var test = await CreateAsync(client, 10);

        var unanswered = test.Questions[0];
        foreach (var q in test.Questions.Skip(1))
        {
            await client.PostAsJsonAsync($"/api/tests/{test.Id}/answers", new TestAnswerRequest(q.Index, q.Options[0]), Json);
        }

        var finished = (await (await client.PostAsync($"/api/tests/{test.Id}/finish", null)).Content.ReadFromJsonAsync<TestDto>(Json))!;
        var retryResponse = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), null, 0, test.Id), Json);
        var retry = (await retryResponse.Content.ReadFromJsonAsync<TestDto>(Json))!;

        Assert.All(test.Questions, q => Assert.False(q.AiMade));
        Assert.Contains("almashtirildi", test.Warning);
        Assert.Equal("Javob berilmagan", finished.Questions.Single(q => q.Index == unanswered.Index).Note);
        Assert.Equal(HttpStatusCode.Created, retryResponse.StatusCode);
        Assert.Equal(finished.Questions.Count(q => q.IsCorrect != true), retry.Questions.Count);
        Assert.EndsWith("xatolar", retry.Title);
        Assert.Equal(1, _ai.Requests.Count(r => r.Feature == "test-gaps")); // the retry reuses the questions
    }

    private static async Task<TestDto> OralAsync(HttpClient client, TestDto test, int index, bool assisted = false)
    {
        var data = new byte[8000];
        "RIFF"u8.CopyTo(data);
        var audio = new ByteArrayContent(data);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        using var form = new MultipartFormDataContent { { audio, "audio", "speech.wav" }, { new StringContent(assisted ? "1" : "0"), "assisted" } };
        var response = await client.PostAsync($"/api/tests/{test.Id}/oral/{index}", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TestDto>(Json))!;
    }

    [Fact]
    public async Task Oral_readiness_asks_every_word_once_in_alternating_directions_and_grades_what_was_heard()
    {
        var azure = new FakePronunciation();
        await using var factory = new ApiFactory
        {
            TableWords = [("학교", "maktab"), ("가다", "bormoq"), ("먹다", "yemoq"), ("보다", "ko‘rmoq"), ("자다", "uxlamoq"), ("읽다", "o‘qimoq")],
            Pronunciation = azure,
        };
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);

        var response = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [1], 0, Oral: true), Json);
        var test = (await response.Content.ReadFromJsonAsync<TestDto>(Json))!;
        var say = test.Questions.Where(q => q.Kind == TestQuestionKind.OralKorean).ToList();
        var mean = test.Questions.Where(q => q.Kind == TestQuestionKind.OralMeaning).ToList();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(6, test.Questions.Count);
        Assert.Equal((3, 3), (say.Count, mean.Count));
        Assert.Equal(6, say.Select(q => q.Prompt).Concat(mean.Select(q => q.Translation)).Distinct().Count()); // each word once
        Assert.All(test.Questions, q => Assert.Null(q.Correct));
        Assert.All(say, q => Assert.EndsWith("…", q.Hint));
        Assert.All(mean, q => Assert.Equal(["Bildim", "Bilmadim"], q.Options));
        Assert.All(mean, q => Assert.NotNull(q.Translation)); // what the learner checks themselves against

        // 1st: heard exactly; 2nd: one sound off; 3rd: another word. An oral question cannot be answered by typing.
        var heard = new Queue<Func<string, string>>([r => r, r => r[..^1] + (char)(r[^1] + 1), _ => "바다"]);
        azure.Respond = reference => new SpeakingResultDto(true, heard.Dequeue()(reference!) + ".", 70, 75, 80, 100, [], null);
        await OralAsync(client, test, say[0].Index, assisted: true);
        await OralAsync(client, test, say[1].Index);
        var afterOral = await OralAsync(client, test, say[2].Index);
        var typed = await client.PostAsJsonAsync($"/api/tests/{test.Id}/answers", new TestAnswerRequest(say[0].Index, "학교"), Json);
        await client.PostAsJsonAsync($"/api/tests/{test.Id}/answers", new TestAnswerRequest(mean[0].Index, "Bildim"), Json);
        await client.PostAsJsonAsync($"/api/tests/{test.Id}/answers", new TestAnswerRequest(mean[1].Index, "Bilmadim"), Json);
        var done = (await (await client.PostAsync($"/api/tests/{test.Id}/finish", null)).Content.ReadFromJsonAsync<TestDto>(Json))!;
        Dictionary<int, TestQuestionDto> byIndex = done.Questions.ToDictionary(q => q.Index);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, typed.StatusCode);
        Assert.Null(afterOral.Questions.Single(q => q.Index == say[2].Index).IsCorrect); // the verdict waits for the end
        Assert.Equal("바다.", afterOral.Questions.Single(q => q.Index == say[2].Index).Given); // the transcript does not
        Assert.Equal(say.Select(q => q.Prompt), azure.Calls.Select(c => done.Questions.Single(q => q.Correct == c.Reference).Prompt));
        Assert.True(byIndex[say[0].Index] is { IsCorrect: true, Assisted: true, Hard: false, PronScore: 70 });
        Assert.True(byIndex[say[1].Index] is { IsCorrect: false, Hard: false }); // two syllables: one sound off is another word
        Assert.Contains("Eshitildi", byIndex[say[1].Index].Note);
        Assert.True(byIndex[say[2].Index] is { IsCorrect: false });
        Assert.Equal((true, false), (byIndex[mean[0].Index].IsCorrect, byIndex[mean[1].Index].IsCorrect));
        Assert.Equal(2, done.Score);
        Assert.Contains(done.ByKind, k => k is { Kind: TestQuestionKind.OralKorean, Correct: 1, Total: 3 });
    }

    [Fact]
    public async Task A_microphone_failure_is_recorded_apart_and_silence_is_not_stored()
    {
        var azure = new FakePronunciation { Respond = _ => new SpeakingResultDto(false, "", null, null, null, null, [], "Nutq eshitilmadi.") };
        await using var factory = new ApiFactory { TableWords = NewFactory().TableWords, Pronunciation = azure };
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);
        var test = (await (await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [1], 20, Oral: true), Json))
            .Content.ReadFromJsonAsync<TestDto>(Json))!;
        var oral = test.Questions.First(q => q.Kind == TestQuestionKind.OralKorean);

        var data = new byte[8000];
        "RIFF"u8.CopyTo(data);
        using var form = new MultipartFormDataContent { { new ByteArrayContent(data), "audio", "speech.wav" } };
        var silent = await client.PostAsync($"/api/tests/{test.Id}/oral/{oral.Index}", form);
        var technical = (await (await client.PostAsync($"/api/tests/{test.Id}/oral/{oral.Index}/technical", null)).Content.ReadFromJsonAsync<TestDto>(Json))!;
        var wrongKind = await client.PostAsync($"/api/tests/{test.Id}/oral/{test.Questions.First(q => q.Kind == TestQuestionKind.OralMeaning).Index}/technical", null);
        var badCount = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [1], 10, Oral: true), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, silent.StatusCode);
        Assert.Contains("eshitilmadi", await silent.Content.ReadAsStringAsync());
        Assert.Equal(6, test.Questions.Count); // 20 asked, the table has 6 words
        Assert.True(technical.Questions.Single(q => q.Index == oral.Index).Technical);
        Assert.Equal(1, technical.Answered);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongKind.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badCount.StatusCode);
    }

    [Fact]
    public async Task The_history_knows_each_tests_kind_and_can_be_filtered()
    {
        await using var factory = NewFactory(withAi: false);
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);
        await CreateAsync(client);
        (await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [1], 20, Oral: true), Json)).EnsureSuccessStatusCode();

        var all = (await client.GetFromJsonAsync<List<TestSummaryDto>>("/api/tests", Json))!;
        var oral = (await client.GetFromJsonAsync<List<TestSummaryDto>>("/api/tests?category=Oral", Json))!;

        Assert.Equal([TestCategory.Oral, TestCategory.Vocabulary], all.Select(t => t.Category));
        Assert.Equal(TestCategory.Oral, Assert.Single(oral).Category);
        Assert.Equal(6, oral[0].Counted); // no technical failures yet: every question counts
    }
}
