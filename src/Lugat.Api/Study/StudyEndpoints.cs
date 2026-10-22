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
        api.MapGet("/vocabulary/tables", (ClaimsPrincipal user, StudyService s, CancellationToken ct) => s.TablesAsync(user.GetUserId(), ct));
        api.MapGet("/vocabulary/tables/{table:int}", (int table, ClaimsPrincipal user, StudyService s, CancellationToken ct) =>
            s.TableAsync(user.GetUserId(), table, ct));

        // How words are used (D-46): prepared once per word by the AI, one batch of words per POST.
        api.MapGet("/vocabulary/usage", (ClaimsPrincipal user, UsageService s, CancellationToken ct) => s.SummaryAsync(user.GetUserId(), ct));
        api.MapGet("/vocabulary/tables/{table:int}/usage", (int table, ClaimsPrincipal user, UsageService s, CancellationToken ct) =>
            s.TableAsync(user.GetUserId(), table, ct));
        api.MapPost("/vocabulary/tables/{table:int}/usage", (int table, ClaimsPrincipal user, UsageService s, CancellationToken ct) =>
            s.PrepareTableAsync(user.GetUserId(), table, ct));

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
        writing.MapGet("/queue", (ReviewDirection? direction, int? count, int? table, ClaimsPrincipal user, Application.Writing.WritingService s, CancellationToken ct) =>
            s.QueueAsync(user.GetUserId(), direction, count ?? 20, table, ct));
        writing.MapPost("/answers", (WritingCheckRequest body, ClaimsPrincipal user, Application.Writing.WritingService s, CancellationToken ct) =>
            s.CheckAsync(user.GetUserId(), body, ct));
        writing.MapGet("/stats", (ClaimsPrincipal user, Application.Writing.WritingService s, CancellationToken ct) =>
            s.StatsAsync(user.GetUserId(), ct));

        return app;
    }
}
