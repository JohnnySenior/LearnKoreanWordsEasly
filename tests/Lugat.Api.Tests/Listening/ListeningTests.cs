using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Api.Tests.Speech;
using Lugat.Application.Listening;
using Lugat.Application.Speech;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Infrastructure.Speech;

namespace Lugat.Api.Tests.Listening;

/// <summary>TOPIK II listening (D-53): AI-written passages, two voices, the text shown only after the answer.</summary>
public class ListeningTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FakeAiProvider _ai = new(AiProviderKind.OpenAi);

    private readonly FakeSpeech _speech = new();

    private ApiFactory NewFactory() => new() { AiProviders = [_ai], Speech = _speech };

    private static string Question(string first, string translation = "Ayol: Salom. Erkak: Salom.", int options = 4) => $$"""
        {"questionKo":"다음을 듣고 들은 내용과 같은 것을 고르십시오.",
         "lines":[{"speaker":"여","text":"{{first}}"},{"speaker":"남","text":"네, 내일 오후에 갈게요."}],
         "options":[{{string.Join(",", new[] { "\"남자는 내일 갑니다.\"", "\"남자는 오늘 갑니다.\"", "\"여자는 내일 갑니다.\"", "\"여자는 가지 않습니다.\"" }.Take(options))}}],
         "answer":1,"translationUz":"{{translation}}","explanationUz":"Erkak ‘내일’ dedi.",
         "words":[{"korean":"오후","uzbek":"tushdan keyin"},{"korean":"x","uzbek":"yaroqsiz"}]}
        """;

    private static string Set(params string[] questions) => $$"""{"questions":[{{string.Join(",", questions)}}]}""";

    [Fact]
    public async Task A_set_is_heard_before_it_is_read_and_the_first_answer_counts()
    {
        _ai.Answer = _ => Set(
            Question("내일 사무실에 오실 수 있어요?"),
            Question("회의가 몇 시에 시작해요?"),
            Question("Привет", translation: "Привет"), // no Hangul, Cyrillic: dropped
            Question("도서관이 몇 시에 열어요?", options: 3), // three options: dropped
            Question("주말에 뭐 하세요?"));
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var id = Guid.NewGuid();

        var created = await client.PostAsJsonAsync("/api/listening", new CreateListeningRequest(id), Json);
        var raw = await created.Content.ReadAsStringAsync();
        var set = JsonSerializer.Deserialize<ListeningDto>(raw, Json)!;
        var again = (await (await client.PostAsJsonAsync("/api/listening", new CreateListeningRequest(id), Json)).Content.ReadFromJsonAsync<ListeningDto>(Json))!;

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(set.Id, again.Id);
        Assert.Single(_ai.Requests);
        Assert.Equal(3, set.Items.Count);
        Assert.All(set.Items, i => Assert.Null(i.Reveal));
        Assert.DoesNotContain("사무실", raw); // the passage is only heard before answering
        Assert.Contains("<learner_words>\n(none yet)", _ai.Requests[0].UserContent.ReplaceLineEndings("\n"));

        var audio = await client.GetAsync($"/api/listening/{id}/items/0/audio");
        var cached = await client.GetAsync($"/api/listening/{id}/items/0/audio");
        var missing = await client.GetAsync($"/api/listening/{id}/items/9/audio");

        Assert.Equal("audio/mpeg", audio.Content.Headers.ContentType?.MediaType);
        Assert.Equal("ID3fake-dialogue"u8.ToArray(), await cached.Content.ReadAsByteArrayAsync());
        var dialogue = Assert.Single(_speech.Dialogues); // the second request came from the store
        Assert.Equal([(false, "내일 사무실에 오실 수 있어요?"), (true, "네, 내일 오후에 갈게요.")], dialogue.Select(l => (l.Male, l.Text)));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var right = set.Items[0].Options.ToList().IndexOf("남자는 내일 갑니다.");
        var wrong = (right + 1) % 4;
        var answered = (await (await client.PostAsJsonAsync($"/api/listening/{id}/answers", new ListeningAnswerRequest(0, wrong), Json)).Content.ReadFromJsonAsync<ListeningDto>(Json))!;
        var changed = (await (await client.PostAsJsonAsync($"/api/listening/{id}/answers", new ListeningAnswerRequest(0, right), Json)).Content.ReadFromJsonAsync<ListeningDto>(Json))!;
        var badChoice = await client.PostAsJsonAsync($"/api/listening/{id}/answers", new ListeningAnswerRequest(1, 4), Json);
        var foreign = await (await factory.SignedInClientAsync(ApiFactory.UserB)).GetAsync($"/api/listening/{id}/items/0/audio");

        var reveal = answered.Items[0].Reveal!;
        Assert.Equal((wrong, right, false), (reveal.Choice, reveal.Answer, reveal.IsCorrect));
        Assert.Equal("내일 사무실에 오실 수 있어요?", reveal.Lines[0].Text);
        Assert.Equal("오후", Assert.Single(reveal.Words).Korean);
        Assert.Equal((1, 0), (answered.Done, answered.Correct));
        Assert.False(changed.Items[0].Reveal!.IsCorrect); // the first answer counts
        Assert.Null(answered.Items[1].Reveal);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badChoice.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        var list = (await client.GetFromJsonAsync<List<ListeningSummaryDto>>("/api/listening", Json))!;
        Assert.Equal((3, 1, 0), (Assert.Single(list).Total, list[0].Done, list[0].Correct));

        // The wrong answer is in the mistakes notebook and the weak points (D-54).
        var mistakes = (await client.GetFromJsonAsync<MistakesDto>("/api/mistakes", Json))!;
        var weak = (await client.GetFromJsonAsync<WeakPointsDto>("/api/weak-points", Json))!;
        var listened = Assert.Single(mistakes.Listening!);
        Assert.Equal((id, 0, "남자는 내일 갑니다."), (listened.SetId, listened.Index, listened.RightOption));
        Assert.Equal((1, 1), (weak.ListeningWrong, weak.ListeningTotal));
    }

    [Fact]
    public async Task Too_few_good_questions_is_a_service_error_and_nothing_is_kept()
    {
        _ai.Answer = _ => Set(Question("내일 사무실에 오실 수 있어요?"));
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var response = await client.PostAsJsonAsync("/api/listening", new CreateListeningRequest(Guid.NewGuid()), Json);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ListeningSummaryDto>>("/api/listening", Json))!);
    }

    [Fact]
    public void The_right_option_follows_the_shuffle()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var item = Assert.Single(ListeningService.Parse(Set(Question("회의가 몇 시에 시작해요?")), new Random(seed)));
            Assert.Equal("남자는 내일 갑니다.", item.Options[item.Answer]);
        }
    }

    [Fact]
    public void Azure_reads_a_dialogue_with_two_voices_and_a_pause_between_turns()
    {
        var ssml = AzureSpeechSynthesizer.DialogueSsml([new SpeechLine(false, "안녕하세요 <b>"), new SpeechLine(true, "네.")], "ko-KR-SunHiNeural", "ko-KR-InJoonNeural");

        Assert.Equal(
            """<speak version="1.0" xml:lang="ko-KR"><voice name="ko-KR-SunHiNeural">안녕하세요 &lt;b&gt;</voice><voice name="ko-KR-InJoonNeural"><break time="700ms"/>네.</voice></speak>""",
            ssml);
    }
}
