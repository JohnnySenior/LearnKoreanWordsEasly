using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Abstractions;
using Lugat.Application.Reminders;
using Lugat.Application.Study;
using Lugat.Application.Users;
using Lugat.Contracts;

namespace Lugat.Api.Me;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/me").RequireAuthorization();

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
