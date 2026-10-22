using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Vocabulary;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Testing;

namespace Lugat.Api.Tests.Vocabulary;

/// <summary>How words are used (D-46): prepared once per word by the AI, then practised without AI calls.</summary>
public class UsageTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FakeAiProvider _ai = new(AiProviderKind.OpenAi);

    private ApiFactory NewFactory() => new()
    {
        TableWords = [("열다", "ochmoq"), ("가져오다", "olib kelmoq"), ("먹다", "yemoq"), ("보다", "ko‘rmoq"), ("자다", "uxlamoq"), ("읽다", "o‘qimoq")],
        AiProviders = [_ai],
    };

    private static async Task ApproveAsync(HttpClient client) =>
        (await client.PostAsync(ApiFactory.TestWordsPath, null)).EnsureSuccessStatusCode();

    /// <summary>
    /// Usage for every "<id>. <korean> — <uzbek>" line except <paramref name="skip"/>. 열다 gets the "biznes ochmoq"
    /// trap; the second question of every word is broken (no gap) and must be dropped.
    /// </summary>
    private static string Usage(string userContent, params string[] skip)
    {
        var items = Regex.Matches(userContent, @"^(\d+)\. (\S+)", RegexOptions.Multiline)
            .Where(m => !skip.Contains(m.Groups[2].Value))
            .Select(m =>
            {
                var (id, word) = (m.Groups[1].Value, m.Groups[2].Value);
                var trap = word == "열다" ? "\"Biznes ochmoq — 열다 emas: 사업을 시작하다.\"" : "null";
                return $$"""
                    {"id":{{id}},"phrases":[{"korean":"{{word}} 1","uzbek":"birinchi ibora"},{"korean":"no hangul","uzbek":"x"}],
                     "trapUz":{{trap}},"example":{"korean":"저는 {{word}}.","uzbek":"Misol gap."},
                     "questions":[
                       {"uzbek":"O‘zbekcha gap.","sentence":"저는 ___.","answer":"{{word}}","wrong":["다른말","{{word}}"],"explanationUz":"Shu so‘z mos."},
                       {"uzbek":"Bo‘shliqsiz gap.","sentence":"저는 {{word}}.","answer":"{{word}}","wrong":["다른말"],"explanationUz":"Buzuq savol."}]}
                    """;
            });
        return $$"""{"words":[{{string.Join(",", items)}}]}""";
    }

    [Fact]
    public async Task Usage_is_prepared_once_per_word_and_only_well_formed_parts_are_kept()
    {
        _ai.Answer = r => Usage(r.UserContent);
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);
        const string url = "/api/vocabulary/tables/1/usage";

        var before = (await client.GetFromJsonAsync<TableUsageDto>(url, Json))!;
        var prepared = (await (await client.PostAsync(url, null)).Content.ReadFromJsonAsync<TableUsageDto>(Json))!;
        var again = (await (await client.PostAsync(url, null)).Content.ReadFromJsonAsync<TableUsageDto>(Json))!;
        var summary = (await client.GetFromJsonAsync<List<UsageTableSummaryDto>>("/api/vocabulary/usage", Json))!;

        Assert.Equal((6, 0), (before.Total, before.Ready));
        Assert.Equal((6, 6), (prepared.Total, prepared.Ready));
        Assert.Equal(1, _ai.Requests.Count(r => r.Feature == UsageService.Feature)); // nothing left to ask the second time
        Assert.Equal(6, again.Ready);
        var open = prepared.Usages.Single(u => u.Korean == "열다");
        Assert.Contains("사업을 시작하다", open.TrapUz);
        Assert.Single(open.Phrases); // the one without Hangul was dropped
        Assert.Equal(1, open.Questions); // the question without a gap was dropped
        Assert.NotNull(open.Warning);
        Assert.All(prepared.Usages.Where(u => u.Korean != "열다"), u => Assert.Null(u.TrapUz));
        Assert.Equal((6, 6, 6), (summary[0].Total, summary[0].Ready, summary[0].Questions));

        // The trap also shows on the card's answer side.
        var start = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true, 1), Json);
        var session = (await start.Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;
        Assert.Equal(session.Current!.Prompt == "열다" || session.Current.Answer == "열다", session.Current.UsageTrap is not null);
    }

    [Fact]
    public async Task A_word_the_ai_skipped_is_asked_again_and_a_failing_round_says_so()
    {
        _ai.Answer = r => Usage(r.UserContent, "먹다");
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);
        const string url = "/api/vocabulary/tables/1/usage";

        var first = (await (await client.PostAsync(url, null)).Content.ReadFromJsonAsync<TableUsageDto>(Json))!;
        _ai.Answer = _ => "not json";
        var broken = await client.PostAsync(url, null);
        _ai.Answer = r => Usage(r.UserContent);
        var second = (await (await client.PostAsync(url, null)).Content.ReadFromJsonAsync<TableUsageDto>(Json))!;
        var foreign = await (await factory.SignedInClientAsync(ApiFactory.UserB)).PostAsync(url, null);

        Assert.Equal(5, first.Ready);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, broken.StatusCode);
        Assert.Equal(6, second.Ready);
        Assert.Contains("1. 먹다", _ai.Requests[^1].UserContent); // only the missing word was sent
        Assert.DoesNotContain("열다", _ai.Requests[^1].UserContent);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Which_word_is_right_test_uses_the_prepared_questions_and_mistakes_are_noted()
    {
        _ai.Answer = r => Usage(r.UserContent);
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);

        var tooEarly = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [1], 10, Usage: true), Json);
        await client.PostAsync("/api/vocabulary/tables/1/usage", null);
        var calls = _ai.Requests.Count;
        var response = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [1], 10, Usage: true), Json);
        var test = (await response.Content.ReadFromJsonAsync<TestDto>(Json))!;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooEarly.StatusCode);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(calls, _ai.Requests.Count); // no AI call per test
        Assert.Equal(6, test.Questions.Count); // one per word; only 6 good questions exist
        Assert.All(test.Questions, q =>
        {
            Assert.Equal(TestQuestionKind.WordUsage, q.Kind);
            Assert.Equal("O‘zbekcha gap.", q.Translation); // the Uzbek meaning is shown from the start
            Assert.Equal(2, q.Options.Count); // the right word and one look-alike; the duplicate was dropped
            Assert.Null(q.Correct);
        });
        Assert.StartsWith("Qo‘llanish", test.Title);

        foreach (var q in test.Questions)
        {
            await client.PostAsJsonAsync($"/api/tests/{test.Id}/answers", new TestAnswerRequest(q.Index, "다른말"), Json);
        }

        var finished = (await (await client.PostAsync($"/api/tests/{test.Id}/finish", null)).Content.ReadFromJsonAsync<TestDto>(Json))!;
        var mistakes = (await client.GetFromJsonAsync<MistakesDto>("/api/mistakes", Json))!;
        var history = (await client.GetFromJsonAsync<List<TestSummaryDto>>("/api/tests?category=Vocabulary", Json))!;

        Assert.Equal(0, finished.Score);
        Assert.All(finished.Questions, q => Assert.Equal("Shu so‘z mos.", q.Explanation));
        Assert.Equal(6, mistakes.Words.Count);
        Assert.All(mistakes.Words, w => Assert.Contains("qo‘llanish", w.Sources));
        Assert.Contains(history, h => h.Id == test.Id);
    }
}
