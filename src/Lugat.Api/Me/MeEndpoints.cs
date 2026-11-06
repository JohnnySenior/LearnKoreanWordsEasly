using Lugat.Api.Premium;
using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Abstractions;
using Lugat.Application.Reminders;
using Lugat.Application.Study;
using Lugat.Application.Users;
using Lugat.Contracts;
using Lugat.Domain.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lugat.Api.Me;

public static class MeEndpoints
{
    public const string VisitRateLimitPolicy = "visits";

    public const string PushRateLimitPolicy = "push";

    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/me").RequireAuthorization();

        // Ad channel links (L-40): a visit is counted before sign-in, the channel is kept on a new account after it.
        app.MapPost("/api/visits", async (SourceRequest body, SourceService s, CancellationToken ct) =>
        {
            await s.VisitAsync(body.Source, ct);
            return TypedResults.NoContent();
        }).AllowAnonymous().RequireRateLimiting(VisitRateLimitPolicy);
        me.MapPost("/source", async (SourceRequest body, ClaimsPrincipal user, SourceService s, CancellationToken ct) =>
        {
            await s.AttachAsync(user.GetUserId(), body.Source, ct);
            return TypedResults.NoContent();
        });

        // The account (spec §11): who I am and how I sign in, my settings, deleting the account.
        me.MapGet("/", (ClaimsPrincipal user, UserService users, CancellationToken ct) => users.GetMeAsync(user.GetUserId(), ct));
        me.MapPatch("/", (UpdateMeRequest body, ClaimsPrincipal user, UserService users, CancellationToken ct) =>
            users.UpdateMeAsync(user.GetUserId(), body, ct));
        me.MapDelete("/", async (ClaimsPrincipal user, UserService users, CancellationToken ct) =>
        {
            await users.DeleteAsync(user.GetUserId(), ct);
            return TypedResults.NoContent();
        });

        // "Hisobni bog‘lash": a second way to sign in, proven the same way as a sign-in.
        me.MapPost("/logins", async (LinkLoginRequest body, ClaimsPrincipal user, UserService users, TelegramLoginValidator telegram,
            GoogleIdTokenValidator google, ILogger<LinkLoginRequest> logger, CancellationToken ct) =>
        {
            var (login, problem) = body.Provider switch
            {
                LoginProvider.Telegram => AuthEndpoints.Verify(telegram, body.Telegram, logger),
                LoginProvider.Google => await AuthEndpoints.VerifyAsync(google, body.GoogleIdToken, ct),
                _ => (null, TypedResults.Problem(detail: "Noma’lum kirish usuli.", statusCode: StatusCodes.Status422UnprocessableEntity)),
            };
            return login is null ? (IResult)problem! : TypedResults.Ok(await users.LinkAsync(user.GetUserId(), login, ct));
        }).RequireRateLimiting(AuthEndpoints.RateLimitPolicy);

        me.MapGet("/preferences", (ClaimsPrincipal user, UserService users, ITelegramMessenger telegram, IWebPushSender push, CancellationToken ct) =>
            users.GetPreferencesAsync(user.GetUserId(), telegram.IsConfigured, ct, push.IsConfigured));

        me.MapPatch("/preferences", (UpdatePreferencesRequest body, ClaimsPrincipal user, UserService users, ITelegramMessenger telegram, IWebPushSender push, CancellationToken ct) =>
            users.UpdatePreferencesAsync(user.GetUserId(), body, telegram.IsConfigured, ct, push.IsConfigured));

        me.MapPost("/reminders/test", async (ClaimsPrincipal user, ReminderService reminders, CancellationToken ct) =>
        {
            var (result, channel) = await reminders.SendTestAsync(user.GetUserId(), ct);
            return (result.Outcome, channel) switch
            {
                (SendOutcome.Sent, ReminderChannel.Push) => new TestReminderResult(true, "Bildirishnoma yuborildi. Qurilmangizni tekshiring."),
                (SendOutcome.Sent, _) => new TestReminderResult(true, "Xabar yuborildi. Telegram'dagi botni tekshiring."),
                (SendOutcome.Blocked, _) => new TestReminderResult(false, "Bot sizga yoza olmadi. Botni oching va «Start» (/start) tugmasini bosing, keyin qayta urinib ko‘ring."),
                (_, ReminderChannel.None) => new TestReminderResult(false, "Xabar yuborilmadi: Telegram bog‘lanmagan yoki qurilma bildirishnomani qabul qilmadi."),
                _ => new TestReminderResult(false, "Xabar yuborilmadi. Birozdan keyin qayta urinib ko‘ring."),
            };
        });

        // Web Push (L-63): the key the browser subscribes with, and this device's subscription.
        me.MapGet("/push/key", Results<Ok<PushKeyDto>, ProblemHttpResult> (IWebPushSender push) => push.PublicKey is { } key
            ? TypedResults.Ok(new PushKeyDto(key))
            : TypedResults.Problem(detail: "Bildirishnomalar hozircha serverda yoqilmagan.", statusCode: StatusCodes.Status503ServiceUnavailable));
        me.MapPost("/push", async (PushSubscribeRequest body, ClaimsPrincipal user, HttpRequest request, PushSubscriptionService devices, CancellationToken ct) =>
        {
            await devices.SubscribeAsync(user.GetUserId(), body, request.Headers.UserAgent.ToString(), ct);
            return TypedResults.NoContent();
        }).RequireRateLimiting(PushRateLimitPolicy);
        me.MapDelete("/push", async ([Microsoft.AspNetCore.Mvc.FromBody] PushUnsubscribeRequest body, ClaimsPrincipal user, PushSubscriptionService devices, CancellationToken ct) =>
        {
            await devices.UnsubscribeAsync(user.GetUserId(), body.Endpoint, ct);
            return TypedResults.NoContent();
        }).RequireRateLimiting(PushRateLimitPolicy);

        // L-64: only the number, for the Premium page a free learner sees; the notebook itself stays Premium.
        app.MapGet("/api/mistakes/count", async (ClaimsPrincipal user, Application.Mistakes.MistakeService s, CancellationToken ct) =>
                new MistakeCountDto((await s.GetAsync(user.GetUserId(), ct)).Words.Count, Application.Mistakes.MistakeService.Days))
            .RequireAuthorization();

        // The mistakes notebook (D-41).
        app.MapGet("/api/mistakes", (ClaimsPrincipal user, Application.Mistakes.MistakeService s, CancellationToken ct) => s.GetAsync(user.GetUserId(), ct))
            .RequireAuthorization().RequirePremium();

        // Weak points of the last 7 or 30 days (D-54).
        app.MapGet("/api/weak-points", (int? days, ClaimsPrincipal user, Application.Mistakes.WeakPointService s, CancellationToken ct) =>
                s.GetAsync(user.GetUserId(), days ?? 7, ct))
            .RequireAuthorization().RequirePremium();

        app.MapGet("/api/progress", (ClaimsPrincipal user, ProgressService progress, CancellationToken ct) =>
                progress.GetAsync(user.GetUserId(), ct))
            .RequireAuthorization();

        return app;
    }
}
