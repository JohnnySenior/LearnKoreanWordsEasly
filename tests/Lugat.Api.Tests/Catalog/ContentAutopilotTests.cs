using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Ai;
using Lugat.Application.Catalog;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Catalog;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Catalog;

/// <summary>L-87: the AI prepares and publishes a level; the server's rules decide what it may confirm.</summary>
public class ContentAutopilotTests
{
    private const string GoTrap = "Yo‘nalish 에 bilan: 회사에 가요 = ishga boraman. 회사에서 가요 deyish xato.";

    /// <summary>40 studyable level B words: units of 25 and 15.</summary>
    private static List<ListRow> Level() => [.. Enumerable.Range(1, 40).Select(i => new ListRow(i * 2, $"{(char)('가' + i)}다", "동", null, "B"))];

    /// <summary>The fake AI: a card for every word (the first with a trap), a scene for the trap, the spelling as the reading.</summary>
    internal static string Answer(AiJsonRequest r, string? oddReading)
    {
        switch (r.SchemaName)
        {
            case "catalog_draft":
                var items = Regex.Matches(r.UserContent, @"^(\d+)\. (\S+) ", RegexOptions.Multiline).Select(m =>
                {
                    var (id, word) = (int.Parse(m.Groups[1].Value), m.Groups[2].Value);
                    var trap = word == "각다" ? JsonSerializer.Serialize(GoTrap) : "null";
                    return $$"""{"id":{{id}},"translationUz":"ma’no {{id}}","romanization":"gada","exampleKo":"저는 {{word}} 해요.","exampleUz":"Men buni qilaman.","trapUz":{{trap}},"theme":"Asosiy fe'l","questions":[]}""";
                });
                return $$"""{"words":[{{string.Join(",", items)}}]}""";
            case "trap_scenes":
                return """{"words":[{"id":1,"cue":"Ishga ketayotib:","wrong":"회사에서 가요","right":"회사에 가요","why":"Borish joyi uchun 에."}]}""";
            default:
                using (var data = JsonDocument.Parse(r.UserContent))
                {
                    return JsonSerializer.Serialize(new
                    {
                        words = data.RootElement.EnumerateArray().Select(w =>
                        {
                            var korean = w.GetProperty("korean").GetString();
                            return new { id = w.GetProperty("id").GetGuid(), korean, reading = korean == "갇다" ? oddReading : korean, explanationUz = (string?)null };
                        }).ToArray(),
                    });
                }
        }
    }

