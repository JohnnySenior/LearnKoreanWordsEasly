using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KoreanAiTeacher.Api.Tests.Infrastructure;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Domain.Imports;
using Microsoft.Extensions.DependencyInjection;

namespace KoreanAiTeacher.Api.Tests.Imports;

public class GrammarBookTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static MultipartFormDataContent Pdf(int megabytes)
    {
        var bytes = new byte[megabytes * 1024 * 1024];
        "%PDF-1.7 grammar"u8.CopyTo(bytes);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { file, "file", "합격 레시피.pdf" } };
    }

    [Fact]
    public async Task A_43_mb_grammar_book_is_stored_whole_and_not_parsed_as_vocabulary()
    {
        await using var factory = new ApiFactory { PdfReader = new FakePdfLayoutReader().AddPage(FakePdfLayoutReader.Words("가", 5)) };
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var response = await client.PostAsync("/api/grammar/books", Pdf(43));
        var again = await client.PostAsync("/api/grammar/books", Pdf(43));
        var books = await client.GetFromJsonAsync<List<SourceDocumentDto>>("/api/grammar/books", Json);
        var imports = await client.GetFromJsonAsync<List<ImportJobDto>>("/api/imports", Json);
        var other = await (await factory.SignedInClientAsync(ApiFactory.UserB)).GetFromJsonAsync<List<SourceDocumentDto>>("/api/grammar/books", Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode); // same file again (IMP07)
        var book = Assert.Single(books!);
        Assert.Equal((SourceDocumentKind.Grammar, "합격 레시피.pdf", 43L * 1024 * 1024), (book.Kind, book.FileName, book.SizeBytes));
        Assert.Empty(imports!);
        Assert.Empty(other!);
    }
    [Fact]
    public async Task Reupload_of_a_restored_book_repairs_the_file_instead_of_creating_a_duplicate()
    {
        await using var factory = new ApiFactory { PdfReader = new FakePdfLayoutReader().AddPage(FakePdfLayoutReader.Words("가", 5)) };
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var book = (await (await client.PostAsync("/api/grammar/books", Pdf(1))).Content.ReadFromJsonAsync<SourceDocumentDto>(Json))!;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KoreanAiTeacher.Infrastructure.Persistence.AppDbContext>();
        var doc = await db.SourceDocuments.FindAsync(book.Id);
        var files = scope.ServiceProvider.GetRequiredService<KoreanAiTeacher.Application.Abstractions.IFileStore>();
        await files.DeleteAsync(doc!.StorageKey, CancellationToken.None);
        var response = await client.PostAsync("/api/grammar/books", Pdf(1));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(book.Id, (await response.Content.ReadFromJsonAsync<SourceDocumentDto>(Json))!.Id);
        await using var repaired = await files.OpenReadAsync(doc.StorageKey, CancellationToken.None);
        Assert.NotNull(repaired);
    }

}

