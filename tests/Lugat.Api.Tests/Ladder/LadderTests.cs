using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Ladder;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Ladder;
using Lugat.Domain.Learning;

namespace Lugat.Api.Tests.Ladder;

/// <summary>"Gap zinapoyasi" (D-47): fill, order and translate; word-for-word mistakes go to "Tuzoqlar daftari".</summary>
public class LadderTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FakeAiProvider _ai = new(AiProviderKind.OpenAi);

    private ApiFactory NewFactory() => new()
    {
        TableWords = [("친구", "do‘st"), ("만나다", "uchrashmoq"), ("사업", "biznes"), ("커피", "qahva"), ("학교", "maktab"), ("가다", "bormoq")],
        AiProviders = [_ai],
    };

    /// <summary>Imports the six words and answers each card once, so they count as started.</summary>
    private static async Task StartWordsAsync(HttpClient client)
    {
        (await client.PostAsync(ApiFactory.TestWordsPath, null)).EnsureSuccessStatusCode();
        var session = (await (await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true), Json)).Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;
        while (session.Current is { } card)
        {
            var result = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/answers", new AnswerRequest(Guid.NewGuid(), session.Position, ReviewAnswer.Easy, false, 2000), Json);
            session = (await result.Content.ReadFromJsonAsync<AnswerResultDto>(Json))!.Session;
        }
    }

    /// <summary>Four good sentences and one whose gap does not give back the sentence (must be dropped).</summary>
    private const string Built = """
        {"sentences":[
          {"wordId":1,"korean":"어제 친구를 만났어요.","uzbek":"Kecha do‘stimni ko‘rdim.",
           "fill":{"sentenceWithGap":"어제 친구___ 만났어요.","answer":"를","wrong":["가","에"],"kind":"particle","explanationUz":"Kimnidir ko‘rmoq — 을/를."},
           "twinUzbek":"Bugun akamni ko‘rdim.","twinKorean":"오늘 형을 만났어요."},
          {"wordId":3,"korean":"저는 작년에 사업을 시작했어요.","uzbek":"Men o‘tgan yili biznes ochdim.",
           "fill":{"sentenceWithGap":"저는 작년___ 사업을 시작했어요.","answer":"에","wrong":["에서","를"],"kind":"particle","explanationUz":"Vaqt — 에."},
           "twinUzbek":"Opam bu yil biznes ochdi.","twinKorean":"누나는 올해 사업을 시작했어요."},
          {"wordId":4,"korean":"카페에서 커피를 마시고 싶어요.","uzbek":"Kafeda qahva ichmoqchiman.",
           "fill":{"sentenceWithGap":"카페에서 커피를 마시___.","answer":"고 싶어요","wrong":["아서 싶어요","지만 싶어요"],"kind":"ending","explanationUz":"Istak — -고 싶다."},
           "twinUzbek":"Uyda choy ichmoqchiman.","twinKorean":"집에서 차를 마시고 싶어요."},
          {"wordId":6,"korean":"내일 학교에 가요.","uzbek":"Ertaga maktabga boraman.",
           "fill":{"sentenceWithGap":"내일 학교___ 가요.","answer":"에","wrong":["에서","을"],"kind":"particle","explanationUz":"Yo‘nalish — 에."},
           "twinUzbek":"Ertaga ishxonaga boraman.","twinKorean":"내일 회사에 가요."},
          {"wordId":2,"korean":"친구를 만나요.","uzbek":"Do‘stni ko‘raman.",
           "fill":{"sentenceWithGap":"친구___ 봐요.","answer":"를","wrong":["가"],"kind":"particle","explanationUz":"Buzuq."},
           "twinUzbek":"Akani ko‘raman.","twinKorean":"형을 만나요."}
        ]}
        """;

    private const string WrongWithTrap = """
        {"verdict":"incorrect","errorTypes":["word_choice","particle"],"correctedText":"누나는 올해 사업을 시작했어요.",
         "explanationUz":"Biznes ochmoq — 사업을 시작하다.","trap":{"wrong":"열었어요","right":"시작했어요","explanationUz":"열다 eshik ochish uchun."}}
        """;

    private const string Right = """
        {"verdict":"correct","errorTypes":[],"correctedText":"오늘 형을 만났어요.","explanationUz":"To‘g‘ri.","trap":null}
        """;

    private static async Task<LadderAnswerResultDto> AnswerAsync(HttpClient client, LadderDto ladder, string answer)
    {
        var task = ladder.Current!;
        var response = await client.PostAsJsonAsync($"/api/ladders/{ladder.Id}/answers", new LadderAnswerRequest(task.Index, task.Step, answer), Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LadderAnswerResultDto>(Json))!;
    }

    [Fact]
    public async Task A_ladder_goes_fill_order_translate_and_word_for_word_mistakes_become_trap_notes()
    {
        _ai.Answer = r => r.Feature == LadderService.BuildFeature ? Built : r.UserContent.Contains("열었어요") ? WrongWithTrap : Right;
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await StartWordsAsync(client);

        var id = Guid.NewGuid();
        var created = await client.PostAsJsonAsync("/api/ladders", new CreateLadderRequest(id), Json);
        var ladder = (await created.Content.ReadFromJsonAsync<LadderDto>(Json))!;
        var again = (await (await client.PostAsJsonAsync("/api/ladders", new CreateLadderRequest(id), Json)).Content.ReadFromJsonAsync<LadderDto>(Json))!;

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(ladder.Id, again.Id);
        Assert.Equal(1, _ai.Requests.Count(r => r.Feature == LadderService.BuildFeature));
        Assert.Equal(12, ladder.Total); // 4 good sentences × 3 rungs; the broken one was dropped
        Assert.Contains("친구", _ai.Requests[0].UserContent);

        // Rung 1: fill. The first sentence wrong, the rest right; an option that is not offered is refused.
        var fill = ladder.Current!;
        Assert.Equal(LadderStep.Fill, fill.Step);
        Assert.Equal("어제 친구___ 만났어요.", fill.SentenceWithGap);
        Assert.Equal(3, fill.Options.Count);
        Assert.Equal("qo‘shimcha", fill.FocusKind);
        var bad = await client.PostAsJsonAsync($"/api/ladders/{id}/answers", new LadderAnswerRequest(fill.Index, LadderStep.Fill, "은"), Json);
        var outOfTurn = await client.PostAsJsonAsync($"/api/ladders/{id}/answers", new LadderAnswerRequest(1, LadderStep.Translate, "형을"), Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, outOfTurn.StatusCode);
        var first = await AnswerAsync(client, ladder, "가");
        Assert.False(first.Feedback.IsCorrect);
        Assert.Equal("어제 친구를 만났어요.", first.Feedback.Correct);
        ladder = first.Ladder;
        while (ladder.Current is { Step: LadderStep.Fill } f)
        {
            ladder = (await AnswerAsync(client, ladder, f.Index switch { 1 => "에", 2 => "고 싶어요", _ => "에" })).Ladder;
        }

        // Rung 2: order. The chunks are the sentence's word groups, shuffled.
        var order = ladder.Current!;
        Assert.Equal(LadderStep.Order, order.Step);
        Assert.Equal(["만났어요.", "어제", "친구를"], order.Chunks.Order());
        Assert.NotEqual(["어제", "친구를", "만났어요."], order.Chunks);
        var korean = new[] { "어제 친구를 만났어요.", "저는 작년에 사업을 시작했어요.", "카페에서 커피를 마시고 싶어요.", "내일 학교에 가요." };
        while (ladder.Current is { Step: LadderStep.Order } o)
        {
            ladder = (await AnswerAsync(client, ladder, korean[o.Index])).Ladder;
        }

        // Rung 3: translate a twin sentence; the AI checks it.
        Assert.Equal("Bugun akamni ko‘rdim.", ladder.Current!.Uzbek);
        var right = await AnswerAsync(client, ladder, "오늘 형을 만났어요.");
        var trap = await AnswerAsync(client, right.Ladder, "누나가 올해 사업을 열었어요.");
        Assert.True(right.Feedback.IsCorrect);
        Assert.False(trap.Feedback.IsCorrect);
        Assert.Equal(["so‘z tanlash", "qo‘shimcha"], trap.Feedback.ErrorTypes);
        Assert.Equal("누나는 올해 사업을 시작했어요.", trap.Feedback.Corrected);
        Assert.Equal(("열었어요", "시작했어요"), (trap.Feedback.Trap!.Wrong, trap.Feedback.Trap.Right));

        // A retry of an answered task returns the same result without another check.
        var checks = _ai.Requests.Count(r => r.Feature == LadderService.CheckFeature);
        var retry = await client.PostAsJsonAsync($"/api/ladders/{id}/answers", new LadderAnswerRequest(1, LadderStep.Translate, "다른 답"), Json);
        Assert.False((await retry.Content.ReadFromJsonAsync<LadderAnswerResultDto>(Json))!.Feedback.IsCorrect);
        Assert.Equal(checks, _ai.Requests.Count(r => r.Feature == LadderService.CheckFeature));

        ladder = trap.Ladder;
        while (ladder.Current is not null)
        {
            ladder = (await AnswerAsync(client, ladder, "오늘 형을 만났어요.")).Ladder;
        }

        var traps = (await client.GetFromJsonAsync<List<TrapNoteDto>>("/api/traps", Json))!;
        var mistakes = (await client.GetFromJsonAsync<MistakesDto>("/api/mistakes", Json))!;
        var list = (await client.GetFromJsonAsync<List<LadderSummaryDto>>("/api/ladders", Json))!;
        var learned = await client.DeleteAsync($"/api/traps/{traps[0].Id}");
        var foreign = await (await factory.SignedInClientAsync(ApiFactory.UserB)).GetAsync($"/api/ladders/{id}");

        Assert.True(ladder.Finished);
        Assert.Equal([3, 4, 3], ladder.Scores.Select(s => s.Correct)); // fill: 1 wrong; translate: the trap one wrong
        Assert.Equal("Opam bu yil biznes ochdi.", Assert.Single(traps).Uzbek);
        Assert.Contains(mistakes.Words, w => w.Sources.Contains("zinapoya")); // the word of the wrong translation (words are sent shuffled)
        Assert.True(Assert.Single(list).Finished);
        Assert.Equal(HttpStatusCode.NoContent, learned.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<TrapNoteDto>>("/api/traps", Json))!);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Without_started_words_or_with_a_broken_ai_answer_nothing_is_saved()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var noWords = await client.PostAsJsonAsync("/api/ladders", new CreateLadderRequest(Guid.NewGuid()), Json);
        await StartWordsAsync(client);
        _ai.Answer = _ => "not json";
        var broken = await client.PostAsJsonAsync("/api/ladders", new CreateLadderRequest(Guid.NewGuid()), Json);

        _ai.Answer = r => r.Feature == LadderService.BuildFeature ? Built : "not json";
        var ladder = (await (await client.PostAsJsonAsync("/api/ladders", new CreateLadderRequest(Guid.NewGuid()), Json)).Content.ReadFromJsonAsync<LadderDto>(Json))!;
        while (ladder.Current is { Step: not LadderStep.Translate } task)
        {
            var answer = task.Step == LadderStep.Fill ? task.Options[0] : string.Join(' ', task.Chunks);
            ladder = (await AnswerAsync(client, ladder, answer)).Ladder;
        }

        var failedCheck = await client.PostAsJsonAsync($"/api/ladders/{ladder.Id}/answers", new LadderAnswerRequest(ladder.Current!.Index, LadderStep.Translate, "오늘 형을 만났어요."), Json);
        var after = (await client.GetFromJsonAsync<LadderDto>($"/api/ladders/{ladder.Id}", Json))!;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, noWords.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, broken.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedCheck.StatusCode);
        Assert.Equal(8, after.Done); // the translation was not saved, so it can be sent again
        Assert.Single((await client.GetFromJsonAsync<List<LadderSummaryDto>>("/api/ladders", Json))!);
    }

    /// <summary>D-54: mistakes from every exercise folded into weak points, and a ladder built from them.</summary>
    [Fact]
    public async Task Weak_points_gather_missed_forms_error_kinds_and_traps_and_feed_a_focused_ladder()
    {
        const string checkWithIssues = """
            {"verdict":"incorrect","errorType":"grammar","issues":[
              {"quote":"학교에서 가요","type":"grammar","severity":"error","hintUz":"Yo‘nalish qo‘shimchasiga qarang."},
              {"quote":"내일","type":"style","severity":"suggestion","hintUz":"Ixtiyoriy."}],
             "hintUz":"1 ta xato bor.","explanationUz":"Izoh.","correctedText":"내일 학교에 가요.","alternatives":[],
             "targetUsage":"used_correctly","confidence":0.9,"sourceRefs":[]}
            """;
        _ai.Answer = r => r.Feature == LadderService.BuildFeature ? Built
            : r.Feature == "sentence-check" ? checkWithIssues
            : r.UserContent.Contains("열었어요") ? WrongWithTrap : Right;
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await StartWordsAsync(client);

        var none = (await client.GetFromJsonAsync<WeakPointsDto>("/api/weak-points", Json))!;
        var noFocus = await client.PostAsJsonAsync("/api/ladders", new CreateLadderRequest(Guid.NewGuid(), WeakPoints: true), Json);
        Assert.Equal((7, 0, 0, 0), (none.Days, none.Forms.Count, none.ErrorTypes.Count, none.Traps.Count));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noFocus.StatusCode);

        // One ladder: the first gap missed, one translation translated word for word.
        var ladder = (await (await client.PostAsJsonAsync("/api/ladders", new CreateLadderRequest(Guid.NewGuid()), Json)).Content.ReadFromJsonAsync<LadderDto>(Json))!;
        var korean = new[] { "어제 친구를 만났어요.", "저는 작년에 사업을 시작했어요.", "카페에서 커피를 마시고 싶어요.", "내일 학교에 가요." };
        var twins = new[] { "오늘 형을 만났어요.", "누나는 올해 사업을 열었어요.", "집에서 차를 마시고 싶어요.", "내일 회사에 가요." };
        while (ladder.Current is { } task)
        {
            var answer = task.Step switch
            {
                LadderStep.Fill => task.Index switch { 0 => "가", 2 => "고 싶어요", _ => "에" },
                LadderStep.Order => korean[task.Index],
                _ => twins[task.Index],
            };
            ladder = (await AnswerAsync(client, ladder, answer)).Ladder;
        }

        // One sentence of the learner's own with a real error and an optional suggestion.
        var target = (await client.GetFromJsonAsync<SentenceTargetDto>("/api/practice/target", Json))!;
        (await client.PostAsJsonAsync("/api/practice/check", new SentenceCheckRequest(Guid.NewGuid(), target.WordSenseId, "내일 학교에서 가요."), Json)).EnsureSuccessStatusCode();

        var weak = (await client.GetFromJsonAsync<WeakPointsDto>("/api/weak-points?days=7", Json))!;
        var badWindow = await client.GetAsync("/api/weak-points?days=5");

        var form = Assert.Single(weak.Forms); // 에 was right twice, -고 싶어요 once: not weak
        Assert.Equal(("를", "qo‘shimcha", 1, 1, "가"), (form.Form, form.Kind, form.Mistakes, form.Total, form.Given));
        Assert.Equal("어제 친구___ 만났어요.", form.Example);
        Assert.Equal(["grammatika", "qo‘shimcha", "so‘z tanlash"], weak.ErrorTypes.Select(e => e.Label).Order());
        Assert.Equal("학교에서 가요", weak.ErrorTypes.Single(e => e.Label == "grammatika").ExampleQuote); // the suggestion is not counted
        Assert.Equal(("열었어요", "시작했어요"), (Assert.Single(weak.Traps).Wrong, weak.Traps[0].Right));
        var weakWord = Assert.Single(weak.Words).Korean; // the word of the wrong translation (word order is random)
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badWindow.StatusCode);

        // The focused ladder: the weak word first, the weak points in the prompt, a title that says so.
        _ai.Requests.Clear();
        var focused = (await (await client.PostAsJsonAsync("/api/ladders", new CreateLadderRequest(Guid.NewGuid(), WeakPoints: true), Json)).Content.ReadFromJsonAsync<LadderDto>(Json))!;
        var prompt = Assert.Single(_ai.Requests).UserContent.ReplaceLineEndings("\n");

        Assert.StartsWith("Zaif joylar · ", focused.Title);
        Assert.Contains($"<words>\n1. {weakWord} — ", prompt);
        Assert.Contains("- 를 (qo‘shimcha): 1 of 1, chose 가 in: 어제 친구___ 만났어요.", prompt);
        Assert.Contains("- so‘z tanlash: 1", prompt);
        Assert.Contains("- 열었어요 → 시작했어요", prompt);
        Assert.Contains("<weak_points>", _ai.Requests[0].Instructions);
    }
}
