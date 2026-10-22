using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KoreanAiTeacher.Api.Tests.Ai;
using KoreanAiTeacher.Api.Tests.Grammar;
using KoreanAiTeacher.Api.Tests.Imports;
using KoreanAiTeacher.Api.Tests.Infrastructure;
using KoreanAiTeacher.Application.Ai;
using KoreanAiTeacher.Application.Testing;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Domain.Ai;
using KoreanAiTeacher.Domain.Testing;

namespace KoreanAiTeacher.Api.Tests.Testing;

/// <summary>Tests from the learner's TOPIK book (GR06, D-33): pages read in the background, answers from the book's key.</summary>
public class BookTestTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FakeAiProvider _ai = new(AiProviderKind.OpenAi);

    private readonly FakePdfLayoutReader _pdf = new FakePdfLayoutReader()
        .AddPage(FakePdfLayoutReader.Words("가", 5)).AddPage(FakePdfLayoutReader.Words("나", 5)).AddPage(FakePdfLayoutReader.Words("다", 5))
        .AddPage(FakePdfLayoutReader.Words("라", 5)).AddPage(FakePdfLayoutReader.Words("마", 5));

    /// <summary>Page 2: questions 1–2 (1 continues on page 3 as a duplicate), a listening item and a cut-off one.</summary>
    private const string Page2 = """
        {"questions":[
          {"number":1,"kind":"choice","instruction":"※ [1~2] (    )에 들어갈 가장 알맞은 것을 고르십시오.","passage":null,
           "stem":"비가 (    ) 우산을 가지고 가세요.","options":["오니까","오지만","오거나","오려고"],"figureUz":null,"aiAnswer":1,
           "hintUz":"Gapning ikkinchi qismi maslahat beradi: sabab kerak.","explanationUz":"-(으)니까 sababni bildiradi va buyruq bilan keladi.","complete":true,"confidence":0.95},
          {"number":2,"kind":"choice","instruction":null,"passage":"저는 주말마다 도서관에 갑니다. 거기에서 책을 읽고 공부도 합니다.",
           "stem":"이 글의 내용과 같은 것을 고르십시오.","options":["주말에 도서관에 간다.","도서관에서 일한다.","책을 산다.","친구를 만난다."],"figureUz":null,"aiAnswer":2,
           "hintUz":"Birinchi gapni diqqat bilan o‘qing.","explanationUz":"Matnda har dam olish kuni kutubxonaga borishi aytilgan.","complete":true,"confidence":0.9},
          {"number":3,"kind":"listening","instruction":null,"passage":null,"stem":"잘 듣고 알맞은 그림을 고르십시오.","options":["①","②","③","④"],
           "figureUz":null,"aiAnswer":null,"hintUz":null,"explanationUz":null,"complete":true,"confidence":0.9},
          {"number":4,"kind":"choice","instruction":null,"passage":"다음 글을","stem":"알맞은 것을","options":["가","나"],
           "figureUz":null,"aiAnswer":1,"hintUz":null,"explanationUz":null,"complete":false,"confidence":0.5}
        ]}
        """;

    private const string Page3 = """
        {"questions":[
          {"number":1,"kind":"choice","instruction":null,"passage":null,"stem":"다른 읽기","options":["가","나","다","라"],"figureUz":null,"aiAnswer":3,
           "hintUz":null,"explanationUz":null,"complete":true,"confidence":0.9},
          {"number":5,"kind":"choice","instruction":null,"passage":null,"stem":"밥을 (    ) 이를 닦아요.","options":["먹고 나서","먹으면서","먹는데","먹도록"],"figureUz":null,"aiAnswer":1,
           "hintUz":"Ketma-ketlik.","explanationUz":"-고 나서: avval biri, keyin ikkinchisi.","complete":true,"confidence":0.9}
        ]}
        """;

    /// <summary>The book's key: question 2 is ③, not the AI's ②.</summary>
    private const string Key = """{"answers":[{"number":1,"answer":1},{"number":2,"answer":3},{"number":5,"answer":1},{"number":9,"answer":7}]}""";

    private ApiFactory NewFactory() => new() { PdfReader = _pdf, AiProviders = [_ai] };

    private string Answer(AiJsonRequest r) => r.Feature switch
    {
        "book-test-key" => Key,
        _ when r.UserContent.Contains("page 2") => Page2,
        _ when r.UserContent.Contains("page 3") => Page3,
        _ => """{"questions":[]}""",
    };

    private static async Task<BookTestDto> CreateAsync(HttpClient client, Guid bookId, int? answerFrom = 5)
    {
        var response = await client.PostAsJsonAsync($"/api/book-tests/documents/{bookId}", new CreateBookTestRequest("1-mock · o‘qish", 2, 3, answerFrom, answerFrom), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookTestDto>(Json))!;
    }

    private static async Task<BookTestDto> WaitAsync(HttpClient client, Guid id, params BookTestStatus[] statuses)
    {
        for (var i = 0; i < 150; i++)
        {
            var test = (await client.GetFromJsonAsync<BookTestDto>($"/api/book-tests/{id}", Json))!;
            if (statuses.Contains(test.Status))
            {
                return test;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Book test {id} did not reach {string.Join('/', statuses)}");
    }

    private static async Task<TestDto> StartAsync(HttpClient client, Guid bookTestId, int count, bool exam)
    {
        var response = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), null, null, false, count, null, bookTestId, exam), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TestDto>(Json))!;
    }

    [Fact]
    public async Task Question_pages_then_the_key_are_read_and_only_complete_choice_questions_are_usable()
    {
        _ai.Answer = Answer;
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await GrammarImportTests.UploadBookAsync(client);

        var test = await WaitAsync(client, (await CreateAsync(client, book.Id)).Id, BookTestStatus.Ready, BookTestStatus.Failed);
        var questions = (await client.GetFromJsonAsync<List<BookQuestionDto>>($"/api/book-tests/{test.Id}/questions", Json))!;

        Assert.Equal(BookTestStatus.Ready, test.Status);
        Assert.Equal([(2, 2), (3, 3), (5, 5)], _pdf.Extracted);
        Assert.Equal(["book-test-page", "book-test-page", "book-test-key"], _ai.Requests.Select(r => r.Feature));
        Assert.Equal([1, 2, 3, 4, 5], questions.Select(q => q.Number));
        Assert.Equal("비가 (    ) 우산을 가지고 가세요.", questions[0].Stem); // the page-3 duplicate is ignored
        Assert.True(questions[2].Excluded); // listening: no audio
        Assert.Contains("tinglash", questions[2].Warning);
        Assert.True(questions[3].Excluded); // cut off, two options
        Assert.Equal((3, 2), (questions[1].OfficialAnswer, questions[1].AiAnswer));
        Assert.Null(questions[1].ExplanationUz); // the AI argued for ②, the key says ③
        Assert.Contains("kalitidan farq", questions[1].Warning);
        Assert.Equal("-(으)니까 sababni bildiradi va buyruq bilan keladi.", questions[0].ExplanationUz);
        Assert.Equal((5, 3, 3), (test.Questions, test.Usable, test.WithKey));
    }

    [Fact]
    public async Task An_exam_run_hides_answers_until_the_end_and_grades_by_the_book_key()
    {
        _ai.Answer = Answer;
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await GrammarImportTests.UploadBookAsync(client);
        var bookTest = await WaitAsync(client, (await CreateAsync(client, book.Id)).Id, BookTestStatus.Ready);

        var run = await StartAsync(client, bookTest.Id, 0, exam: true);

        Assert.Equal(5, run.TimeLimitMinutes); // 3 questions × 84 s, rounded up
        Assert.Equal(bookTest.Id, run.BookTestId);
        Assert.Equal([1, 2, 5], run.Questions.Select(q => q.Number));
        Assert.All(run.Questions, q => Assert.Equal(TestQuestionKind.BookChoice, q.Kind));
        Assert.Equal(["오니까", "오지만", "오거나", "오려고"], run.Questions[0].Options); // book order, never shuffled
        Assert.Equal("저는 주말마다 도서관에 갑니다. 거기에서 책을 읽고 공부도 합니다.", run.Questions[1].Sentence);
        Assert.All(run.Questions, q => Assert.Null(q.Correct));
        Assert.All(run.Questions, q => Assert.Null(q.Hint));

        await client.PostAsJsonAsync($"/api/tests/{run.Id}/answers", new TestAnswerRequest(0, "오니까"), Json);
        await client.PostAsJsonAsync($"/api/tests/{run.Id}/answers", new TestAnswerRequest(1, "도서관에서 일한다."), Json);
        var done = (await (await client.PostAsync($"/api/tests/{run.Id}/finish", null)).Content.ReadFromJsonAsync<TestDto>(Json))!;

        Assert.Equal(1, done.Score);
        Assert.Equal("책을 산다.", done.Questions[1].Correct); // ③ from the key
        Assert.Equal("Birinchi gapni diqqat bilan o‘qing.", done.Questions[1].Hint);
        Assert.Equal(true, done.Questions[1].OfficialKey);

        var retry = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), null, null, false, 0, done.Id), Json);
        var mistakes = (await retry.Content.ReadFromJsonAsync<TestDto>(Json))!;
        Assert.Equal([2, 5], mistakes.Questions.Select(q => q.Number));
        Assert.Equal(["주말에 도서관에 간다.", "도서관에서 일한다.", "책을 산다.", "친구를 만난다."], mistakes.Questions[0].Options);
        Assert.Null(mistakes.TimeLimitMinutes);
    }

    [Fact]
    public async Task Answers_are_refused_after_the_exam_time_is_up_but_the_run_can_still_be_finished()
    {
        _ai.Answer = Answer;
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await GrammarImportTests.UploadBookAsync(client);
        var bookTest = await WaitAsync(client, (await CreateAsync(client, book.Id)).Id, BookTestStatus.Ready);
        var run = await StartAsync(client, bookTest.Id, 0, exam: true);

        factory.Time.Advance(TimeSpan.FromMinutes(6));
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var late = await client.PostAsJsonAsync($"/api/tests/{run.Id}/answers", new TestAnswerRequest(0, "오니까"), Json);
        var finish = await client.PostAsync($"/api/tests/{run.Id}/finish", null);

        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Contains("Vaqt tugadi", await late.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, finish.StatusCode);
    }

    [Fact]
    public async Task Without_a_key_the_ai_answer_is_used_and_marked_as_such()
    {
        _ai.Answer = Answer;
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await GrammarImportTests.UploadBookAsync(client);
        var bookTest = await WaitAsync(client, (await CreateAsync(client, book.Id, answerFrom: null)).Id, BookTestStatus.Ready);

        var run = await StartAsync(client, bookTest.Id, 2, exam: false);
        var done = (await (await client.PostAsync($"/api/tests/{run.Id}/finish", null)).Content.ReadFromJsonAsync<TestDto>(Json))!;

        Assert.Null(run.TimeLimitMinutes);
        Assert.Equal(2, run.Questions.Count);
        Assert.True(run.Questions[0].Number < run.Questions[1].Number); // a random subset, in book order
        Assert.Contains("AI yechimidan", run.Warning);
        Assert.All(done.Questions, q => Assert.Equal(false, q.OfficialKey));
        Assert.DoesNotContain(_ai.Requests, r => r.Feature == "book-test-key");
    }

    [Fact]
    public async Task Excluding_a_question_takes_it_out_of_new_tests_and_other_users_cannot_see_the_book_test()
    {
        _ai.Answer = Answer;
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await GrammarImportTests.UploadBookAsync(client);
        var bookTest = await WaitAsync(client, (await CreateAsync(client, book.Id)).Id, BookTestStatus.Ready);
        var questions = (await client.GetFromJsonAsync<List<BookQuestionDto>>($"/api/book-tests/{bookTest.Id}/questions", Json))!;

        var first = questions[0];
        var excluded = await client.PatchAsJsonAsync($"/api/book-tests/questions/{first.Id}", new UpdateBookQuestionRequest(true, first.Version), Json);
        var stale = await client.PatchAsJsonAsync($"/api/book-tests/questions/{first.Id}", new UpdateBookQuestionRequest(false, first.Version), Json);
        var run = await StartAsync(client, bookTest.Id, 0, exam: false);
        var other = await factory.SignedInClientAsync(ApiFactory.UserB);

        Assert.Equal(HttpStatusCode.OK, excluded.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal([2, 5], run.Questions.Select(q => q.Number));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/book-tests/{bookTest.Id}")).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<BookTestDto>>("/api/book-tests", Json))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync("/api/tests",
            new CreateTestRequest(Guid.NewGuid(), null, null, false, 0, null, bookTest.Id, false), Json)).StatusCode);
    }

    [Fact]
    public async Task Page_ranges_are_checked_and_a_failed_read_can_be_retried()
    {
        _ai.Answer = r => r.UserContent.Contains("page 2") ? Page2 : throw new AiProviderException("ChatGPT: xizmat vaqtincha ishlamayapti (503)", transient: true);
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await GrammarImportTests.UploadBookAsync(client);

        var tooFar = await client.PostAsJsonAsync($"/api/book-tests/documents/{book.Id}", new CreateBookTestRequest("x", 2, 9, null, null), Json);
        var noTitle = await client.PostAsJsonAsync($"/api/book-tests/documents/{book.Id}", new CreateBookTestRequest(" ", 2, 3, null, null), Json);
        var failed = await WaitAsync(client, (await CreateAsync(client, book.Id, answerFrom: null)).Id, BookTestStatus.Failed);
        _ai.Answer = Answer;
        (await client.PostAsync($"/api/book-tests/{failed.Id}/retry", null)).EnsureSuccessStatusCode();
        var ready = await WaitAsync(client, failed.Id, BookTestStatus.Ready);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooFar.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noTitle.StatusCode);
        Assert.Contains("3-bet", failed.Error);
        Assert.Equal(2, failed.DonePage);
        Assert.Equal(3, ready.DonePage);
        Assert.Equal(5, ready.Questions);
    }

    [Fact]
    public async Task History_scores_runs_out_of_100_and_ranks_question_types_weakest_first()
    {
        _ai.Answer = Answer;
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await GrammarImportTests.UploadBookAsync(client);
        var bookTest = await WaitAsync(client, (await CreateAsync(client, book.Id)).Id, BookTestStatus.Ready);
        var empty = (await client.GetFromJsonAsync<BookHistoryDto>("/api/book-tests/history", Json))!;
        var run = await StartAsync(client, bookTest.Id, 0, exam: true);
        await client.PostAsJsonAsync($"/api/tests/{run.Id}/answers", new TestAnswerRequest(0, "오니까"), Json); // 1: right
        await client.PostAsJsonAsync($"/api/tests/{run.Id}/answers", new TestAnswerRequest(1, "도서관에서 일한다."), Json); // 2: wrong
        await client.PostAsync($"/api/tests/{run.Id}/finish", null); // 5: unanswered

        var history = (await client.GetFromJsonAsync<BookHistoryDto>("/api/book-tests/history", Json))!;

        Assert.Empty(empty.Runs);
        var only = Assert.Single(history.Runs);
        Assert.Equal((33, 1, 3, true, 5), (only.Score, only.Correct, only.Total, only.Exam, only.TimeLimitMinutes));
        Assert.Equal(3, history.Answered);
        // 1 is a gap question ([1~2]); 2's own question asks about the content; 5 has no instruction of its own.
        Assert.Equal(
            [(BookCategory.Content, 0, 1), (BookCategory.Other, 0, 1), (BookCategory.Blank, 100, 1)],
            history.Categories.Select(c => (c.Category, c.Percent, c.Total)));
        Assert.Equal("Mazmunga mos gap", history.Categories[0].Label);
        Assert.All(history.Categories, c => Assert.Equal(1, c.Available));
    }

    [Fact]
    public async Task Practice_on_one_question_type_takes_questions_from_all_book_tests_without_a_timer()
    {
        _ai.Answer = Answer;
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await GrammarImportTests.UploadBookAsync(client);
        await WaitAsync(client, (await CreateAsync(client, book.Id)).Id, BookTestStatus.Ready);

        var response = await client.PostAsJsonAsync("/api/tests",
            new CreateTestRequest(Guid.NewGuid(), null, null, false, 10, null, null, false, BookCategory.Blank), Json);
        var practice = (await response.Content.ReadFromJsonAsync<TestDto>(Json))!;
        var none = await client.PostAsJsonAsync("/api/tests",
            new CreateTestRequest(Guid.NewGuid(), null, null, false, 10, null, null, false, BookCategory.Headline), Json);
        var zero = await client.PostAsJsonAsync("/api/tests",
            new CreateTestRequest(Guid.NewGuid(), null, null, false, 0, null, null, false, BookCategory.Blank), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Kitob · Bo‘sh joyni to‘ldirish", practice.Title);
        Assert.Equal([1], practice.Questions.Select(q => q.Number));
        Assert.Equal(["오니까", "오지만", "오거나", "오려고"], practice.Questions[0].Options);
        Assert.Null(practice.TimeLimitMinutes);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, none.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, zero.StatusCode);
    }

    [Fact]
    public void The_key_parser_drops_numbers_outside_the_four_options()
    {
        var key = BookTestService.ParseKey(Key);

        Assert.Equal(new Dictionary<int, int> { [1] = 1, [2] = 3, [5] = 1 }, key);
        Assert.Throws<Application.ServiceUnavailableException>(() => BookTestService.ParseKey("not json"));
    }
}
