using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Catalog;
using Lugat.Contracts;

namespace Lugat.Api.Catalog;

public static class IntroductionEndpoints
{
    public static IEndpointRouteBuilder MapIntroductionEndpoints(this IEndpointRouteBuilder app)
    {
        var learner = app.MapGroup("/api").RequireAuthorization();
        learner.MapGet("/units/{unitId:guid}/introduction", async (Guid unitId, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            Results.Ok(new IntroductionAvailabilityDto(await lessons.PublishedAsync(user.GetUserId(), unitId, ct))));
        learner.MapGet("/introductions/{id:guid}/audio/{word:int}/{beat:int}", async (Guid id, int word, int beat, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            Results.File(await lessons.AudioAsync(user.GetUserId(), id, word, beat, 0, false, ct), "audio/mpeg", enableRangeProcessing: true));
        learner.MapGet("/introductions/{id:guid}/audio/{word:int}/{beat:int}/{part:int}", async (Guid id, int word, int beat, int part, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            Results.File(await lessons.AudioAsync(user.GetUserId(), id, word, beat, part, false, ct), "audio/mpeg", enableRangeProcessing: true));
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Policies.Admin);
        admin.MapGet("/units/{unitId:guid}/introductions", (Guid unitId, IntroductionService lessons, CancellationToken ct) => lessons.ListAsync(unitId, ct));
        admin.MapPost("/units/{unitId:guid}/introductions", (Guid unitId, CreateIntroductionRequest body, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            lessons.CreateAsync(user.GetUserId(), unitId, body.Id, ct));
        admin.MapGet("/introductions/{id:guid}", (Guid id, IntroductionService lessons, CancellationToken ct) => lessons.PreviewAsync(id, ct));
        admin.MapPost("/introductions/{id:guid}/draft-next", (Guid id, IntroductionVersionRequest body, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            lessons.DraftNextAsync(user.GetUserId(), id, body.Version, ct));
        admin.MapPut("/introductions/{id:guid}/words/{wordId:guid}", (Guid id, Guid wordId, UpdateIntroductionWordRequest body, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            lessons.UpdateAsync(user.GetUserId(), id, wordId, body, ct));
        admin.MapPost("/introductions/{id:guid}/approve", (Guid id, IntroductionVersionRequest body, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            lessons.ApproveAsync(user.GetUserId(), id, body.Version, ct));
        admin.MapPost("/introductions/{id:guid}/return-to-draft", (Guid id, IntroductionVersionRequest body, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            lessons.ReturnToDraftAsync(user.GetUserId(), id, body.Version, ct));
        admin.MapPost("/introductions/{id:guid}/audio-next", (Guid id, IntroductionVersionRequest body, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            lessons.AudioNextAsync(user.GetUserId(), id, body.Version, ct));
        admin.MapPost("/introductions/{id:guid}/publish", (Guid id, PublishIntroductionRequest body, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            lessons.PublishAsync(user.GetUserId(), id, body, ct));
        admin.MapPost("/introductions/{id:guid}/discard", (Guid id, IntroductionVersionRequest body, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            lessons.DiscardAsync(user.GetUserId(), id, body.Version, ct));
        admin.MapGet("/introductions/{id:guid}/audio/{word:int}/{beat:int}", async (Guid id, int word, int beat, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            Results.File(await lessons.AudioAsync(user.GetUserId(), id, word, beat, 0, true, ct), "audio/mpeg", enableRangeProcessing: true));
        admin.MapGet("/introductions/{id:guid}/audio/{word:int}/{beat:int}/{part:int}", async (Guid id, int word, int beat, int part, ClaimsPrincipal user, IntroductionService lessons, CancellationToken ct) =>
            Results.File(await lessons.AudioAsync(user.GetUserId(), id, word, beat, part, true, ct), "audio/mpeg", enableRangeProcessing: true));
        return app;
    }
}
