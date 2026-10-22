using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Imports;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Grammar;
using static Lugat.Api.Tests.Grammar.GrammarImportTests;

namespace Lugat.Api.Tests.Grammar;

/// <summary>Grammar sets ("to‘plam", D-28): what the teacher gave to learn, with a deadline and a daily target.</summary>
public class GrammarSetTests
{
    private readonly FakeAiProvider _ai = new(AiProviderKind.OpenAi);

    private readonly FakePdfLayoutReader _pdf = new FakePdfLayoutReader()
        .AddPage(FakePdfLayoutReader.Words("가", 5)).AddPage(FakePdfLayoutReader.Words("나", 5));

    /// <summary>Six points on page 2, all approved.</summary>
    private const string Page = """
        {"items":[
          {"number":1,"section":"연결어미","form":"-(으)ㄹ수록","similar":null,"meaningKo":null,"meaningUz":"Oshgani sari.","connectionUz":null,"restrictionUz":null,"examples":[],"confidence":0.9,"note":null},
          {"number":2,"section":"연결어미","form":"-거든","similar":null,"meaningKo":null,"meaningUz":"Agar bo‘lsa.","connectionUz":null,"restrictionUz":null,"examples":[],"confidence":0.9,"note":null},
          {"number":3,"section":"연결어미","form":"-느라고","similar":null,"meaningKo":null,"meaningUz":"Sababli.","connectionUz":null,"restrictionUz":null,"examples":[],"confidence":0.9,"note":null},
          {"number":4,"section":"연결어미","form":"-다가","similar":null,"meaningKo":null,"meaningUz":"Qilib turib.","connectionUz":null,"restrictionUz":null,"examples":[],"confidence":0.9,"note":null},
          {"number":5,"section":"연결어미","form":"-더니","similar":null,"meaningKo":null,"meaningUz":"Ko‘rgandim, keyin.","connectionUz":null,"restrictionUz":null,"examples":[],"confidence":0.9,"note":null},
          {"number":6,"section":"연결어미","form":"-자마자","similar":null,"meaningKo":null,"meaningUz":"Bilanoq.","connectionUz":null,"restrictionUz":null,"examples":[],"confidence":0.9,"note":null}
        ]}
        """;

    private async Task<(ApiFactory Factory, HttpClient Client, List<GrammarItemDto> Items)> SetUpAsync()
    {
        _ai.Answer = r => r.Feature == "grammar-page" ? Page : FakeAiProvider.Good;
        var factory = new ApiFactory { PdfReader = _pdf, AiProviders = [_ai] };
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await UploadBookAsync(client);
        var job = await WaitAsync(client, (await StartAsync(client, book.Id, 2, 2)).Id, GrammarImportStatus.NeedsReview);
        await client.PostAsync($"/api/grammar/imports/{job.Id}/pages/2/approve", null);
        var items = (await client.GetFromJsonAsync<List<GrammarItemDto>>($"/api/grammar/imports/{job.Id}/items", Json))!;
        return (factory, client, items);
    }

