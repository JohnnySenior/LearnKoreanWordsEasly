using System.Security.Claims;
using KoreanAiTeacher.Api.Auth;
using KoreanAiTeacher.Application.Lessons;
using KoreanAiTeacher.Application.Study;
using KoreanAiTeacher.Application.Vocabulary;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Domain.Learning;

namespace KoreanAiTeacher.Api.Study;

public static class StudyEndpoints
{
    public static IEndpointRouteBuilder MapStudyEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/today", (ClaimsPrincipal user, StudyService s, CancellationToken ct) => s.TodayAsync(user.GetUserId(), ct));
        api.MapGet("/vocabulary/tables", (ClaimsPrincipal user, StudyService s, CancellationToken ct) => s.TablesAsync(user.GetUserId(), ct));
        api.MapGet("/vocabulary/tables/{documentId:guid}/{table:int}", (Guid documentId, int table, ClaimsPrincipal user, StudyService s, CancellationToken ct) =>
            s.TableAsync(user.GetUserId(), documentId, table, ct));

        // How words are used (D-46): prepared once per word by the AI, one batch of words per POST.
        api.MapGet("/vocabulary/usage", (ClaimsPrincipal user, UsageService s, CancellationToken ct) => s.SummaryAsync(user.GetUserId(), ct));
        api.MapGet("/vocabulary/tables/{documentId:guid}/{table:int}/usage", (Guid documentId, int table, ClaimsPrincipal user, UsageService s, CancellationToken ct) =>
            s.TableAsync(user.GetUserId(), documentId, table, ct));
        api.MapPost("/vocabulary/tables/{documentId:guid}/{table:int}/usage", (Guid documentId, int table, ClaimsPrincipal user, UsageService s, CancellationToken ct) =>
            s.PrepareTableAsync(user.GetUserId(), documentId, table, ct));

        api.MapGet("/assignments", (ClaimsPrincipal user, LessonService s, CancellationToken ct) => s.ListAsync(user.GetUserId(), ct));
        api.MapGet("/assignments/suggestion", async (ClaimsPrincipal user, LessonService s, CancellationToken ct) =>
            await s.SuggestAsync(user.GetUserId(), ct) is { } suggestion ? Results.Ok(suggestion) : Results.NoContent());
        api.MapPost("/assignments", async (CreateAssignmentRequest body, ClaimsPrincipal user, LessonService s, CancellationToken ct) =>
        {
            var created = await s.CreateAsync(user.GetUserId(), body, ct);
            return TypedResults.Created($"/api/assignments/{created.Id}", created);
        });
        api.MapPatch("/assignments/{id:guid}", (Guid id, UpdateAssignmentRequest body, ClaimsPrincipal user, LessonService s, CancellationToken ct) =>
            s.UpdateAsync(user.GetUserId(), id, body, ct));

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
        writing.MapGet("/queue", (ReviewDirection? direction, int? count, Guid? assignmentId, ClaimsPrincipal user, Application.Writing.WritingService s, CancellationToken ct) =>
            s.QueueAsync(user.GetUserId(), direction, count ?? 20, assignmentId, ct));
        writing.MapPost("/answers", (WritingCheckRequest body, ClaimsPrincipal user, Application.Writing.WritingService s, CancellationToken ct) =>
            s.CheckAsync(user.GetUserId(), body, ct));
        writing.MapGet("/stats", (ClaimsPrincipal user, Application.Writing.WritingService s, CancellationToken ct) =>
            s.StatsAsync(user.GetUserId(), ct));

        return app;
    }
}
