using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application;
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
        // The teacher's vocabulary lists for a group (L-67). Filling with the AI and publishing need the paid plan.
        teacher.MapGet("/groups/{id:guid}/decks", (Guid id, ClaimsPrincipal user, GroupDeckService s, CancellationToken ct) => s.ListAsync(user.GetUserId(), id, ct));
        teacher.MapPost("/groups/{id:guid}/decks", (Guid id, CreateGroupDeckRequest body, ClaimsPrincipal user, GroupDeckService s, CancellationToken ct) =>
            s.CreateAsync(user.GetUserId(), id, body, ct));
        teacher.MapGet("/decks/{id:guid}", (Guid id, ClaimsPrincipal user, GroupDeckService s, CancellationToken ct) => s.GetAsync(user.GetUserId(), id, ct));
        teacher.MapPut("/decks/{id:guid}", (Guid id, RenameGroupDeckRequest body, ClaimsPrincipal user, GroupDeckService s, CancellationToken ct) =>
            s.RenameAsync(user.GetUserId(), id, body, ct));
        teacher.MapPost("/decks/{id:guid}/import", async (Guid id, IFormFile? file, ClaimsPrincipal user, GroupDeckService s, CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                throw new ValidationException("Fayl tanlanmagan.");
            }

            if (file.Length > GroupDeckService.MaxFileBytes)
            {
                throw new PayloadTooLargeException("Fayl juda katta (2 MB gacha).");
            }

            await using var stream = file.OpenReadStream();
            return await s.ImportAsync(user.GetUserId(), id, stream, file.FileName, ct);
        }).DisableAntiforgery(); // bearer tokens, no cookies: nothing to forge
        teacher.MapPost("/decks/{id:guid}/words", (Guid id, AddGroupWordRequest body, ClaimsPrincipal user, GroupDeckService s, CancellationToken ct) =>
            s.AddWordAsync(user.GetUserId(), id, body, ct));
        teacher.MapPut("/decks/{id:guid}/words/{wordId:guid}", (Guid id, Guid wordId, UpdateGroupWordRequest body, ClaimsPrincipal user, GroupDeckService s, CancellationToken ct) =>
            s.UpdateWordAsync(user.GetUserId(), id, wordId, body, ct));
        teacher.MapDelete("/decks/{id:guid}/words/{wordId:guid}", (Guid id, Guid wordId, ClaimsPrincipal user, GroupDeckService s, CancellationToken ct) =>
            s.RemoveWordAsync(user.GetUserId(), id, wordId, ct));
        teacher.MapPost("/decks/{id:guid}/fill", (Guid id, ClaimsPrincipal user, GroupDeckService s, CancellationToken ct) => s.FillAsync(user.GetUserId(), id, ct));
        teacher.MapPost("/decks/{id:guid}/publish", (Guid id, ClaimsPrincipal user, GroupDeckService s, CancellationToken ct) => s.PublishAsync(user.GetUserId(), id, ct));
        teacher.MapDelete("/decks/{id:guid}", async (Guid id, ClaimsPrincipal user, GroupDeckService s, CancellationToken ct) =>
        {
            await s.ArchiveAsync(user.GetUserId(), id, ct);
            return Results.NoContent();
        });
        teacher.MapGet("/students/{id:guid}", (Guid id, ClaimsPrincipal user, TeacherService s, CancellationToken ct) => s.StudentAsync(user.GetUserId(), id, ct));
        teacher.MapGet("/commission", (ClaimsPrincipal user, TeacherService s, CancellationToken ct) => s.CommissionAsync(user.GetUserId(), ct));
        return app;
    }
}
