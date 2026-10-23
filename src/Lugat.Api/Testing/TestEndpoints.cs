using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Testing;
using Lugat.Contracts;

namespace Lugat.Api.Testing;

public static class TestEndpoints
{
    /// <summary>Starting tests: 20 an hour per user (spec §6.4).</summary>
    public const string StartRateLimitPolicy = "test-start";

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
        }).RequireRateLimiting(StartRateLimitPolicy);
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
        tests.MapGet("/{id:guid}/questions/{index:int}/audio", async (Guid id, int index, ClaimsPrincipal user, TestService s, CancellationToken ct) =>
            Results.File(await s.AudioAsync(user.GetUserId(), id, index, ct), "audio/mpeg"));

        // Unit tests (spec §4.4, §4.5, §11): the final test of an open unit, "Bilaman" for the next locked one.
        // Answers and finishing work like any test; finishing applies the result (pass, next unit) once.
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapPost("/units/{unitId:guid}/tests", (Guid unitId, StartUnitTestRequest body, ClaimsPrincipal user,
                Application.Catalog.UnitTestService s, CancellationToken ct) => s.StartAsync(user.GetUserId(), unitId, body, ct))
            .RequireRateLimiting(StartRateLimitPolicy);
        api.MapPost("/unit-tests/{id:guid}/answers", (Guid id, TestAnswerRequest body, ClaimsPrincipal user, TestService s, CancellationToken ct) =>
            s.AnswerAsync(user.GetUserId(), id, body, ct));
        api.MapPost("/unit-tests/{id:guid}/finish", (Guid id, ClaimsPrincipal user, TestService s, CancellationToken ct) => s.FinishAsync(user.GetUserId(), id, ct));

        return app;
    }
}
