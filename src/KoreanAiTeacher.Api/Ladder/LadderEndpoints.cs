using System.Security.Claims;
using KoreanAiTeacher.Api.Auth;
using KoreanAiTeacher.Application.Essays;
using KoreanAiTeacher.Application.Ladder;
using KoreanAiTeacher.Application.Listening;
using KoreanAiTeacher.Contracts;

namespace KoreanAiTeacher.Api.Ladder;

/// <summary>Sentence building and writing: "Gap zinapoyasi", "Tuzoqlar daftari" (D-47), TOPIK writing (D-49) and listening (D-53).</summary>
public static class LadderEndpoints
{
    public static IEndpointRouteBuilder MapLadderEndpoints(this IEndpointRouteBuilder app)
    {
        var ladders = app.MapGroup("/api/ladders").RequireAuthorization();
        ladders.MapGet("/", (ClaimsPrincipal user, LadderService s, CancellationToken ct) => s.ListAsync(user.GetUserId(), ct));
        ladders.MapPost("/", async (CreateLadderRequest body, ClaimsPrincipal user, LadderService s, CancellationToken ct) =>
        {
            var ladder = await s.CreateAsync(user.GetUserId(), body, ct);
            return Results.Created($"/api/ladders/{ladder.Id}", ladder);
        });
        ladders.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal user, LadderService s, CancellationToken ct) => s.GetAsync(user.GetUserId(), id, ct));
        ladders.MapPost("/{id:guid}/answers", (Guid id, LadderAnswerRequest body, ClaimsPrincipal user, LadderService s, CancellationToken ct) =>
            s.AnswerAsync(user.GetUserId(), id, body, ct));

        var traps = app.MapGroup("/api/traps").RequireAuthorization();
        traps.MapGet("/", (ClaimsPrincipal user, LadderService s, CancellationToken ct) => s.TrapsAsync(user.GetUserId(), ct));
        traps.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, LadderService s, CancellationToken ct) =>
        {
            await s.DeleteTrapAsync(user.GetUserId(), id, ct);
            return Results.NoContent();
        });
        // TOPIK II writing 51–54 (D-49).
        var essays = app.MapGroup("/api/essays").RequireAuthorization();
        essays.MapGet("/", (ClaimsPrincipal user, EssayService s, CancellationToken ct) => s.ListAsync(user.GetUserId(), ct));
        essays.MapPost("/", async (CreateEssayRequest body, ClaimsPrincipal user, EssayService s, CancellationToken ct) =>
        {
            var essay = await s.CreateAsync(user.GetUserId(), body, ct);
            return Results.Created($"/api/essays/{essay.Id}", essay);
        });
        essays.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal user, EssayService s, CancellationToken ct) => s.GetAsync(user.GetUserId(), id, ct));
        essays.MapPut("/{id:guid}/draft", (Guid id, SaveEssayDraftRequest body, ClaimsPrincipal user, EssayService s, CancellationToken ct) =>
            s.SaveDraftAsync(user.GetUserId(), id, body, ct));
        essays.MapPost("/{id:guid}/submit", (Guid id, SubmitEssayRequest body, ClaimsPrincipal user, EssayService s, CancellationToken ct) =>
            s.SubmitAsync(user.GetUserId(), id, body, ct));
        essays.MapPost("/{id:guid}/retry", (Guid id, RetryEssayRequest body, ClaimsPrincipal user, EssayService s, CancellationToken ct) =>
            s.RetryAsync(user.GetUserId(), id, body, ct));

        // TOPIK II listening (D-53): the passage is heard through its own audio and shown only after the answer.
        var listening = app.MapGroup("/api/listening").RequireAuthorization();
        listening.MapGet("/", (ClaimsPrincipal user, ListeningService s, CancellationToken ct) => s.ListAsync(user.GetUserId(), ct));
        listening.MapPost("/", async (CreateListeningRequest body, ClaimsPrincipal user, ListeningService s, CancellationToken ct) =>
        {
            var set = await s.CreateAsync(user.GetUserId(), body, ct);
            return Results.Created($"/api/listening/{set.Id}", set);
        });
        listening.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal user, ListeningService s, CancellationToken ct) => s.GetAsync(user.GetUserId(), id, ct));
        listening.MapGet("/{id:guid}/items/{index:int}/audio", async (Guid id, int index, ClaimsPrincipal user, ListeningService s, HttpContext http, CancellationToken ct) =>
        {
            var audio = await s.AudioAsync(user.GetUserId(), id, index, ct);
            http.Response.Headers.CacheControl = "private, max-age=86400";
            return Results.File(audio, "audio/mpeg");
        });
        listening.MapPost("/{id:guid}/answers", (Guid id, ListeningAnswerRequest body, ClaimsPrincipal user, ListeningService s, CancellationToken ct) =>
            s.AnswerAsync(user.GetUserId(), id, body, ct));
        return app;
    }
}

