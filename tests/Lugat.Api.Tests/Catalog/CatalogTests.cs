using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Catalog;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Catalog;
using Lugat.Domain.Users;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Catalog;

/// <summary>Stage 2 (spec §13.3): import of the official list, units, AI drafts, checking and publishing.</summary>
public class CatalogTests
{
    private const long Editor = 3001;

    private readonly FakeAiProvider _ai = new(AiProviderKind.Anthropic);

    private ApiFactory NewFactory() => new() { AdminTelegramIds = [ApiFactory.UserA], AiProviders = [_ai] };

    private static async Task<CatalogImportResultDto> ImportAsync(HttpClient admin, byte[] file, string name)
    {
        var response = await admin.PostAsync("/api/admin/catalog/import", CatalogFiles.Upload(file, name));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CatalogImportResultDto>(ApiFactory.Json))!;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }

    private static async Task<List<UnitAdminSummaryDto>> UnitsAsync(HttpClient client, CatalogLevel level) =>
        (await client.GetFromJsonAsync<List<UnitAdminSummaryDto>>($"/api/admin/units?level={level}", ApiFactory.Json))!;

    private static async Task<UnitAdminDto> UnitAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<UnitAdminDto>($"/api/admin/units/{id}", ApiFactory.Json))!;

    /// <summary>Gives a signed-in user the Editor role, as the admin panel will (stage 7).</summary>
    private static async Task<HttpClient> EditorAsync(ApiFactory factory)
    {
        var client = await factory.SignedInClientAsync(Editor, "Muharrir");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var login = await db.UserLogins.SingleAsync(l => l.ProviderUserId == Editor.ToString());
        var user = await db.Users.SingleAsync(u => u.Id == login.UserId);
        user.Roles |= UserRoles.Editor;
        await db.SaveChangesAsync();
        return client;
    }

    /// <summary>A small level A list: 60 studyable words (units of 25, 25 and 10), plus 의/보/관 words.</summary>
    private static List<ListRow> SmallList() =>
    [
        .. Enumerable.Range(1, 60).Select(i => new ListRow(i * 2, $"{(char)('가' + i)}다{(i % 2 == 0 ? "01" : "")}", i % 2 == 0 ? "동" : "명", null, "A")),
        new(1, "것01", "의", null, "A"),
        new(3, "있다01", "보", null, "A"),
        new(5, "이01", "관", null, "A"),
    ];

    /// <summary>A valid draft for every "<id>. <word>" line, broken where the test asks.</summary>
    private static string Draft(string userContent, Func<int, string, string?>? broken = null)
    {
        var items = Regex.Matches(userContent, @"^(\d+)\. (\S+) ", RegexOptions.Multiline).Select(m =>
        {
            var (id, word) = (int.Parse(m.Groups[1].Value), m.Groups[2].Value);
            var how = broken?.Invoke(id, word);
            var translation = how == "cyrillic" ? "бормоқ" : $"ma’no {id}; ikkinchi ma'no";
            var example = how == "long" ? "저는 오늘 아침에 회사에 가서 친구를 만나고 밥을 먹었어요." : $"저는 {word} 해요.";
            var trap = how == "trap" ? "\"Бу тузоқ.\"" : id == 1 ? "\"O'zbekchadagi kabi ishlaydi.\"" : "null";
            return $$"""
                {"id":{{id}},"translationUz":"{{translation}}","romanization":"Gada","exampleKo":"{{example}}","exampleUz":"Men buni qilaman.",
                 "trapUz":{{trap}},"theme":"Asosiy fe'l",
                 "questions":[{"uzbek":"O‘zbekcha gap.","sentence":"저는 ___.","answer":"{{word}}","wrong":["다른말"],"explanationUz":"Shu so‘z mos."},
                              {"uzbek":"Bo‘shliqsiz.","sentence":"저는 {{word}}.","answer":"{{word}}","wrong":["다른말"],"explanationUz":"Buzuq."}]}
                """;
        });
        return $$"""{"words":[{{string.Join(",", items)}}]}""";
    }

    [Fact]
    public async Task Import_saves_every_row_once_and_importing_again_adds_nothing()
    {
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);
        var rows = CatalogFiles.OfficialSize();
        var file = CatalogFiles.Xlsx(rows.AsEnumerable().Reverse()); // file order must not matter

        var first = await ImportAsync(admin, file, "어휘목록.xlsx");
        var again = await ImportAsync(admin, file, "어휘목록.xlsx");

        Assert.Equal((5965, 5965, 0, 0, 0), (first.Rows, first.Created, first.Updated, first.Unchanged, first.Skipped));
        Assert.Equal((5965, 0, 0, 5965), (again.Rows, again.Created, again.Updated, again.Unchanged));
        Assert.Equal([982, 2111, 2872], first.Levels.Select(l => l.Words));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(5965, await db.CatalogWords.CountAsync());
        var gaDa = await db.CatalogWords.SingleAsync(w => w.FreqRank == 3);
        Assert.Equal(("01", CatalogWordStatus.Draft), (gaDa.Homonym, gaDa.Status)); // "…다01": the homonym number is split off
        Assert.False((await db.CatalogWords.SingleAsync(w => w.FreqRank == 1)).IsStudyable); // 의
    }

    [Fact]
    public async Task A_new_rank_in_a_later_file_updates_the_word_without_a_duplicate()
    {
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);
        var rows = SmallList();
        await ImportAsync(admin, CatalogFiles.Xlsx(rows), "a.xlsx");

        var changed = rows.Select((r, i) => i == 0 ? r with { Rank = 999, Gloss = "出" } : r).ToList();
        var result = await ImportAsync(admin, CatalogFiles.Cp949Text(changed), "a.txt");

        Assert.Equal((0, 1, rows.Count - 1), (result.Created, result.Updated, result.Unchanged));
    }

    [Fact]
    public async Task A_level_is_split_into_units_of_25_by_frequency_without_bound_words()
    {
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(admin, CatalogFiles.Xlsx(CatalogFiles.OfficialSize()), "list.xlsx");

        var level = await ReadAsync<CatalogLevelAdminDto>(await admin.PostAsync("/api/admin/catalog/levels/A/split", null));
        var units = await UnitsAsync(admin, CatalogLevel.A);
        var words = new List<CatalogWordAdminDto>();
        foreach (var unit in units)
        {
            words.AddRange((await UnitAsync(admin, unit.Id)).Words);
        }

        Assert.Equal(Enumerable.Range(1, units.Count), units.Select(u => u.Number));
        Assert.All(units.SkipLast(1), u => Assert.Equal(25, u.Words));
        Assert.InRange(units[^1].Words, 10, 34);
        Assert.Equal(level.Studyable, words.Count);
        Assert.Equal(0, level.Unplaced);
        Assert.Equal(words.Select(w => w.FreqRank).Order(), words.Select(w => w.FreqRank)); // most frequent first
        Assert.DoesNotContain(words, w => w.Pos is "보" or "의" or "관");
        Assert.True(level.Words > level.Studyable);
    }

    [Fact]
    public async Task Unpublished_units_are_not_visible_to_learners()
    {
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);
        var learner = await factory.SignedInClientAsync(ApiFactory.UserB);
        await ImportAsync(admin, CatalogFiles.Xlsx(SmallList()), "a.xlsx");
        await admin.PostAsync("/api/admin/catalog/levels/A/split", null);
        var draft = (await UnitsAsync(admin, CatalogLevel.A))[0];

        var levels = (await learner.GetFromJsonAsync<List<LevelDto>>("/api/levels", ApiFactory.Json))!;
        var units = (await learner.GetFromJsonAsync<LevelUnitsDto>("/api/levels/A/units", ApiFactory.Json))!;
        var detail = await learner.GetAsync($"/api/units/{draft.Id}");
        var adminSide = await learner.GetAsync($"/api/admin/units/{draft.Id}");

        Assert.Equal((0, 3), (levels[0].PublishedUnits, levels[0].ComingSoonUnits));
        Assert.Empty(units.Units);
        Assert.Equal(3, units.ComingSoon);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminSide.StatusCode);
    }

    [Fact]
    public async Task A_unit_is_published_only_when_every_word_is_checked_and_in_order()
    {
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);
        var editor = await EditorAsync(factory);
        var learner = await factory.SignedInClientAsync(ApiFactory.UserB);
        await ImportAsync(admin, CatalogFiles.Xlsx(SmallList()), "a.xlsx");
        await admin.PostAsync("/api/admin/catalog/levels/A/split", null);
        var units = await UnitsAsync(editor, CatalogLevel.A);
        var unit = await UnitAsync(editor, units[0].Id);

        var notChecked = await editor.PostAsync($"/api/admin/units/{unit.Unit.Id}/publish", null);
        var incomplete = await editor.PostAsJsonAsync($"/api/admin/catalog/words/{unit.Words[0].Id}/review", new ReviewCatalogWordRequest(unit.Words[0].Version));
        foreach (var word in unit.Words)
        {
            var saved = await ReadAsync<CatalogWordAdminDto>(await editor.PutAsJsonAsync($"/api/admin/catalog/words/{word.Id}",
                new UpdateCatalogWordRequest("bormoq; ketmoq", "gada", "학교에 가요.", "Maktabga boraman.", null, "Harakat", [], word.Version), ApiFactory.Json));
            await ReadAsync<CatalogWordAdminDto>(await editor.PostAsJsonAsync($"/api/admin/catalog/words/{word.Id}/review", new ReviewCatalogWordRequest(saved.Version)));
        }

        var third = await UnitAsync(editor, units[2].Id);
        foreach (var word in third.Words)
        {
            var saved = await ReadAsync<CatalogWordAdminDto>(await editor.PutAsJsonAsync($"/api/admin/catalog/words/{word.Id}",
                new UpdateCatalogWordRequest("kelmoq", "oda", "집에 와요.", "Uyga kelaman.", null, null, null, word.Version), ApiFactory.Json));
            await editor.PostAsJsonAsync($"/api/admin/catalog/words/{word.Id}/review", new ReviewCatalogWordRequest(saved.Version));
        }

        var outOfOrder = await editor.PostAsync($"/api/admin/units/{units[2].Id}/publish", null);
        var published = await ReadAsync<UnitAdminSummaryDto>(await editor.PostAsync($"/api/admin/units/{unit.Unit.Id}/publish", null));
        var visible = (await learner.GetFromJsonAsync<LevelUnitsDto>("/api/levels/A/units", ApiFactory.Json))!;
        var words = (await learner.GetFromJsonAsync<UnitDto>($"/api/units/{unit.Unit.Id}", ApiFactory.Json))!;
        var resplit = await ReadAsync<CatalogLevelAdminDto>(await admin.PostAsync("/api/admin/catalog/levels/A/split", null));
        var after = await UnitAsync(editor, unit.Unit.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, notChecked.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, incomplete.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, outOfOrder.StatusCode);
        Assert.Contains("1-bo‘limini", await outOfOrder.Content.ReadAsStringAsync());
        Assert.Equal(UnitStatus.Published, published.Status);
        Assert.Equal([1], visible.Units.Select(u => u.Number));
        Assert.Equal(2, visible.ComingSoon);
        Assert.Equal(25, words.Words.Count);
        Assert.Equal(("bormoq; ketmoq", "fe’l"), (words.Words[1].TranslationUz, words.Words[1].PosUz));
        Assert.All(words.Words, w => Assert.Contains("국립국어원", w.Source));
        Assert.Equal(3, resplit.Units); // splitting again keeps the published unit as it is
        Assert.Equal(unit.Words.Select(w => w.Id), after.Words.Select(w => w.Id));
        Assert.All(after.Words, w => Assert.Equal("Muharrir", w.ReviewedBy));
    }

    [Fact]
    public async Task Editing_checks_the_content_rules_and_concurrent_changes()
    {
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);
        var editor = await EditorAsync(factory);
        await ImportAsync(admin, CatalogFiles.Xlsx(SmallList()), "a.xlsx");
        await admin.PostAsync("/api/admin/catalog/levels/A/split", null);
        var word = (await UnitAsync(editor, (await UnitsAsync(editor, CatalogLevel.A))[0].Id)).Words[0];
        UpdateCatalogWordRequest Good(uint version) => new("bo'lmoq", "doeda", "의사가 돼요.", "Shifokor bo'laman.", "  ", null,
            [new("Men shifokor bo‘laman.", "저는 의사가 ___.", "돼요", ["해요"], "Bo‘lmoq — 되다.")], version);

        var cyrillic = await editor.PutAsJsonAsync($"/api/admin/catalog/words/{word.Id}", Good(word.Version) with { TranslationUz = "бўлмоқ" }, ApiFactory.Json);
        var tooLong = await editor.PutAsJsonAsync($"/api/admin/catalog/words/{word.Id}", Good(word.Version) with { ExampleKo = "가 나 다 라 마 바 사 아 자." }, ApiFactory.Json);
        var noGap = await editor.PutAsJsonAsync($"/api/admin/catalog/words/{word.Id}",
            Good(word.Version) with { Questions = [new("Gap.", "저는 의사가 돼요.", "돼요", ["해요"], "Izoh.")] }, ApiFactory.Json);
        var saved = await ReadAsync<CatalogWordAdminDto>(await editor.PutAsJsonAsync($"/api/admin/catalog/words/{word.Id}", Good(word.Version), ApiFactory.Json));
        var stale = await editor.PutAsJsonAsync($"/api/admin/catalog/words/{word.Id}", Good(word.Version), ApiFactory.Json);
        var importByEditor = await editor.PostAsync("/api/admin/catalog/import", CatalogFiles.Upload(CatalogFiles.Xlsx(SmallList()), "a.xlsx"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, cyrillic.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noGap.StatusCode);
        Assert.Equal(("bo‘lmoq", "Shifokor bo‘laman.", null), (saved.TranslationUz, saved.ExampleUz, saved.TrapUz)); // one apostrophe form, blank → none
        Assert.Equal(word.ContentVersion + 1, saved.ContentVersion);
        Assert.Single(saved.Questions);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, importByEditor.StatusCode); // importing the list is for the admin
    }

    [Fact]
    public async Task An_AI_draft_keeps_only_what_passes_the_checks()
    {
        _ai.Answer = r => Draft(r.UserContent, (id, _) => id switch { 2 => "cyrillic", 3 => "long", 4 => "trap", _ => null });
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);
        var editor = await EditorAsync(factory);
        await ImportAsync(admin, CatalogFiles.Xlsx(SmallList()), "a.xlsx");
        await admin.PostAsync("/api/admin/catalog/levels/A/split", null);
        var unitId = (await UnitsAsync(editor, CatalogLevel.A))[0].Id;

        var unit = await ReadAsync<UnitAdminDto>(await editor.PostAsync($"/api/admin/units/{unitId}/draft", null));

        var request = Assert.Single(_ai.Requests);
        Assert.Equal(CatalogDraftService.Feature, request.Feature);
        Assert.Equal(10, Regex.Matches(request.UserContent, @"^\d+\. ", RegexOptions.Multiline).Count);
        Assert.Contains("<known>", request.UserContent);
        var w = unit.Words;
        Assert.Equal(("ma’no 1; ikkinchi ma’no", "gada", "O‘zbekchadagi kabi ishlaydi."), (w[0].TranslationUz, w[0].Romanization, w[0].TrapUz));
        Assert.Null(w[1].TranslationUz); // Cyrillic translation: the word is not saved
        Assert.Null(w[2].TranslationUz); // example longer than 8 words
        Assert.Equal("ma’no 4; ikkinchi ma’no", w[3].TranslationUz);
        Assert.Null(w[3].TrapUz); // only the broken trap is dropped
        Assert.Single(w[0].Questions); // the question without a gap is dropped
        Assert.Equal("Claude (Anthropic)", w[0].AiProvider);
        Assert.All(w, x => Assert.Equal(CatalogWordStatus.Draft, x.Status)); // a person still has to check
        Assert.Equal(25 - 8, unit.Remaining);
        Assert.Equal(8, unit.Unit.Drafted);
    }

    [Fact]
    public async Task An_unusable_AI_answer_saves_nothing()
    {
        _ai.Answer = r => Draft(r.UserContent, (_, _) => "cyrillic");
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ImportAsync(admin, CatalogFiles.Xlsx(SmallList()), "a.xlsx");
        await admin.PostAsync("/api/admin/catalog/levels/A/split", null);
        var unitId = (await UnitsAsync(admin, CatalogLevel.A))[0].Id;

        var broken = await admin.PostAsync($"/api/admin/units/{unitId}/draft", null);
        _ai.Answer = _ => "not json";
        var notJson = await admin.PostAsync($"/api/admin/units/{unitId}/draft", null);
        var unit = await UnitAsync(admin, unitId);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, broken.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, notJson.StatusCode);
        Assert.All(unit.Words, w => Assert.Null(w.TranslationUz));
        Assert.Equal(25, unit.Remaining);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().CatalogUsages.CountAsync());
    }

    [Fact]
    public async Task Bad_files_are_refused_with_a_reason()
    {
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);

        var noHeader = await admin.PostAsync("/api/admin/catalog/import", CatalogFiles.Upload(CatalogFiles.Xlsx(SmallList(), ["No", "Word", "Pos", "Gloss", "Level"]), "a.xlsx"));
        var notExcel = await admin.PostAsync("/api/admin/catalog/import", CatalogFiles.Upload("garbage"u8.ToArray(), "a.xlsx"));
        var pdf = await admin.PostAsync("/api/admin/catalog/import", CatalogFiles.Upload([1, 2, 3], "a.pdf"));
        var someBad = await ImportAsync(admin, CatalogFiles.Xlsx([.. SmallList().Take(3), new(0, "가다", "동", null, "A"), new(7, "나다", "동", null, "D"), SmallList()[0]]), "b.xlsx");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, noHeader.StatusCode);
        Assert.Contains("순위", (await notExcel.Content.ReadAsStringAsync()) + (await noHeader.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notExcel.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, pdf.StatusCode);
        Assert.Equal((6, 3, 3), (someBad.Rows, someBad.Created, someBad.Skipped));
        Assert.Equal(3, someBad.Problems.Count);
    }

    [Fact]
    public async Task The_sample_seeds_two_published_units_once()
    {
        await using var factory = new ApiFactory { Settings = { ["Catalog:SeedSample"] = "true" } };
        var learner = await factory.SignedInClientAsync(ApiFactory.UserB);

        var units = (await learner.GetFromJsonAsync<LevelUnitsDto>("/api/levels/A/units", ApiFactory.Json))!;
        var first = (await learner.GetFromJsonAsync<UnitDto>($"/api/units/{units.Units[0].Id}", ApiFactory.Json))!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "content/samples/lugat_A_namuna_50.json"));
            await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync(CatalogSeeder.Read(file), CancellationToken.None);
        }

        await using var check = factory.Services.CreateAsyncScope();
        Assert.Equal([1, 2], units.Units.Select(u => u.Number));
        Assert.Equal([25, 25], units.Units.Select(u => u.WordCount));
        Assert.Equal(("하다", "qilmoq", "fe’l"), (first.Words[0].Word, first.Words[0].TranslationUz, first.Words[0].PosUz));
        Assert.Contains("o‘zbekchadagi", first.Words[0].TrapUz); // ASCII apostrophes of the file stored in one form
        Assert.Equal(50, await check.ServiceProvider.GetRequiredService<AppDbContext>().CatalogWords.CountAsync());
    }
}
