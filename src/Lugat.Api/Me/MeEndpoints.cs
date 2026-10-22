using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Abstractions;
using Lugat.Application.Reminders;
using Lugat.Application.Study;
using Lugat.Application.Users;
using Lugat.Contracts;
using Lugat.Domain.Users;

namespace Lugat.Api.Me;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/me").RequireAuthorization();

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

        me.MapGet("/preferences", (ClaimsPrincipal user, UserService users, ITelegramMessenger telegram, CancellationToken ct) =>
            users.GetPreferencesAsync(user.GetUserId(), telegram.IsConfigured, ct));

        me.MapPatch("/preferences", (UpdatePreferencesRequest body, ClaimsPrincipal user, UserService users, ITelegramMessenger telegram, CancellationToken ct) =>
            users.UpdatePreferencesAsync(user.GetUserId(), body, telegram.IsConfigured, ct));

        me.MapPost("/reminders/test", async (ClaimsPrincipal user, ReminderService reminders, CancellationToken ct) =>
        {
            var result = await reminders.SendTestAsync(user.GetUserId(), ct);
            return result.Outcome switch
            {
                SendOutcome.Sent => new TestReminderResult(true, "Xabar yuborildi. Telegram'dagi botni tekshiring."),
                SendOutcome.Blocked => new TestReminderResult(false, "Bot sizga yoza olmadi. Botni oching va «Start» (/start) tugmasini bosing, keyin qayta urinib ko‘ring."),
                _ => new TestReminderResult(false, "Xabar yuborilmadi. Birozdan keyin qayta urinib ko‘ring."),
            };
        });

        // The mistakes notebook (D-41).
        app.MapGet("/api/mistakes", (ClaimsPrincipal user, Application.Mistakes.MistakeService s, CancellationToken ct) => s.GetAsync(user.GetUserId(), ct))
            .RequireAuthorization();

        // Weak points of the last 7 or 30 days (D-54).
        app.MapGet("/api/weak-points", (int? days, ClaimsPrincipal user, Application.Mistakes.WeakPointService s, CancellationToken ct) =>
                s.GetAsync(user.GetUserId(), days ?? 7, ct))
            .RequireAuthorization();

        app.MapGet("/api/progress", (ClaimsPrincipal user, ProgressService progress, CancellationToken ct) =>
                progress.GetAsync(user.GetUserId(), ct))
            .RequireAuthorization();

        return app;
    }
}
