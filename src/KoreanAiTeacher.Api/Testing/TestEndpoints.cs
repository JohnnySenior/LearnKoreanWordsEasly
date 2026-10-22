using System.Security.Claims;
using KoreanAiTeacher.Api.Auth;
using KoreanAiTeacher.Application.Testing;
using KoreanAiTeacher.Contracts;

namespace KoreanAiTeacher.Api.Testing;

public static class TestEndpoints
{
    public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder app)
    {
        // Practice tests (spec TEST01–TEST04, D-30): answers stay hidden until the test is finished.
        var tests = app.MapGroup("/api/tests").RequireAuthorization();
        tests.MapGet("/options", (ClaimsPrincipal user, TestService s, CancellationToken ct) => s.OptionsAsync(user.GetUserId(), ct));
        tests.MapGet("/", (TestCategory? category, ClaimsPrincipal user, TestService s, CancellationToken ct) => s.ListAsync(user.GetUserId(), ct, category));
        tests.MapPost("/", async (CreateTestRequest body, ClaimsPrincipal user, TestService s, CancellationToken ct) =>
        {
            var created = await s.CreateAsync(user.GetUserId(), body, ct);
            return TypedResults.Created($"/api/tests/{created.Id}", created);
        });
        tests.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal user, TestService s, CancellationToken ct) => s.GetAsync(user.GetUserId(), id, ct));
        tests.MapPost("/{id:guid}/answers", (Guid id, TestAnswerRequest body, ClaimsPrincipal user, TestService s, CancellationToken ct) =>
            s.AnswerAsync(user.GetUserId(), id, body, ct));
        // Oral readiness (TEST02, D-38): a short WAV for an "Uzbek → say the Korean word" question.
        tests.MapPost("/{id:guid}/oral/{index:int}", async (Guid id, int index, HttpRequest request, ClaimsPrincipal user, TestService s, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.Problem(detail: "Yozuv yuborilmadi.", statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var form = await request.ReadFormAsync(ct);
            if (form.Files["audio"] is not { Length: > 0 and <= Application.Speech.SpeakingService.MaxBytes } file)
            {
                return Results.Problem(detail: "Yozuv yo‘q yoki juda uzun (ko‘pi bilan 30 soniya).", statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var assisted = form["assisted"].FirstOrDefault() is "1" or "true";
            return Results.Ok(await s.OralAnswerAsync(user.GetUserId(), id, index, ms.ToArray(), assisted, ct));
        }).DisableAntiforgery();
        tests.MapPost("/{id:guid}/oral/{index:int}/technical", (Guid id, int index, ClaimsPrincipal user, TestService s, CancellationToken ct) =>
            s.OralTechnicalAsync(user.GetUserId(), id, index, ct));
        tests.MapPost("/{id:guid}/finish", (Guid id, ClaimsPrincipal user, TestService s, CancellationToken ct) => s.FinishAsync(user.GetUserId(), id, ct));

        // Tests from the learner's TOPIK book (GR06, D-33): pages read in the background, then used like any test.
        var book = app.MapGroup("/api/book-tests").RequireAuthorization();
        book.MapGet("/", (ClaimsPrincipal user, BookTestService s, CancellationToken ct) => s.ListAsync(user.GetUserId(), ct));
        book.MapGet("/history", (ClaimsPrincipal user, TestService s, CancellationToken ct) => s.BookHistoryAsync(user.GetUserId(), ct));
        book.MapPost("/documents/{documentId:guid}", async (Guid documentId, CreateBookTestRequest body, ClaimsPrincipal user, BookTestService s, CancellationToken ct) =>
        {
            var created = await s.CreateAsync(user.GetUserId(), documentId, body, ct);
            return TypedResults.Created($"/api/book-tests/{created.Id}", created);
        });
        book.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal user, BookTestService s, CancellationToken ct) => s.GetAsync(user.GetUserId(), id, ct));
        book.MapGet("/{id:guid}/questions", (Guid id, ClaimsPrincipal user, BookTestService s, CancellationToken ct) => s.QuestionsAsync(user.GetUserId(), id, ct));
        book.MapPost("/{id:guid}/retry", (Guid id, ClaimsPrincipal user, BookTestService s, CancellationToken ct) => s.RetryAsync(user.GetUserId(), id, ct));
        book.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, BookTestService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(user.GetUserId(), id, ct);
            return TypedResults.NoContent();
        });
        book.MapPatch("/questions/{id:guid}", (Guid id, UpdateBookQuestionRequest body, ClaimsPrincipal user, BookTestService s, CancellationToken ct) =>
            s.UpdateQuestionAsync(user.GetUserId(), id, body, ct));
        return app;
    }
}

/// <summary>Reads queued book-test pages in the background (D-33). One page at a time; AI calls are slow.</summary>
public sealed class BookTestWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<BookTestWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = config.GetValue("BookTests:PollInterval", TimeSpan.FromSeconds(10));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<BookTestService>().ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Book test worker iteration failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