    private static DateOnly Today(ApiFactory f) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(f.Time.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul")).DateTime);

    private static async Task<HttpResponseMessage> CreateAsync(HttpClient client, IEnumerable<Guid> ids, DateOnly? due) =>
        await client.PostAsJsonAsync("/api/grammar/sets", new CreateGrammarSetRequest(ids.ToList(), due), Json);

    [Fact]
    public async Task A_set_spreads_its_points_over_the_days_until_the_deadline()
    {
        var (factory, client, items) = await SetUpAsync();
        await using var _ = factory;

        var response = await CreateAsync(client, items.Take(5).Select(i => i.Id), Today(factory).AddDays(1));
        var set = (await response.Content.ReadFromJsonAsync<GrammarSetDetailDto>(Json))!;
        var before = (await client.GetFromJsonAsync<GrammarTodayDto>("/api/grammar/today", Json))!;
        (await client.PostAsJsonAsync("/api/practice/check", new SentenceCheckRequest(Guid.NewGuid(), null, "운동을 할수록 건강해져요.", items[0].Id), Json)).EnsureSuccessStatusCode();
        var after = (await client.GetFromJsonAsync<GrammarTodayDto>("/api/grammar/today", Json))!;
        var detail = (await client.GetFromJsonAsync<GrammarDetailDto>($"/api/grammar/items/{items[1].Id}?set={set.Set.Id}", Json))!;
        var list = (await client.GetFromJsonAsync<List<GrammarListItemDto>>("/api/grammar/items", Json))!;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((1, 5), (set.Set.Number, set.Set.Total));
        Assert.Equal(items.Take(5).Select(i => i.Id), set.Items.Select(i => i.Id));
        var current = before.Current!;
        Assert.Equal((3, 0, items[0].Id), (current.TodayTarget, current.StartedToday, current.NextItemId)); // 5 over 2 days
        Assert.Equal((0, 1), (before.ReviewDue, before.FreeItems));
        Assert.Equal((3, 1, 1, items[1].Id), (after.Current!.TodayTarget, after.Current.StartedToday, after.Current.Started, after.Current.NextItemId));
        Assert.Equal((set.Set.Id, 1, items[0].Id, items[2].Id), (detail.SetId, detail.SetNumber, detail.PreviousId, detail.NextId));
        Assert.Equal([1, 1, 1, 1, 1, null], list.Select(i => i.SetNumber));
    }

    [Fact]
    public async Task After_the_deadline_points_not_yet_independent_come_back_for_review()
    {
        var (factory, client, items) = await SetUpAsync();
        await using var _ = factory;
        (await CreateAsync(client, items.Take(2).Select(i => i.Id), Today(factory))).EnsureSuccessStatusCode();

        factory.Time.Advance(TimeSpan.FromDays(2));
        client = await factory.SignedInClientAsync(ApiFactory.UserA); // the access token lasts an hour
        var today = (await client.GetFromJsonAsync<GrammarTodayDto>("/api/grammar/today", Json))!;
        var sets = (await client.GetFromJsonAsync<List<GrammarSetDto>>("/api/grammar/sets", Json))!;

        Assert.Null(today.Current);
        Assert.Equal((2, items[0].Id), (today.ReviewDue, today.ReviewNextItemId));
        Assert.True(Assert.Single(sets).Overdue);
    }

    [Fact]
    public async Task Points_must_be_approved_and_in_one_set_only_and_sets_can_be_edited_and_deleted()
    {
        var (factory, client, items) = await SetUpAsync();
        await using var _ = factory;
        var unapproved = items[5];
        await client.PatchAsJsonAsync($"/api/grammar/items/{unapproved.Id}",
            new UpdateGrammarItemRequest(null, null, null, null, null, null, null, GrammarItemStatus.NeedsReview, unapproved.Version), Json);

        var first = (await (await CreateAsync(client, items.Take(3).Select(i => i.Id), null)).Content.ReadFromJsonAsync<GrammarSetDetailDto>(Json))!;
        var taken = await CreateAsync(client, [items[2].Id, items[3].Id], null);
        var unchecked_ = await CreateAsync(client, [unapproved.Id], null);
        var past = await CreateAsync(client, [items[3].Id], Today(factory).AddDays(-1));
        var empty = await CreateAsync(client, [], null);
        var edited = await client.PatchAsJsonAsync($"/api/grammar/sets/{first.Set.Id}",
            new UpdateGrammarSetRequest(Today(factory).AddDays(3), false, [items[3].Id], [items[0].Id], first.Set.Version), Json);
        var editedSet = (await edited.Content.ReadFromJsonAsync<GrammarSetDetailDto>(Json))!;
        var stale = await client.PatchAsJsonAsync($"/api/grammar/sets/{first.Set.Id}", new UpdateGrammarSetRequest(null, true, null, null, first.Set.Version), Json);
        var foreign = await (await factory.SignedInClientAsync(ApiFactory.UserB)).GetAsync($"/api/grammar/sets/{first.Set.Id}");
        var deleted = await client.DeleteAsync($"/api/grammar/sets/{first.Set.Id}");
        var list = (await client.GetFromJsonAsync<List<GrammarListItemDto>>("/api/grammar/items", Json))!;
        var second = (await (await CreateAsync(client, [items[0].Id], null)).Content.ReadFromJsonAsync<GrammarSetDetailDto>(Json))!;

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unchecked_.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, past.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, empty.StatusCode);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal([items[1].Id, items[2].Id, items[3].Id], editedSet.Items.Select(i => i.Id));
        Assert.Equal(Today(factory).AddDays(3), editedSet.Set.DueDate);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.All(list, i => Assert.Null(i.SetNumber));
        Assert.Equal(1, second.Set.Number); // numbers follow the sets that exist
    }
    [Fact]
    public async Task A_test_can_be_built_from_a_grammar_set()
    {
        var (factory, client, items) = await SetUpAsync();
        await using var _ = factory;
        var set = (await (await CreateAsync(client, items.Take(5).Select(i => i.Id), null)).Content.ReadFromJsonAsync<GrammarSetDetailDto>(Json))!;
        _ai.Answer = r => r.Feature == "test-gaps"
            ? "{\"items\":[" + string.Join(",", r.UserContent.Split('\n').Where(l => l.Contains("grammar_choice")).Select(l => l[..l.IndexOf('.')]).Select(id =>
                $"{{\"id\":{id},\"sentence\":\"공부를 ___ 재미있어요.\",\"translationUz\":\"O‘qigan sari qiziq.\",\"answer\":\"할수록\",\"options\":[\"할수록\",\"하거든\",\"하느라고\",\"하다가\"],\"explanationUz\":\"Oshgani sari.\"}}")) + "]}"
            : FakeAiProvider.Good;

        var response = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [], set.Set.Id, false, 10), Json);
        var test = (await response.Content.ReadFromJsonAsync<TestDto>(Json))!;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("1-to‘plam", test.Title);
        Assert.Equal(10, test.Questions.Count);
        Assert.Contains(test.Questions, q => q.Kind == Domain.Testing.TestQuestionKind.GrammarChoice && q.Options.Contains("할수록"));
        Assert.Contains(test.Questions, q => q.Kind == Domain.Testing.TestQuestionKind.GrammarMeaning && q.Options.Count == 4);
    }
}
