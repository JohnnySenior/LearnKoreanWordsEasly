using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Learning;
using Lugat.Domain.Writing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Writing;

public class WritingEndpointsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FakeAiProvider _ai = new(AiProviderKind.OpenAi);

    // 가가 = "o‘jarlik", 가나 = "1. ishqalamoq; 2. surtmoq", 가다 = "tutun"
    private ApiFactory NewFactory(bool withAi = true) => new()
    {
        TableWords = [("가가", "o‘jarlik"), ("가나", "1. ishqalamoq; 2. surtmoq"), ("가다", "tutun"), ("가라", "ma’no 4"), ("가마", "ma’no 5")],
        AiProviders = withAi ? [_ai] : [],
    };

    private static async Task ApproveAsync(HttpClient client)
    {
        (await client.PostAsync(ApiFactory.TestWordsPath, null)).EnsureSuccessStatusCode();
    }

    /// <summary>Starts every word in flash cards so it is eligible for writing.</summary>
    private static async Task StartAllAsync(HttpClient client)
    {
        var session = (await (await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, false), Json)).Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;
        while (session.Current is not null)
        {
            var r = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/answers", new AnswerRequest(Guid.NewGuid(), session.Position, ReviewAnswer.Easy, false, 1000), Json);
            session = (await r.Content.ReadFromJsonAsync<AnswerResultDto>(Json))!.Session;
        }
    }

    private static async Task<WritingPromptDto> PromptForAsync(HttpClient client, ReviewDirection direction, string text)
    {
        var queue = (await client.GetFromJsonAsync<List<WritingPromptDto>>($"/api/writing/queue?direction={direction}&count=10", Json))!;
        return queue.Single(p => p.Prompt == text);
    }

    private static async Task<WritingResultDto> AnswerAsync(HttpClient client, WritingPromptDto prompt, string answer, Guid? id = null)
    {
        var response = await client.PostAsJsonAsync("/api/writing/answers", new WritingCheckRequest(id ?? Guid.NewGuid(), prompt.WordSenseId, prompt.Direction, answer), Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WritingResultDto>(Json))!;
    }

    [Fact]
    public async Task Typed_answers_are_graded_without_ai_when_the_text_matches()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);
        await StartAllAsync(client);

        var toUzbek = await PromptForAsync(client, ReviewDirection.KoreanToUzbek, "가나");
        var toKorean = await PromptForAsync(client, ReviewDirection.UzbekToKorean, "o‘jarlik");

        Assert.Equal(WritingVerdict.Correct, (await AnswerAsync(client, toUzbek, "Surtmoq")).Verdict);
        Assert.Equal(WritingVerdict.Correct, (await AnswerAsync(client, toKorean, "가가")).Verdict);
        var slip = await AnswerAsync(client, toKorean, "각가");
        Assert.Equal(WritingVerdict.Spelling, slip.Verdict);
        Assert.Equal("가가", slip.Expected);
        Assert.Equal(WritingVerdict.Wrong, (await AnswerAsync(client, toKorean, "—")).Verdict); // "Bilmayman"
        Assert.Equal(WritingVerdict.Wrong, (await AnswerAsync(client, toKorean, "gaga")).Verdict); // no Hangul
        Assert.Empty(_ai.Requests);
    }

    [Fact]
    public async Task A_different_text_with_the_same_meaning_is_judged_by_ai_as_data()
    {
        _ai.Answer = _ => """{"verdict":"same_meaning","explanationUz":"«Qaysarlik» ham shu ma’noni beradi.","confidence":0.9}""";
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);
        await StartAllAsync(client);
        var prompt = await PromptForAsync(client, ReviewDirection.KoreanToUzbek, "가가");

        var result = await AnswerAsync(client, prompt, "qaysarlik");

        Assert.Equal(WritingVerdict.SameMeaning, result.Verdict);
        Assert.True(result.AiChecked);
        Assert.Contains("Qaysarlik", result.Note);
        var request = Assert.Single(_ai.Requests);
        Assert.Contains("<learner_answer>\nqaysarlik\n</learner_answer>", request.UserContent.ReplaceLineEndings("\n"));
        Assert.DoesNotContain("qaysarlik", request.Instructions);
    }

    [Fact]
    public async Task Without_ai_a_mismatch_is_wrong_and_says_it_was_not_checked()
    {
        await using var factory = NewFactory(withAi: false);
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);
        await StartAllAsync(client);
        var prompt = await PromptForAsync(client, ReviewDirection.KoreanToUzbek, "가다");

        var result = await AnswerAsync(client, prompt, "osmon");

        Assert.Equal(WritingVerdict.Wrong, result.Verdict);
        Assert.False(result.AiChecked);
        Assert.Contains("AI tekshira olmadi", result.Note);
        Assert.Equal("tutun", result.Expected);
    }

    [Fact]
    public async Task Writing_never_touches_flash_card_schedules_and_is_counted_separately()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(client);
        await StartAllAsync(client);
        List<(Guid, DateTimeOffset?)> Snapshot(IServiceScope scope) =>
            scope.ServiceProvider.GetRequiredService<Lugat.Infrastructure.Persistence.AppDbContext>()
                .Cards.AsNoTracking().OrderBy(c => c.Id).Select(c => new ValueTuple<Guid, DateTimeOffset?>(c.Id, c.DueAt)).ToList();
        List<(Guid, DateTimeOffset?)> before;
        using (var scope = factory.Services.CreateScope())
        {
            before = Snapshot(scope);
        }

        var id = Guid.NewGuid();
        var prompt = await PromptForAsync(client, ReviewDirection.UzbekToKorean, "tutun");
        await AnswerAsync(client, prompt, "가다", id);
        await AnswerAsync(client, prompt, "가다", id); // retry of the same answer
        var stats = await client.GetFromJsonAsync<WritingStatsDto>("/api/writing/stats", Json);

        using (var scope = factory.Services.CreateScope())
        {
            Assert.Equal(before, Snapshot(scope));
        }

        Assert.Equal(new WritingDirectionStatsDto(1, 1, 0, 0, 0), stats!.TypingKorean);
        Assert.Equal(0, stats.TypingUzbek.Attempts);
    }

    [Fact]
    public async Task Other_users_words_and_answers_are_not_found()
    {
        await using var factory = NewFactory();
        var a = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveAsync(a);
        await StartAllAsync(a);
        var prompt = await PromptForAsync(a, ReviewDirection.KoreanToUzbek, "가가");
        var id = Guid.NewGuid();
        await AnswerAsync(a, prompt, "o‘jarlik", id);
        var b = await factory.SignedInClientAsync(ApiFactory.UserB);

        var foreignWord = await b.PostAsJsonAsync("/api/writing/answers", new WritingCheckRequest(Guid.NewGuid(), prompt.WordSenseId, prompt.Direction, "x"), Json);
        var foreignId = await b.PostAsJsonAsync("/api/writing/answers", new WritingCheckRequest(id, prompt.WordSenseId, prompt.Direction, "x"), Json);
        var emptyQueue = await b.GetAsync("/api/writing/queue");

        Assert.Equal(HttpStatusCode.NotFound, foreignWord.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignId.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, emptyQueue.StatusCode);
    }
}
