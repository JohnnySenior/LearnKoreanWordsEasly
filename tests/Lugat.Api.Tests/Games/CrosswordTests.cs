using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Contracts;
using Lugat.Domain.Games;

namespace Lugat.Api.Tests.Games;

public class CrosswordTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = ApiFactory.Json;

    // Words that cross on their first syllables: 학교/학생 학, 학생/생일 생, 교실/학교 교, 생일/일요일 일, 실내/교실 실.
    private static readonly (string, string)[] Unit1 = [("학교", "maktab"), ("학생", "talaba"), ("생일", "tug‘ilgan kun"), ("교실", "sinf xonasi"), ("집", "uy")];
    private static readonly (string, string)[] Unit2 = [("일요일", "yakshanba"), ("실내", "ichkari"), ("내일", "ertaga")];

    private static readonly Dictionary<string, string> KoreanOf = Unit1.Concat(Unit2).ToDictionary(w => w.Item2, w => w.Item1);

    private static async Task<CrosswordDto> StartAsync(HttpClient client, CrosswordSize size = CrosswordSize.Small, IReadOnlyList<Guid>? units = null)
    {
        var response = await client.PostAsJsonAsync("/api/crosswords", new StartCrosswordRequest(Guid.NewGuid(), size, units), Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CrosswordDto>(Json))!;
    }

    [Fact]
    public async Task One_open_unit_keeps_the_crossword_locked_and_two_open_it_without_showing_the_answers()
    {
        await using var factory = new ApiFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await factory.AddWordsAsync(ApiFactory.UserA, 1, Unit1);

        var home = (await client.GetFromJsonAsync<CrosswordHomeDto>("/api/crosswords", Json))!;
        Assert.Equal(1, home.OpenUnits);
        Assert.All(home.Sizes, s => Assert.False(s.Available));
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.PostAsJsonAsync("/api/crosswords", new StartCrosswordRequest(Guid.NewGuid(), CrosswordSize.Small), Json)).StatusCode);

        await factory.AddWordsAsync(ApiFactory.UserA, 2, Unit2);
        home = (await client.GetFromJsonAsync<CrosswordHomeDto>("/api/crosswords", Json))!;
        Assert.True(home.Sizes.Single(s => s.Size == CrosswordSize.Small).Available);
        Assert.False(home.Sizes.Single(s => s.Size == CrosswordSize.Large).Available); // three units
        var crossword = await StartAsync(client);

        Assert.True(crossword.Clues.Count >= 4);
        Assert.DoesNotContain(crossword.Clues, c => c.Uzbek == "uy"); // one syllable never goes in
        Assert.All(crossword.Clues, c => Assert.Null(c.Korean));
        Assert.All(crossword.Cells, c => Assert.Null(c.Syllable));
        Assert.True(crossword.Cols <= 10);
        // It is kept: the page opens it again.
        Assert.Equal(crossword.Id, (await client.GetFromJsonAsync<CrosswordHomeDto>("/api/crosswords", Json))!.Active!.Id);
    }

    [Fact]
    public async Task Answers_hints_and_reveals_are_checked_on_the_server_and_a_reveal_reaches_the_mistakes_notebook()
    {
        await using var factory = new ApiFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await factory.AddWordsAsync(ApiFactory.UserA, 1, Unit1);
        await factory.AddWordsAsync(ApiFactory.UserA, 2, Unit2);
        var crossword = await StartAsync(client);
        var root = $"/api/crosswords/{crossword.Id}";

        var first = crossword.Clues[0];
        var wrong = await (await client.PostAsJsonAsync(root + "/answers", new CrosswordAnswerRequest(first.Index, "모르다"), Json))
            .Content.ReadFromJsonAsync<CrosswordAnswerResultDto>(Json);
        Assert.False(wrong!.Correct);

        var right = (await (await client.PostAsJsonAsync(root + "/answers", new CrosswordAnswerRequest(first.Index, " " + KoreanOf[first.Uzbek] + " "), Json))
            .Content.ReadFromJsonAsync<CrosswordAnswerResultDto>(Json))!;
        Assert.True(right.Correct);
        Assert.Equal(KoreanOf[first.Uzbek], right.Crossword.Clues[0].Korean);
        Assert.Contains(right.Crossword.Cells, c => c.Syllable is not null);

        // A syllable hint opens one hidden syllable; pressed again it opens the next, also the very last one.
        var other = right.Crossword.Clues.First(c => c.State == CrosswordWordState.Open);
        var hinted = (await (await client.PostAsJsonAsync(root + "/hints", new CrosswordHintRequest(other.Index, CrosswordHint.Syllable), Json))
            .Content.ReadFromJsonAsync<CrosswordDto>(Json))!;
        var clue = hinted.Clues.Single(c => c.Index == other.Index);
        Assert.True(clue.Pattern.Count(p => p is not null) > other.Pattern.Count(p => p is not null));
        Assert.Equal(1, clue.Hints);
        Assert.Null(clue.Korean);

        var revealed = (await (await client.PostAsJsonAsync(root + "/hints", new CrosswordHintRequest(other.Index, CrosswordHint.Reveal), Json))
            .Content.ReadFromJsonAsync<CrosswordDto>(Json))!;
        Assert.Equal(CrosswordWordState.Revealed, revealed.Clues.Single(c => c.Index == other.Index).State);
        Assert.Equal(1, revealed.Revealed);

        var mistakes = (await client.GetFromJsonAsync<MistakesDto>("/api/mistakes", Json))!;
        var word = Assert.Single(mistakes.Words, w => w.Korean == KoreanOf[other.Uzbek]);
        Assert.Contains("krossvord", word.Sources);

        // Everything solved: the crossword is finished and a new one can start.
        foreach (var c in revealed.Clues.Where(c => c.State == CrosswordWordState.Open))
        {
            await client.PostAsJsonAsync(root + "/answers", new CrosswordAnswerRequest(c.Index, KoreanOf[c.Uzbek]), Json);
        }

        var home = (await client.GetFromJsonAsync<CrosswordHomeDto>("/api/crosswords", Json))!;
        Assert.Null(home.Active);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(root + "/answers", new CrosswordAnswerRequest(0, "학교"), Json)).StatusCode);

        var stranger = await factory.SignedInClientAsync(ApiFactory.UserB);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync(root + "/hints", new CrosswordHintRequest(0, CrosswordHint.Reveal), Json)).StatusCode);
    }

    [Fact]
    public async Task The_learner_chooses_at_least_two_open_units_and_the_words_come_only_from_them()
    {
        await using var factory = new ApiFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await factory.AddWordsAsync(ApiFactory.UserA, 1, Unit1);
        await factory.AddWordsAsync(ApiFactory.UserA, 2, Unit2);

        var home = (await client.GetFromJsonAsync<CrosswordHomeDto>("/api/crosswords", Json))!;
        Assert.Equal(["A-1", "A-2"], home.Units!.Select(u => u.Unit));
        Assert.Equal(4, home.Units![0].Words); // 집 has one syllable

        async Task<HttpStatusCode> Try(CrosswordSize size, params Guid[] units) =>
            (await client.PostAsJsonAsync("/api/crosswords", new StartCrosswordRequest(Guid.NewGuid(), size, units), Json)).StatusCode;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await Try(CrosswordSize.Small, ApiFactory.Unit(1)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await Try(CrosswordSize.Small, ApiFactory.Unit(1), Guid.NewGuid()));

        var crossword = await StartAsync(client, CrosswordSize.Small, [ApiFactory.Unit(1), ApiFactory.Unit(2)]);
        Assert.All(crossword.Clues, c => Assert.Contains(c.Uzbek, KoreanOf.Keys));
    }

    [Fact]
    public void Words_of_the_last_crosswords_wait_while_fresh_ones_can_fill_the_grid()
    {
        // 생일, 일요일 and 내일 cross on 일; the rest were in a recent crossword.
        var fresh = new HashSet<string> { "생일", "일요일", "내일" };
        var pool = Unit1.Concat(Unit2).Where(w => w.Item1.Length > 1)
            .Select(w => new Lugat.Application.Games.CrosswordCandidate(Guid.NewGuid(), w.Item1, w.Item2, null, null, !fresh.Contains(w.Item1))).ToList();
        for (var seed = 0; seed < 10; seed++)
        {
            var (_, _, words) = Lugat.Application.Games.CrosswordGenerator.Build(pool, 3, new Random(seed));
            Assert.Equal(fresh, words.Select(w => w.Korean).ToHashSet());
        }
    }

    [Fact]
    public void The_generator_crosses_words_on_shared_syllables_and_keeps_the_grid_narrow()
    {
        var pool = Unit1.Concat(Unit2).Where(w => w.Item1.Length > 1)
            .Select(w => new Lugat.Application.Games.CrosswordCandidate(Guid.NewGuid(), w.Item1, w.Item2, null, null, false)).ToList();
        var (rows, cols, words) = Lugat.Application.Games.CrosswordGenerator.Build(pool, 8, new Random(7));

        Assert.True(words.Count >= 5);
        Assert.True(cols <= Lugat.Application.Games.CrosswordGenerator.MaxCols);
        var cells = new Dictionary<(int, int), string>();
        foreach (var w in words)
        {
            var syl = w.Syllables();
            for (var i = 0; i < syl.Length; i++)
            {
                var cell = w.Cell(i);
                Assert.InRange(cell.Row, 0, rows - 1);
                Assert.InRange(cell.Col, 0, cols - 1);
                // Two words meeting in a cell agree on its syllable.
                Assert.True(!cells.TryGetValue(cell, out var s) || s == syl[i]);
                cells[cell] = syl[i];
            }
        }
    }
}
