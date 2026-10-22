using System.Security.Claims;
using KoreanAiTeacher.Api.Auth;
using KoreanAiTeacher.Application.Imports;
using KoreanAiTeacher.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace KoreanAiTeacher.Api.Imports;

public static class ImportEndpoints
{
    /// <summary>Slightly above the 100 MiB file limit (D-20) to leave room for multipart overhead.</summary>
    private const long MaxRequestBytes = 101 * 1024 * 1024;

    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var imports = app.MapGroup("/api/imports").RequireAuthorization();

        imports.MapPost("/", Upload)
            .DisableAntiforgery() // bearer-token API, no cookies: CSRF does not apply
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
            .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxRequestBytes });

        var books = app.MapGroup("/api/grammar/books").RequireAuthorization();
        books.MapPost("/", UploadGrammar)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
            .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxRequestBytes });
        books.MapGet("/", (ClaimsPrincipal user, ImportService s, CancellationToken ct) =>
            s.ListDocumentsAsync(user.GetUserId(), Domain.Imports.SourceDocumentKind.Grammar, ct));

        imports.MapGet("/", (ClaimsPrincipal user, ImportService s, CancellationToken ct) =>
            s.ListJobsAsync(user.GetUserId(), ct));

        imports.MapGet("/{jobId:guid}", (Guid jobId, ClaimsPrincipal user, ImportService s, CancellationToken ct) =>
            s.GetJobAsync(user.GetUserId(), jobId, ct));

        imports.MapGet("/{jobId:guid}/tables", (Guid jobId, ClaimsPrincipal user, ImportService s, CancellationToken ct) =>
            s.ListTablesAsync(user.GetUserId(), jobId, ct));

        imports.MapGet("/{jobId:guid}/entries", (Guid jobId, int? table, bool? flagged, ClaimsPrincipal user, ImportService s, CancellationToken ct) =>
            s.ListEntriesAsync(user.GetUserId(), jobId, table, flagged ?? false, ct));

        imports.MapPost("/{jobId:guid}/tables/{table:int}/approve", (Guid jobId, int table, ClaimsPrincipal user, ImportService s, CancellationToken ct) =>
            s.ApproveTableAsync(user.GetUserId(), jobId, table, ct));

        imports.MapGet("/{jobId:guid}/file", async (Guid jobId, ClaimsPrincipal user, ImportService s, CancellationToken ct) =>
        {
            var (content, _) = await s.OpenOriginalAsync(user.GetUserId(), jobId, ct);
            return TypedResults.Stream(content, "application/pdf");
        });

        app.MapPatch("/api/entries/{entryId:guid}", (Guid entryId, UpdateEntryRequest body, ClaimsPrincipal user, ImportService s, CancellationToken ct) =>
                s.UpdateEntryAsync(user.GetUserId(), entryId, new EntryPatch(body.Korean, body.KoreanNote, body.Uzbek, body.Status, body.Version), ct))
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Upload(IFormFile? file, ClaimsPrincipal user, ImportService s, CancellationToken ct)
    {
        if (file is null)
        {
            return TypedResults.Problem(detail: "Fayl tanlanmagan.", statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        await using var stream = file.OpenReadStream();
        var result = await s.UploadVocabularyAsync(user.GetUserId(), file.FileName, stream, ct);
        return result.Created
            ? TypedResults.Created($"/api/imports/{result.Job.Id}", result.Job)
            : TypedResults.Ok(result.Job);
    }

    private static async Task<IResult> UploadGrammar(IFormFile? file, ClaimsPrincipal user, ImportService s, CancellationToken ct)
    {
        if (file is null)
        {
            return TypedResults.Problem(detail: "Fayl tanlanmagan.", statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        await using var stream = file.OpenReadStream();
        var (document, created) = await s.UploadGrammarBookAsync(user.GetUserId(), file.FileName, stream, ct);
        return created ? TypedResults.Created($"/api/grammar/books/{document.Id}", document) : TypedResults.Ok(document);
    }
}
