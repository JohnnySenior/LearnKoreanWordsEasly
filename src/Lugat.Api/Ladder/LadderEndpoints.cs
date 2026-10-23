using Lugat.Api.Premium;
using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Ladder;
using Lugat.Application.Listening;
using Lugat.Contracts;

namespace Lugat.Api.Ladder;

/// <summary>Sentence building: "Gap zinapoyasi", "Tuzoqlar daftari" (D-47), and listening (D-53).</summary>
public static class LadderEndpoints
{
    public static IEndpointRouteBuilder MapLadderEndpoints(this IEndpointRouteBuilder app, IConfiguration config)
    {
        var ladders = app.MapGroup("/api/ladders").RequireAuthorization().RequirePremium();
        ladders.MapGet("/", (ClaimsPrincipal user, LadderService s, CancellationToken ct) => s.ListAsync(user.GetUserId(), ct));
        ladders.MapPost("/", async (CreateLadderRequest body, ClaimsPrincipal user, LadderService s, CancellationToken ct) =>
        {
            var ladder = await s.CreateAsync(user.GetUserId(), body, ct);
            return Results.Created($"/api/ladders/{ladder.Id}", ladder);
        });
        ladders.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal user, LadderService s, CancellationToken ct) => s.GetAsync(user.GetUserId(), id, ct));
        ladders.MapPost("/{id:guid}/answers", (Guid id, LadderAnswerRequest body, ClaimsPrincipal user, LadderService s, CancellationToken ct) =>
            s.AnswerAsync(user.GetUserId(), id, body, ct));

        var traps = app.MapGroup("/api/traps").RequireAuthorization().RequirePremium();
        traps.MapGet("/", (ClaimsPrincipal user, LadderService s, CancellationToken ct) => s.TrapsAsync(user.GetUserId(), ct));
        traps.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, LadderService s, CancellationToken ct) =>
        {
            await s.DeleteTrapAsync(user.GetUserId(), id, ct);
            return Results.NoContent();
        });
        // Listening (D-53) comes in v1.1 (spec §3.1): the code is kept, but nothing is mapped until the feature is on.
        if (!config.GetValue("Features:Listening", false))
        {
            return app;
        }

        // The passage is heard through its own audio and shown only after the answer.
        var listening = app.MapGroup("/api/listening").RequireAuthorization().RequirePremium();
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