    private static async Task<Guid> UserIdAsync(ApiFactory f, long telegramId)
    {
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.UserLogins.SingleAsync(l => l.ProviderUserId == telegramId.ToString())).UserId;
    }

    [Fact]
    public async Task The_AI_splits_prepares_confirms_and_publishes_the_level_in_order_and_leaves_an_unexplained_reading_to_the_editor()
    {
        var ai = new FakeAiProvider(AiProviderKind.Anthropic);
        ai.Answer = r => Answer(r, oddReading: "갇따따");
        await using var f = new ApiFactory { AdminTelegramIds = [ApiFactory.UserA], AiProviders = [ai] };
        var admin = await f.SignedInClientAsync(ApiFactory.UserA);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/api/admin/catalog/import", CatalogFiles.Upload(CatalogFiles.Xlsx(Level()), "b.xlsx"))).StatusCode);
        var userId = await UserIdAsync(f, ApiFactory.UserA);

        var steps = new List<AutopilotStep>();
        for (var i = 0; i < 40 && steps.LastOrDefault() is not { Finished: true }; i++)
        {
            await using var scope = f.Services.CreateAsyncScope();
            steps.Add(await scope.ServiceProvider.GetRequiredService<ContentAutopilotService>().StepAsync(userId, CatalogLevel.B, CancellationToken.None));
        }

        var last = steps.Last();
        Assert.True(last.Finished);
        Assert.False(last.Stopped);
        Assert.Equal("B daraja 2 ta bo‘limga bo‘lindi.", steps[0].Message);
        Assert.Equal([1, 2], steps.Select(s => s.PublishedUnit).OfType<int>());
        Assert.Contains(steps, s => s.Message == "1-bo‘lim nashr qilindi. 1 ta so‘zning talaffuzi sizning tekshiruvingizni kutadi.");
        Assert.All(ai.Requests, r => Assert.True(r.Content)); // all of it goes to the content model
        Assert.DoesNotContain(ai.Requests, r => r.Feature == IntroductionService.Feature); // no introduction lesson

        var units = (await admin.GetFromJsonAsync<List<UnitAdminSummaryDto>>("/api/admin/units?level=B", ApiFactory.Json))!;
        Assert.All(units, u => Assert.Equal(UnitStatus.Published, u.Status));
        var unit = (await admin.GetFromJsonAsync<UnitAdminDto>($"/api/admin/units/{units[0].Id}", ApiFactory.Json))!;
        Assert.All(unit.Words, w => Assert.Equal((CatalogWordStatus.Reviewed, "AI (fake-claude)"), (w.Status, w.ReviewedBy)));
        var trapped = unit.Words.Single(w => w.Word == "각다");
        Assert.Equal(("회사에서 가요", false), (trapped.TrapWrong, trapped.TrapScenePending)); // the scene is confirmed
        Assert.False(unit.Words.Single(w => w.Word == "갇다").PronunciationReview!.Reviewed); // the rules do not explain it
        Assert.True(unit.Words.Single(w => w.Word == "간다").PronunciationReview!.Reviewed);

        // A person's check takes the word over from the AI.
        var word = unit.Words.Single(w => w.Word == "간다");
        var edited = await admin.PutAsJsonAsync($"/api/admin/catalog/words/{word.Id}", new UpdateCatalogWordRequest(word.TranslationUz + "; boshqa", word.Romanization, word.ExampleKo, word.ExampleUz, word.TrapUz, word.Theme, null, word.Version), ApiFactory.Json);
        Assert.True(edited.IsSuccessStatusCode, await edited.Content.ReadAsStringAsync());
        Assert.NotEqual("AI (fake-claude)", (await edited.Content.ReadFromJsonAsync<CatalogWordAdminDto>(ApiFactory.Json))!.ReviewedBy);
    }

    [Fact]
    public async Task The_background_job_runs_for_the_admin_only_and_stops_on_a_word_it_cannot_complete()
    {
        var ai = new FakeAiProvider(AiProviderKind.Anthropic);
        // The AI never writes a usable card, so nothing can be published.
        ai.Answer = r => r.SchemaName == "catalog_draft" ? """{"words":[]}""" : Answer(r, null);
        await using var f = new ApiFactory { AdminTelegramIds = [ApiFactory.UserA], AiProviders = [ai] };
        f.Settings["Catalog:AutopilotRetryDelay"] = "00:00:00.050";
        var admin = await f.SignedInClientAsync(ApiFactory.UserA);
        var learner = await f.SignedInClientAsync(ApiFactory.UserB);
        await admin.PostAsync("/api/admin/catalog/import", CatalogFiles.Upload(CatalogFiles.Xlsx(Level()), "b.xlsx"));

        Assert.Equal(HttpStatusCode.Forbidden, (await learner.PostAsync("/api/admin/catalog/levels/B/autopilot", null)).StatusCode);
        var started = await admin.PostAsync("/api/admin/catalog/levels/B/autopilot", null);
        Assert.True(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync("/api/admin/catalog/levels/A/autopilot", null)).StatusCode);

        ContentAutopilotDto? status = null;
        for (var i = 0; i < 300 && status is not { Running: false }; i++)
        {
            await Task.Delay(100);
            status = await admin.GetFromJsonAsync<ContentAutopilotDto>("/api/admin/catalog/autopilot", ApiFactory.Json);
        }

        Assert.NotNull(status);
        Assert.False(status.Running);
        // Three tries, then the job ends and says why.
        Assert.True(status.Stopped);
        Assert.Contains("AI javobi tekshiruvdan o‘tmadi", status.Message);
        Assert.Equal(3, ai.Requests.Count(r => r.SchemaName == "catalog_draft"));
        Assert.Empty(status.PublishedUnits);
        var units = (await admin.GetFromJsonAsync<List<UnitAdminSummaryDto>>("/api/admin/units?level=B", ApiFactory.Json))!;
        Assert.All(units, u => Assert.Equal(UnitStatus.Draft, u.Status));
    }

    [Fact]
    public async Task A_scene_that_never_passes_the_checks_is_dropped_and_the_level_is_still_published()
    {
        // L-89: the two lines differ only by a space (집안에 / 집 안에) — the checks refuse it every time.
        var ai = new FakeAiProvider(AiProviderKind.Anthropic);
        ai.Answer = r => r.SchemaName == "trap_scenes"
            ? """{"words":[{"id":1,"cue":"Bola qayerda:","wrong":"아이는 집안에 있어요","right":"아이는 집 안에 있어요","why":"Ajratib yoziladi."}]}"""
            : Answer(r, null);
        await using var f = new ApiFactory { AdminTelegramIds = [ApiFactory.UserA], AiProviders = [ai] };
        var admin = await f.SignedInClientAsync(ApiFactory.UserA);
        await admin.PostAsync("/api/admin/catalog/import", CatalogFiles.Upload(CatalogFiles.Xlsx(Level()), "b.xlsx"));
        var userId = await UserIdAsync(f, ApiFactory.UserA);

        AutopilotStep? last = null;
        for (var i = 0; i < 40 && last is not { Finished: true }; i++)
        {
            await using var scope = f.Services.CreateAsyncScope();
            last = await scope.ServiceProvider.GetRequiredService<ContentAutopilotService>().StepAsync(userId, CatalogLevel.B, CancellationToken.None);
        }

        Assert.Equal((true, false), (last!.Finished, last.Stopped));
        Assert.Single(ai.Requests, r => r.SchemaName == "trap_scenes"); // asked once, not again and again
        var units = (await admin.GetFromJsonAsync<List<UnitAdminSummaryDto>>("/api/admin/units?level=B", ApiFactory.Json))!;
        Assert.All(units, u => Assert.Equal(UnitStatus.Published, u.Status));
        var trapped = (await admin.GetFromJsonAsync<UnitAdminDto>($"/api/admin/units/{units[0].Id}", ApiFactory.Json))!.Words.Single(w => w.Word == "각다");
        Assert.Null(trapped.TrapWrong);
        Assert.Equal(GoTrap, trapped.TrapUz); // the card keeps its trap note
    }

    [Fact]
    public void Catalog_content_goes_to_the_content_model_when_one_is_set()
    {
        var options = new AiProviderOptions { Model = "everyday", ContentModel = " strong " };
        var content = new AiJsonRequest("f", "i", "u", "s", "{}", 10, Content: true);
        Assert.Equal("strong", options.ModelFor(content));
        Assert.Equal("everyday", options.ModelFor(content with { Content = false }));
        Assert.Equal("everyday", new AiProviderOptions { Model = "everyday" }.ModelFor(content));
    }
}
