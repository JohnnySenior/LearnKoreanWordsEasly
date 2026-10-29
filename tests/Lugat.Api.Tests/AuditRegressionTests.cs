using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Premium;
using Lugat.Domain.Vocabulary;
using Lugat.Infrastructure.Persistence;
using Lugat.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Lugat.Api.Tests;

// Regressions from the 2026-10-29 audit: provider stubs, a fake clock and an isolated PostgreSQL database.
public class AuditRegressionTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = ApiFactory.Json;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public async Task Invalid_ai_cards_should_not_exhaust_the_free_word_allowance()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi) { Answer = _ => "{\"words\":[]}" };
        await using var factory = new ApiFactory { AiProviders = [ai], Premium = false };
        factory.Settings["Ai:Budget:Prices:0:Provider"] = "OpenAi";
        factory.Settings["Ai:Budget:Prices:0:Model"] = "fake-gpt";
        factory.Settings["Ai:Budget:Prices:0:InputPerMillionUsd"] = "1";
        factory.Settings["Ai:Budget:Prices:0:OutputPerMillionUsd"] = "1";
        factory.Settings["Ai:Budget:Prices:0:AsOf"] = "2026-10-29";
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/my-words/lookup", new MyWordLookupRequest("piyoz"), Json)).StatusCode);
        ai.Answer = _ => "{\"words\":[{\"korean\":\"양파\",\"translationUz\":\"piyoz\",\"romanization\":\"yangpa\",\"exampleKo\":\"양파를 먹어요.\",\"exampleUz\":\"Piyoz yeyman.\",\"trapUz\":null}]}";
        var valid = await client.PostAsJsonAsync("/api/my-words/lookup", new MyWordLookupRequest("piyoz"), Json);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var usage = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AiUsages.ToListAsync();
        Assert.Equal(3, usage.Count(u => !u.Succeeded));
        Assert.Single(usage, u => u.Succeeded);
        Assert.All(usage, u => Assert.Equal(0.0002m, u.EstimatedUsd)); // Invalid cards still cost the provider's tokens.
    }

    [Fact]
    public async Task Returning_after_absence_should_not_introduce_more_than_ten_new_own_words()
    {
        await using var factory = new ApiFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        for (var i = 0; i < 11; i++)
        {
            var request = new AddMyWordRequest(Guid.NewGuid(), $"단어{i}", "so‘z", "daneo", "단어를 배워요.", "So‘z o‘rganaman.", null, PersonalWordSource.Ai, null, null);
            (await client.PostAsJsonAsync("/api/my-words", request, Json)).EnsureSuccessStatusCode();
        }
        factory.Time.Advance(TimeSpan.FromDays(2));
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var today = (await client.GetFromJsonAsync<TodayDto>("/api/today", Json))!;
        Assert.True(today.NewAvailable <= 20, $"Expected at most 20 new cards (10 words); actual {today.NewAvailable}");
        var start = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true), Json);
        var session = (await start.Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;
        Assert.Equal(20, session.Total);
        while (session.Current is { } card)
        {
            var answered = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/answers",
                new AnswerRequest(Guid.NewGuid(), card.Position, Lugat.Domain.Learning.ReviewAnswer.Easy, false, 1000), Json);
            answered.EnsureSuccessStatusCode();
            session = (await answered.Content.ReadFromJsonAsync<AnswerResultDto>(Json))!.Session;
        }
        Assert.Equal(0, (await client.GetFromJsonAsync<TodayDto>("/api/today", Json))!.NewAvailable);
        var words = (await client.GetFromJsonAsync<MyWordsDto>("/api/my-words", Json))!.Words;
        var learned = words.First(w => w.State != MyWordState.New);
        (await client.DeleteAsync($"/api/my-words/{learned.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(0, (await client.GetFromJsonAsync<TodayDto>("/api/today", Json))!.NewAvailable);
        factory.Time.Advance(TimeSpan.FromDays(1));
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var tomorrow = (await client.GetFromJsonAsync<TodayDto>("/api/today", Json))!;
        Assert.Equal(2, tomorrow.NewAvailable);
        Assert.True(tomorrow.DueNow > 0); // The new-word limit does not suppress existing reviews.
    }

    private static async Task<(HttpClient Admin, HttpClient Learner)> SceneSetup(ApiFactory factory)
    {
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);
        var learner = await factory.SignedInClientAsync(ApiFactory.UserB);
        await factory.AddWordsAsync(ApiFactory.UserB, 1, [("가다", "bormoq"), ("하다", "qilmoq"), ("먹다", "yemoq")]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var w in await db.CatalogWords.ToListAsync()) w.TrapUz = "Yo‘nalish: 회사에 가요 to‘g‘ri, 회사에서 가요 xato.";
        await db.SaveChangesAsync();
        return (admin, learner);
    }

    [Theory]
    [InlineData("{\"words\":[]}")]
    [InlineData("{\"words\":\"[{\\\"id\\\":1\"}")]
    [InlineData("{broken")]
    public async Task An_omitted_scene_response_should_allow_the_editor_to_retry(string json)
    {
        var ai = new FakeAiProvider(AiProviderKind.Anthropic) { Answer = _ => json };
        await using var factory = new ApiFactory { AdminTelegramIds = [ApiFactory.UserA], AiProviders = [ai] };
        var (admin, _) = await SceneSetup(factory);
        var response = await admin.PostAsync($"/api/admin/units/{ApiFactory.Unit(1)}/trap-scenes", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var unit = (await admin.GetFromJsonAsync<UnitAdminDto>($"/api/admin/units/{ApiFactory.Unit(1)}", Json))!;
        Assert.Equal(3, unit.ScenesRemaining);
    }

    [Theory]
    [InlineData("{\"words\":\"[{\\\"id\\\":1,\\\"wrong\\\":\\\"회사에서 가요\\\",\\\"right\\\":\\\"회사에 가요\\\"},{\\\"id\\\":2},{\\\"id\\\":3}]\"}")]
    [InlineData("{\"words\":\"{\\\"words\\\":[\\n{\\\"id\\\":1,\\\"cue\\\":null,\\\"wrong\\\":\\\"회사에서 가요\\\",\\\"right\\\":\\\"회사에 가요\\\",\\\"wrong_note\\\":null},{\\\"id\\\":2},{\\\"id\\\":3}]}\"}")]
    [InlineData("[{\"id\":1,\"wrong\":\"회사에서 가요\",\"right\":\"회사에 가요\"},{\"id\":2},{\"id\":3}]")]
    [InlineData("{\"words\":[{\"id\":1,\"wrong\":\"회사에서 가요\",\"right\":\"회사에 가요\"},{\"id\":2},{\"id\":3}]}")]
    public async Task A_claude_tool_answer_in_another_shape_is_still_read(string json)
    {
        // Claude can send the array as a JSON string, or bare, and leaves out null keys ("no pair" = id only).
        var ai = new FakeAiProvider(AiProviderKind.Anthropic) { Answer = _ => json };
        await using var factory = new ApiFactory { AdminTelegramIds = [ApiFactory.UserA], AiProviders = [ai] };
        var (admin, _) = await SceneSetup(factory);
        var response = await admin.PostAsync($"/api/admin/units/{ApiFactory.Unit(1)}/trap-scenes", null);
        response.EnsureSuccessStatusCode();
        var unit = (await response.Content.ReadFromJsonAsync<UnitAdminDto>(Json))!;
        Assert.Equal(0, unit.ScenesRemaining);
        Assert.Single(unit.Words, w => w.TrapScenePending);
    }

    [Fact]
    public async Task A_partial_scene_response_keeps_invalid_items_retryable_and_accepts_explicit_nulls()
    {
        var ai = new FakeAiProvider(AiProviderKind.Anthropic)
        {
            Answer = _ => """
                {"words":[
                  {"id":1,"cue":null,"wrong":"회사에서 가요","right":"회사에 가요"},
                  {"id":2,"cue":null,"wrong":null,"right":null},
                  {"id":3,"cue":null,"wrong":"먹어요","right":"먹어요"}]}
                """
        };
        await using var factory = new ApiFactory { AdminTelegramIds = [ApiFactory.UserA], AiProviders = [ai] };
        var (admin, _) = await SceneSetup(factory);
        var first = await admin.PostAsync($"/api/admin/units/{ApiFactory.Unit(1)}/trap-scenes", null);
        first.EnsureSuccessStatusCode();
        Assert.Equal(1, (await first.Content.ReadFromJsonAsync<UnitAdminDto>(Json))!.ScenesRemaining);
        ai.Answer = _ => """{"words":[{"id":1,"cue":null,"wrong":null,"right":null}]}""";
        var retry = await admin.PostAsync($"/api/admin/units/{ApiFactory.Unit(1)}/trap-scenes", null);
        retry.EnsureSuccessStatusCode();
        Assert.Equal(0, (await retry.Content.ReadFromJsonAsync<UnitAdminDto>(Json))!.ScenesRemaining);
        Assert.Contains("먹다", ai.Requests.Last().UserContent);
        Assert.DoesNotContain("가다", ai.Requests.Last().UserContent);
    }

    [Fact]
    public async Task Removing_a_catalog_scene_should_also_remove_it_from_the_own_word_card()
    {
        await using var factory = new ApiFactory { AdminTelegramIds = [ApiFactory.UserA] };
        var (_, learner) = await SceneSetup(factory);
        Guid catalogId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var w = await db.CatalogWords.SingleAsync(w => w.Word == "가다");
            catalogId = w.Id;
            w.TrapWrong = "회사에서 가요";
            w.TrapRight = "회사에 가요";
            w.TrapScenePending = false;
            await db.SaveChangesAsync();
        }
        var addedResponse = await learner.PostAsJsonAsync("/api/my-words", new AddMyWordRequest(Guid.NewGuid(), "", "", null, null, null, null, PersonalWordSource.Catalog, catalogId, null), Json);
        addedResponse.EnsureSuccessStatusCode();
        var added = (await addedResponse.Content.ReadFromJsonAsync<MyWordDto>(Json))!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var w = await db.CatalogWords.SingleAsync(w => w.Id == catalogId);
            w.TrapCue = w.TrapWrong = w.TrapRight = null;
            await db.SaveChangesAsync();
        }
        var started = await learner.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true, LearningItemIds: [added.LearningItemId]), Json);
        started.EnsureSuccessStatusCode();
        var card = (await started.Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!.Current!;
        Assert.Null(card.TrapScene);
    }

    private static void Set(object o, string name, object? value) => o.GetType().GetField(name, Private)!.SetValue(o, value);
    private static Task Call(object o, string name, object arg) => (Task)o.GetType().GetMethod(name, Private)!.Invoke(o, [arg])!;
    private static void Inject(object o, string name, object value) => o.GetType().GetProperty(name, Private)!.SetValue(o, value);

    [Fact]
    public async Task Korean_ime_confirmation_should_not_send_a_lookup()
    {
        var handler = new AuditHandler();
        var browser = new BrowserInterop(new AuditJs());
        var api = new SessionService(new HttpClient(handler) { BaseAddress = new Uri("https://audit.test/") }, browser);
        var page = new Lugat.Web.Pages.MyWords();
        Inject(page, "Api", api);
        Set(page, "_text", "가");
        await Call(page, "KeyAsync", new KeyboardEventArgs { Key = "Enter", IsComposing = true });
        Assert.Equal(0, handler.LookupCalls);
    }

    [Fact]
    public async Task Late_promo_response_should_not_replace_the_price_of_the_current_currency()
    {
        var handler = new AuditHandler();
        var browser = new BrowserInterop(new AuditJs());
        var api = new SessionService(new HttpClient(handler) { BaseAddress = new Uri("https://audit.test/") }, browser);
        var page = new Lugat.Web.Pages.Premium();
        Inject(page, "Api", api);
        Inject(page, "Browser", browser);
        Set(page, "_plans", new PremiumPlansDto([new PlanDto(SubscriptionPlan.Monthly, "1 oy", 5000, 1, 29000)], null, null, 0));
        Set(page, "_promo", "SALE");
        Set(page, "_check", new PromoCheckDto(true, "SALE", 10, 5000, 4500, null, Currency.Krw));
        var uz = Call(page, "SelectCurrencyAsync", Currency.Uzs);
        await handler.UzStarted.Task;
        var kr = Call(page, "SelectCurrencyAsync", Currency.Krw);
        await handler.KrStarted.Task;
        handler.Kr.SetResult(JsonResponse(new PromoCheckDto(true, "SALE", 10, 5000, 4500, null, Currency.Krw)));
        await kr;
        handler.Uz.SetResult(JsonResponse(new PromoCheckDto(true, "SALE", 10, 29000, 26100, null, Currency.Uzs)));
        await uz;
        Assert.Equal(Currency.Krw, page.GetType().GetField("_currency", Private)!.GetValue(page));
        Assert.Equal(4500, (int)page.GetType().GetProperty("Price", Private)!.GetValue(page)!);
    }

    private static HttpResponseMessage JsonResponse(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: Json) };
    private sealed class AuditHandler : HttpMessageHandler
    {
        public int LookupCalls;
        public TaskCompletionSource<HttpResponseMessage> Uz { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<HttpResponseMessage> Kr { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource UzStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource KrStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/lookup"))
            {
                LookupCalls++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{\"detail\":\"audit\"}", Encoding.UTF8, "application/json") };
            }
            var promo = (await request.Content!.ReadFromJsonAsync<PromoCheckRequest>(Json, ct))!;
            if (promo.Currency == Currency.Uzs) { UzStarted.SetResult(); return await Uz.Task; }
            KrStarted.SetResult(); return await Kr.Task;
        }
    }

    private sealed class AuditJs : IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => identifier switch
        {
            "import" => new((TValue)(object)this),
            "storageSet" => new((TValue)(object)true),
            _ => new(default(TValue)!),
        };
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
