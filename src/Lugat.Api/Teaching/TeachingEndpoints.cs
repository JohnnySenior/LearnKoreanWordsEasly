using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Teaching;
using Lugat.Contracts;

namespace Lugat.Api.Teaching;

public static class TeachingEndpoints
{
    public const string CodeRateLimitPolicy = "group-codes";

    public static IEndpointRouteBuilder MapTeachingEndpoints(this IEndpointRouteBuilder app)
    {
        // The learner's side (spec §9.1, §11). Invite codes are not guessable; the per-user limit keeps it that way.
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/groups/invite/{code}", (string code, ClaimsPrincipal user, GroupService s, CancellationToken ct) => s.InviteAsync(user.GetUserId(), code, ct))
            .RequireRateLimiting(CodeRateLimitPolicy);
        api.MapPost("/groups/join/{code}", (string code, ClaimsPrincipal user, GroupService s, CancellationToken ct) => s.JoinAsync(user.GetUserId(), code, ct))
            .RequireRateLimiting(CodeRateLimitPolicy);
        api.MapDelete("/groups/{id:guid}/membership", async (Guid id, ClaimsPrincipal user, GroupService s, CancellationToken ct) =>
        {
            await s.LeaveAsync(user.GetUserId(), id, ct);
            return Results.NoContent();
        });
        api.MapGet("/me/groups", (ClaimsPrincipal user, GroupService s, CancellationToken ct) => s.MyGroupsAsync(user.GetUserId(), ct));
        api.MapPost("/me/referral", async (ReferralRequest body, ClaimsPrincipal user, GroupService s, CancellationToken ct) =>
            new { PromoCode = await s.ReferAsync(user.GetUserId(), body.Code, ct) })
            .RequireRateLimiting(CodeRateLimitPolicy);

        // The teacher panel (spec §9.2, §9.3): only the Teacher role, and only the teacher's own groups.
        var teacher = app.MapGroup("/api/teacher").RequireAuthorization(Policies.Teacher);
        teacher.MapGet("/groups", (ClaimsPrincipal user, TeacherService s, CancellationToken ct) => s.GroupsAsync(user.GetUserId(), ct));
        teacher.MapPost("/groups", (CreateGroupRequest body, ClaimsPrincipal user, TeacherService s, CancellationToken ct) => s.CreateGroupAsync(user.GetUserId(), body, ct));
        teacher.MapGet("/groups/{id:guid}", (Guid id, ClaimsPrincipal user, TeacherService s, CancellationToken ct) => s.GroupAsync(user.GetUserId(), id, ct));
        teacher.MapPost("/groups/{id:guid}/invite", (Guid id, ClaimsPrincipal user, TeacherService s, CancellationToken ct) => s.NewInviteAsync(user.GetUserId(), id, ct));
        teacher.MapDelete("/groups/{id:guid}", async (Guid id, ClaimsPrincipal user, TeacherService s, CancellationToken ct) =>
        {
            await s.CloseGroupAsync(user.GetUserId(), id, ct);
            return Results.NoContent();
        });
        teacher.MapGet("/students/{id:guid}", (Guid id, ClaimsPrincipal user, TeacherService s, CancellationToken ct) => s.StudentAsync(user.GetUserId(), id, ct));
        teacher.MapGet("/commission", (ClaimsPrincipal user, TeacherService s, CancellationToken ct) => s.CommissionAsync(user.GetUserId(), ct));
        return app;
    }
}
