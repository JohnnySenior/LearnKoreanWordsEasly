using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Games;
using Lugat.Contracts;

namespace Lugat.Api.Games;

/// <summary>Crosswords (L-55): free for every learner, no AI is involved.</summary>
public static class CrosswordEndpoints
{
    public static IEndpointRouteBuilder MapCrosswordEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/crosswords").RequireAuthorization();
        group.MapGet("/", (ClaimsPrincipal user, CrosswordService s, CancellationToken ct) => s.HomeAsync(user.GetUserId(), ct));
        group.MapPost("/", (StartCrosswordRequest body, ClaimsPrincipal user, CrosswordService s, CancellationToken ct) =>
            s.StartAsync(user.GetUserId(), body, ct));
        group.MapPost("/{id:guid}/answers", (Guid id, CrosswordAnswerRequest body, ClaimsPrincipal user, CrosswordService s, CancellationToken ct) =>
            s.AnswerAsync(user.GetUserId(), id, body, ct));
        group.MapPost("/{id:guid}/hints", (Guid id, CrosswordHintRequest body, ClaimsPrincipal user, CrosswordService s, CancellationToken ct) =>
            s.HintAsync(user.GetUserId(), id, body, ct));
        group.MapGet("/{id:guid}/words/{index:int}/audio", async (Guid id, int index, ClaimsPrincipal user, CrosswordService s, CancellationToken ct) =>
            Results.File(await s.AudioAsync(user.GetUserId(), id, index, ct), "audio/mpeg"));
        return app;
    }
}
