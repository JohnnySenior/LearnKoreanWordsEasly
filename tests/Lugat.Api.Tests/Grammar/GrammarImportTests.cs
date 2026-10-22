using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Imports;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Ai;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Grammar;

namespace Lugat.Api.Tests.Grammar;

public class GrammarImportTests
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FakeAiProvider _ai = new(AiProviderKind.OpenAi);

    private readonly FakePdfLayoutReader _pdf = new FakePdfLayoutReader()
        .AddPage(FakePdfLayoutReader.Words("가", 5)).AddPage(FakePdfLayoutReader.Words("나", 5)).AddPage(FakePdfLayoutReader.Words("다", 5))
        .AddPage(FakePdfLayoutReader.Words("라", 5)).AddPage(FakePdfLayoutReader.Words("마", 5));

    internal const string Page2 = """
        {"items":[
          {"number":1,"section":"연결어미","form":"-(으)ㄹ수록","similar":null,"meaningKo":"비례","meaningUz":"Bir narsa oshgani sari ikkinchisi ham oshadi.",
           "connectionUz":"Fe’l va sifatga qo‘shiladi.","restrictionUz":null,
           "examples":[{"korean":"한국어는 배울수록 재미있어요.","uzbek":"Koreys tilini o‘rgangan sari qiziqarli bo‘ladi."}],"confidence":0.95,"note":null},
          {"number":2,"section":"연결어미","form":"-(으)ㄹ까 봐","similar":null,"meaningKo":"걱정","meaningUz":"Биров ҳақида хавотир.",
           "connectionUz":null,"restrictionUz":null,"examples":[],"confidence":0.9,"note":null}
        ]}
        """;

    private const string Page3 = """
        {"items":[
          {"number":3,"section":"종결어미","form":"-거든요","similar":null,"meaningKo":"이유","meaningUz":"Sababni tushuntiradi.",
           "connectionUz":null,"restrictionUz":null,"examples":[],"confidence":0.4,"note":"Shakl xira bosilgan."}
        ]}
        """;

    private ApiFactory NewFactory() => new() { PdfReader = _pdf, AiProviders = [_ai] };

    internal static async Task<SourceDocumentDto> UploadBookAsync(HttpClient client)
    {
        var file = new ByteArrayContent("%PDF-1.7 grammar book"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        using var form = new MultipartFormDataContent { { file, "file", "합격 레시피.pdf" } };
        return (await (await client.PostAsync("/api/grammar/books", form)).Content.ReadFromJsonAsync<SourceDocumentDto>(Json))!;
    }

    internal static async Task<GrammarImportDto> WaitAsync(HttpClient client, Guid id, params GrammarImportStatus[] statuses)
    {
        for (var i = 0; i < 150; i++)
        {
            var job = (await client.GetFromJsonAsync<GrammarImportDto>($"/api/grammar/imports/{id}", Json))!;
            if (statuses.Contains(job.Status))
            {
                return job;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Grammar import {id} did not reach {string.Join('/', statuses)}");
    }

    internal static async Task<GrammarImportDto> StartAsync(HttpClient client, Guid bookId, int from, int to)
    {
        var response = await client.PostAsJsonAsync($"/api/grammar/books/{bookId}/imports", new CreateGrammarImportRequest(from, to), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GrammarImportDto>(Json))!;
    }

    [Fact]
    public async Task Each_page_is_sent_alone_to_the_ai_and_items_wait_for_review()
    {
        _ai.Answer = r => r.UserContent.Contains("page 2") ? Page2 : r.UserContent.Contains("page 3") ? Page3 : """{"items":[]}""";
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await UploadBookAsync(client);

        var job = await WaitAsync(client, (await StartAsync(client, book.Id, 2, 4)).Id, GrammarImportStatus.NeedsReview, GrammarImportStatus.Failed);
        var items = (await client.GetFromJsonAsync<List<GrammarItemDto>>($"/api/grammar/imports/{job.Id}/items", Json))!;

        Assert.Equal(GrammarImportStatus.NeedsReview, job.Status);
        Assert.Equal([(2, 2), (3, 3), (4, 4)], _pdf.Extracted);
        Assert.All(_ai.Requests, r => Assert.Equal("application/pdf", Assert.Single(r.Attachments!).MediaType));
        Assert.Equal(["-(으)ㄹ수록", "-(으)ㄹ까 봐", "-거든요"], items.Select(i => i.Form));
        Assert.All(items, i => Assert.Equal(GrammarItemStatus.NeedsReview, i.Status));
        var first = items[0];
        Assert.False(first.Flagged);
        Assert.Equal(("연결어미", "비례", 2), (first.Section, first.MeaningKo, first.PdfPage));
        Assert.Equal("한국어는 배울수록 재미있어요.", Assert.Single(first.Examples).Korean);
        Assert.True(items[1].Flagged); // Cyrillic Uzbek
        Assert.Contains("lotin", items[1].Warning);
        Assert.True(items[2].Flagged); // unsure reading
        Assert.Contains("xira", items[2].Warning);
        Assert.Equal((3, 0, 2), (job.Items, job.Approved, job.Flagged));
    }

    [Fact]
    public async Task Page_approval_skips_flagged_items_until_they_are_corrected()
    {
        _ai.Answer = _ => Page2;
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await UploadBookAsync(client);
        var job = await WaitAsync(client, (await StartAsync(client, book.Id, 2, 2)).Id, GrammarImportStatus.NeedsReview);

        await client.PostAsync($"/api/grammar/imports/{job.Id}/pages/2/approve", null);
        var items = (await client.GetFromJsonAsync<List<GrammarItemDto>>($"/api/grammar/imports/{job.Id}/items?page=2", Json))!;
        var flagged = items.Single(i => i.Flagged);
        var fixedResponse = await client.PatchAsJsonAsync($"/api/grammar/items/{flagged.Id}",
            new UpdateGrammarItemRequest(null, null, null, "Biror narsadan xavotirlanganda ishlatiladi.", null, null, null, GrammarItemStatus.Approved, flagged.Version), Json);
        var fixedItem = (await fixedResponse.Content.ReadFromJsonAsync<GrammarItemDto>(Json))!;
        var stale = await client.PatchAsJsonAsync($"/api/grammar/items/{flagged.Id}",
            new UpdateGrammarItemRequest("x", null, null, null, null, null, null, null, flagged.Version), Json);
        var emptyMeaning = await client.PatchAsJsonAsync($"/api/grammar/items/{flagged.Id}",
            new UpdateGrammarItemRequest(null, null, null, "  ", null, null, null, null, fixedItem.Version), Json);

        Assert.Equal(GrammarItemStatus.Approved, items.Single(i => !i.Flagged).Status);
        Assert.Equal(GrammarItemStatus.NeedsReview, flagged.Status);
        Assert.Equal((GrammarItemStatus.Approved, false), (fixedItem.Status, fixedItem.Flagged));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, emptyMeaning.StatusCode);
    }

    [Fact]
    public async Task When_the_ai_is_down_the_import_fails_and_a_retry_continues_where_it_stopped()
    {
        _ai.Answer = r => r.UserContent.Contains("page 2") ? Page2 : throw new AiProviderException("ChatGPT: xizmat vaqtincha ishlamayapti (503)", transient: true);
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await UploadBookAsync(client);

        var failed = await WaitAsync(client, (await StartAsync(client, book.Id, 2, 3)).Id, GrammarImportStatus.Failed);
        _ai.Answer = _ => Page3; // the service is back
        _pdf.Extracted.Clear();
        (await client.PostAsync($"/api/grammar/imports/{failed.Id}/retry", null)).EnsureSuccessStatusCode();
        var done = await WaitAsync(client, failed.Id, GrammarImportStatus.NeedsReview);

        Assert.Equal(2, failed.DonePage);
        Assert.Contains("3-bet", failed.Error);
        Assert.Equal([(3, 3)], _pdf.Extracted); // page 2 was not read again
        Assert.Equal(3, done.Items);
    }

    [Fact]
    public async Task Page_range_is_checked_and_other_users_cannot_see_imports()
    {
        _ai.Answer = _ => """{"items":[]}""";
        await using var factory = NewFactory();
        var a = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = await UploadBookAsync(a);
        var job = await StartAsync(a, book.Id, 1, 2);
        var b = await factory.SignedInClientAsync(ApiFactory.UserB);

        var outside = await a.PostAsJsonAsync($"/api/grammar/books/{book.Id}/imports", new CreateGrammarImportRequest(4, 9), Json);
        var overlap = await a.PostAsJsonAsync($"/api/grammar/books/{book.Id}/imports", new CreateGrammarImportRequest(2, 3), Json);
        var foreignBook = await b.PostAsJsonAsync($"/api/grammar/books/{book.Id}/imports", new CreateGrammarImportRequest(3, 3), Json);
        var foreignJob = await b.GetAsync($"/api/grammar/imports/{job.Id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, outside.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignBook.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignJob.StatusCode);
    }
}
