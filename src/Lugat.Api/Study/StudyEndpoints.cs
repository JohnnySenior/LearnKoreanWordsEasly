using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Study;
using Lugat.Application.Vocabulary;
using Lugat.Contracts;
using Lugat.Domain.Learning;

namespace Lugat.Api.Study;

public static class StudyEndpoints
{
    public static IEndpointRouteBuilder MapStudyEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/today", (ClaimsPrincipal user, StudyService s, CancellationToken ct) => s.TodayAsync(user.GetUserId(), ct));

        // How words are used (D-46): prepared once per word by the AI, one batch of words per POST.
        api.MapGet("/vocabulary/usage", (ClaimsPrincipal user, UsageService s, CancellationToken ct) => s.SummaryAsync(user.GetUserId(), ct));
        api.MapGet("/vocabulary/units/{unitId:guid}/usage", (Guid unitId, ClaimsPrincipal user, UsageService s, CancellationToken ct) =>
            s.TableAsync(user.GetUserId(), unitId, ct));
        api.MapPost("/vocabulary/units/{unitId:guid}/usage", (Guid unitId, ClaimsPrincipal user, UsageService s, CancellationToken ct) =>
            s.PrepareTableAsync(user.GetUserId(), unitId, ct));
        // The same for a deck of own words (L-51): "umumiy" or a deck id.
        api.MapGet("/vocabulary/decks/{deck}/usage", (string deck, ClaimsPrincipal user, UsageService s, CancellationToken ct) =>
            s.DeckAsync(user.GetUserId(), deck, ct));
        api.MapPost("/vocabulary/decks/{deck}/usage", (string deck, ClaimsPrincipal user, UsageService s, CancellationToken ct) =>
            s.PrepareDeckAsync(user.GetUserId(), deck, ct));

        api.MapGet("/sessions/current", async (ClaimsPrincipal user, StudyService s, CancellationToken ct) =>
            await s.CurrentAsync(user.GetUserId(), ct) is { } session ? Results.Ok(session) : Results.NoContent());
        api.MapPost("/sessions", (StartSessionRequest body, ClaimsPrincipal user, StudyService s, CancellationToken ct) =>
            s.StartAsync(user.GetUserId(), body, ct));
        api.MapGet("/sessions/{id:guid}", (Guid id, ClaimsPrincipal user, StudyService s, CancellationToken ct) =>
            s.GetAsync(user.GetUserId(), id, ct));
        api.MapPost("/sessions/{id:guid}/answers", (Guid id, AnswerRequest body, ClaimsPrincipal user, StudyService s, CancellationToken ct) =>
            s.AnswerAsync(user.GetUserId(), id, body, ct));
        api.MapPost("/reviews/{eventId:guid}/undo", (Guid eventId, ClaimsPrincipal user, StudyService s, CancellationToken ct) =>
            s.UndoAsync(user.GetUserId(), eventId, ct));

        var writing = app.MapGroup("/api/writing").RequireAuthorization();
        writing.MapGet("/queue", (ReviewDirection? direction, int? count, Guid? unit, string? deck, ClaimsPrincipal user, Application.Writing.WritingService s, CancellationToken ct) =>
            s.QueueAsync(user.GetUserId(), direction, count ?? 20, unit, ct, deck));
        writing.MapPost("/answers", (WritingCheckRequest body, ClaimsPrincipal user, Application.Writing.WritingService s, CancellationToken ct) =>
            s.CheckAsync(user.GetUserId(), body, ct));
        writing.MapGet("/stats", (ClaimsPrincipal user, Application.Writing.WritingService s, CancellationToken ct) =>
            s.StatsAsync(user.GetUserId(), ct));

        return app;
    }
}
