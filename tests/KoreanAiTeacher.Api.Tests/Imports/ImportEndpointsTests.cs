using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Api.Tests.Infrastructure;
using KoreanAiTeacher.Application.Abstractions;
using KoreanAiTeacher.Application.Imports;
using KoreanAiTeacher.Domain.Imports;
using Microsoft.Extensions.DependencyInjection;

namespace KoreanAiTeacher.Api.Tests.Imports;

public class ImportEndpointsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly byte[] PdfBytes = "%PDF-1.7\nfake body"u8.ToArray();

    private readonly FakePdfLayoutReader _pdf = new FakePdfLayoutReader()
        .AddPage(("도리어", "aksincha"), ("고집", "o’jarlik"), ("피하다", "qochmoq, oldini olmoq"), ("적극적", "faol"), ("편찮다", "kasal bo’lmoq"))
        .AddPage([.. FakePdfLayoutReader.Words("둘", 5), ("바닥나다", "qolmaslik 2. teshilmoq")]);

    private ApiFactory Factory() => new() { PdfReader = _pdf };

    private static async Task<HttpResponseMessage> Upload(HttpClient client, byte[] bytes, string name = "lugat.pdf")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", name);
        return await client.PostAsync("/api/imports", form);
    }

    private static async Task<ImportJobDto> WaitForAsync(HttpClient client, Guid jobId, params ImportStatus[] statuses)
    {
        for (var i = 0; i < 100; i++)
        {
            var job = await client.GetFromJsonAsync<ImportJobDto>($"/api/imports/{jobId}", Json);
            if (statuses.Contains(job!.Status))
            {
                return job;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Import {jobId} did not reach {string.Join('/', statuses)}");
    }

    private static async Task<ImportJobDto> UploadAndProcessAsync(HttpClient client)
    {
        var response = await Upload(client, PdfBytes);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var job = (await response.Content.ReadFromJsonAsync<ImportJobDto>(Json))!;
        return await WaitForAsync(client, job.Id, ImportStatus.NeedsReview);
    }

    [Fact]
    public async Task Upload_is_extracted_in_the_background_into_tables_for_review()
    {
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var job = await UploadAndProcessAsync(client);

        Assert.Equal(11, job.TotalEntries);
        Assert.Equal(11, job.NeedsReview);
        Assert.Equal(1, job.Flagged);
        var tables = await client.GetFromJsonAsync<List<ImportTableDto>>($"/api/imports/{job.Id}/tables", Json);
        Assert.Equal([5, 6], tables!.Select(t => t.Total));
        var entries = await client.GetFromJsonAsync<List<SourceEntryDto>>($"/api/imports/{job.Id}/entries?table=1", Json);
        Assert.Equal(("도리어", "aksincha", 1), (entries![0].Korean, entries[0].Uzbek, entries[0].PdfPage));
        Assert.Equal("aksincha", entries[0].OriginalUzbek);
    }

    [Fact]
    public async Task Uploading_the_same_file_again_returns_the_same_import()
    {
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var first = await UploadAndProcessAsync(client);

        var again = await Upload(client, PdfBytes, "boshqa-nom.pdf");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(first.Id, (await again.Content.ReadFromJsonAsync<ImportJobDto>(Json))!.Id);
        var all = await client.GetFromJsonAsync<List<ImportJobDto>>("/api/imports", Json);
        Assert.Single(all!);
        Assert.Equal(11, all![0].TotalEntries);
    }

    [Fact]
    public async Task Other_user_cannot_see_or_change_the_import()
    {
        await using var factory = Factory();
        var owner = await factory.SignedInClientAsync(ApiFactory.UserA);
        var other = await factory.SignedInClientAsync(ApiFactory.UserB);
        var job = await UploadAndProcessAsync(owner);
        var entry = (await owner.GetFromJsonAsync<List<SourceEntryDto>>($"/api/imports/{job.Id}/entries", Json))![0];

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/imports/{job.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/imports/{job.Id}/entries")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/imports/{job.Id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/imports/{job.Id}/tables/1/approve", null)).StatusCode);
        var patch = await other.PatchAsJsonAsync($"/api/entries/{entry.Id}", new { uzbek = "buzildi", version = entry.Version });
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<ImportJobDto>>("/api/imports", Json))!);

        // The same file uploaded by the other user is their own, separate import.
        var theirs = await Upload(other, PdfBytes);
        Assert.Equal(HttpStatusCode.Created, theirs.StatusCode);
    }

    [Fact]
    public async Task Approving_a_table_leaves_flagged_rows_for_a_person()
    {
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var job = await UploadAndProcessAsync(client);

        var t1 = await (await client.PostAsync($"/api/imports/{job.Id}/tables/1/approve", null)).Content.ReadFromJsonAsync<ApproveTableResult>(Json);
        var t2 = await (await client.PostAsync($"/api/imports/{job.Id}/tables/2/approve", null)).Content.ReadFromJsonAsync<ApproveTableResult>(Json);

        Assert.Equal(new ApproveTableResult(5, 0), t1);
        Assert.Equal(new ApproveTableResult(5, 1), t2);
        Assert.Equal(ImportStatus.NeedsReview, (await client.GetFromJsonAsync<ImportJobDto>($"/api/imports/{job.Id}", Json))!.Status);

        var flagged = (await client.GetFromJsonAsync<List<SourceEntryDto>>($"/api/imports/{job.Id}/entries?flagged=true", Json))!.Single();
        Assert.Equal("바닥나다", flagged.Korean);
        Assert.True(flagged.Blocking);

        var fixedEntry = await client.PatchAsJsonAsync(
            $"/api/entries/{flagged.Id}",
            new { uzbek = "1. tugamoq 2. teshilmoq", status = "Approved", version = flagged.Version },
            Json);
        Assert.Equal(HttpStatusCode.OK, fixedEntry.StatusCode);
        var saved = (await fixedEntry.Content.ReadFromJsonAsync<SourceEntryDto>(Json))!;
        Assert.Equal("1. tugamoq 2. teshilmoq", saved.Uzbek);
        Assert.Equal("qolmaslik 2. teshilmoq", saved.OriginalUzbek); // the printed text is kept (spec SRC03)

        var done = await client.GetFromJsonAsync<ImportJobDto>($"/api/imports/{job.Id}", Json);
        Assert.Equal(ImportStatus.Confirmed, done!.Status);
        Assert.Equal(11, done.Approved);
    }

    [Fact]
    public async Task Stale_version_is_a_conflict()
    {
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var job = await UploadAndProcessAsync(client);
        var entry = (await client.GetFromJsonAsync<List<SourceEntryDto>>($"/api/imports/{job.Id}/entries", Json))![0];

        var first = await client.PatchAsJsonAsync($"/api/entries/{entry.Id}", new { uzbek = "birinchi", version = entry.Version });
        var second = await client.PatchAsJsonAsync($"/api/entries/{entry.Id}", new { uzbek = "ikkinchi", version = entry.Version });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Korean_field_must_contain_hangul()
    {
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var job = await UploadAndProcessAsync(client);
        var entry = (await client.GetFromJsonAsync<List<SourceEntryDto>>($"/api/imports/{job.Id}/entries", Json))![0];

        var response = await client.PatchAsJsonAsync($"/api/entries/{entry.Id}", new { korean = "doria", version = entry.Version });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Non_pdf_is_rejected_with_a_reason()
    {
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var response = await Upload(client, "hello"u8.ToArray(), "lugat.pdf");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("PDF", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Oversized_file_is_413()
    {
        await using var factory = Factory();
        factory.Settings["Imports:MaxFileBytes"] = "10";
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var response = await Upload(client, PdfBytes);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Encrypted_pdf_is_rejected_at_upload()
    {
        _pdf.FailOnCount = new PdfReadException("PDF parol bilan himoyalangan. Parolsiz nusxasini yuklang.");
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var response = await Upload(client, PdfBytes);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("parol", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Extraction_failure_is_reported_on_the_job()
    {
        _pdf.FailOnRead = new PdfReadException("PDF faylni o‘qib bo‘lmadi. Fayl buzilgan bo‘lishi mumkin.");
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var created = (await (await Upload(client, PdfBytes)).Content.ReadFromJsonAsync<ImportJobDto>(Json))!;

        var job = await WaitForAsync(client, created.Id, ImportStatus.Failed);

        Assert.Contains("buzilgan", job.Error);
        Assert.Equal(0, job.TotalEntries);
    }

    [Fact]
    public async Task Uploading_the_same_file_after_a_failure_retries_it()
    {
        _pdf.FailOnRead = new PdfReadException("PDF faylni o‘qib bo‘lmadi. Fayl buzilgan bo‘lishi mumkin.");
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var failed = (await (await Upload(client, PdfBytes)).Content.ReadFromJsonAsync<ImportJobDto>(Json))!;
        await WaitForAsync(client, failed.Id, ImportStatus.Failed);

        _pdf.FailOnRead = null; // the temporary problem is gone
        var retry = await Upload(client, PdfBytes);
        var retried = (await retry.Content.ReadFromJsonAsync<ImportJobDto>(Json))!;
        var done = await WaitForAsync(client, retried.Id, ImportStatus.NeedsReview);

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.NotEqual(failed.Id, retried.Id);
        Assert.Equal(failed.SourceDocumentId, retried.SourceDocumentId);
        Assert.Equal(11, done.TotalEntries);
    }

    [Fact]
    public async Task Approved_word_cannot_lose_its_translation_by_a_plain_save()
    {
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var job = await UploadAndProcessAsync(client);
        (await client.PostAsync($"/api/imports/{job.Id}/tables/1/approve", null)).EnsureSuccessStatusCode();
        var entry = (await client.GetFromJsonAsync<List<SourceEntryDto>>($"/api/imports/{job.Id}/entries?table=1", Json))![0];

        var response = await client.PatchAsJsonAsync($"/api/entries/{entry.Id}", new { uzbek = "   ", version = entry.Version });
        var after = (await client.GetFromJsonAsync<List<SourceEntryDto>>($"/api/imports/{job.Id}/entries?table=1", Json))![0];

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("aksincha", after.Uzbek);
        Assert.Equal(EntryStatus.Approved, after.Status);
    }

    [Fact]
    public async Task Edit_and_practice_card_are_saved_together()
    {
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var job = await UploadAndProcessAsync(client);
        (await client.PostAsync($"/api/imports/{job.Id}/tables/1/approve", null)).EnsureSuccessStatusCode();
        var entry = (await client.GetFromJsonAsync<List<SourceEntryDto>>($"/api/imports/{job.Id}/entries?table=1", Json))![0];

        (await client.PatchAsJsonAsync($"/api/entries/{entry.Id}", new { uzbek = "aksincha, teskari", version = entry.Version })).EnsureSuccessStatusCode();
        var session = (await (await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, false), Json))
            .Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;

        Assert.Equal("도리어", session.Current!.Prompt);
        Assert.Equal("aksincha, teskari", session.Current.Answer);
    }

    [Fact]
    public async Task Imports_require_sign_in()
    {
        await using var factory = Factory();

        var response = await Upload(factory.CreateClient(), PdfBytes);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Original_file_can_be_downloaded_by_its_owner()
    {
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var job = await UploadAndProcessAsync(client);

        var file = await client.GetAsync($"/api/imports/{job.Id}/file");

        Assert.Equal("application/pdf", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PdfBytes, await file.Content.ReadAsByteArrayAsync());
    }
    [Fact]
    public async Task Reupload_repairs_a_lost_original_without_reimporting_or_changing_progress()
    {
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var first = await UploadAndProcessAsync(client);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KoreanAiTeacher.Infrastructure.Persistence.AppDbContext>();
        var doc = await db.SourceDocuments.FindAsync(first.SourceDocumentId);
        var files = scope.ServiceProvider.GetRequiredService<IFileStore>();
        await files.DeleteAsync(doc!.StorageKey, CancellationToken.None);
        var again = await Upload(client, PdfBytes);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(first.Id, (await again.Content.ReadFromJsonAsync<ImportJobDto>(Json))!.Id);
        Assert.Equal(PdfBytes, await (await client.GetAsync($"/api/imports/{first.Id}/file")).Content.ReadAsByteArrayAsync());
    }

}

