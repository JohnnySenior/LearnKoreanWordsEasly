using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Testing;
using Lugat.Contracts;

namespace Lugat.Api.Testing;

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

        return app;
    }
}
