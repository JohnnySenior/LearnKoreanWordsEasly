using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Premium;
using Lugat.Contracts;

namespace Lugat.Api.Premium;

public static class PremiumEndpoints
{
    /// <summary>
    /// Premium features (spec §7.1) answer 403 with type "premium-required" without Premium (spec §11); the check is
    /// the server's, never the client's (spec §0.6).
    /// </summary>
    public static TBuilder RequirePremium<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            await http.RequestServices.GetRequiredService<PremiumService>().RequireAsync(http.User.GetUserId(), http.RequestAborted);
            return await next(context);
        });

    public static IEndpointRouteBuilder MapPremiumEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/me/premium", (ClaimsPrincipal user, PremiumService s, CancellationToken ct) => s.StatusAsync(user.GetUserId(), ct));
        api.MapGet("/premium/plans", (PremiumService s) => s.Plans());
        api.MapPost("/premium/promo/check", (PromoCheckRequest body, PremiumService s, CancellationToken ct) => s.CheckPromoAsync(body, ct))
            .RequireRateLimiting(AuthEndpoints.RateLimitPolicy); // no guessing codes
        // L-64: the earned trial after the first unit, and which locked feature brought the learner here.
        api.MapGet("/premium/trial", (ClaimsPrincipal user, PremiumService s, CancellationToken ct) => s.TrialOfferAsync(user.GetUserId(), ct));
        api.MapPost("/premium/trial", (ClaimsPrincipal user, PremiumService s, CancellationToken ct) => s.StartTrialAsync(user.GetUserId(), ct))
            .RequireRateLimiting(AuthEndpoints.RateLimitPolicy);
        api.MapPost("/premium/views", async (PremiumViewRequest body, PremiumService s, CancellationToken ct) =>
        {
            await s.CountViewAsync(body.Feature, ct);
            return Results.NoContent();
        });

        // The owner's manual payment flow (spec §7.3, §10).
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Policies.Admin);
        admin.MapGet("/users", (string? q, PremiumService s, CancellationToken ct) => s.SearchUsersAsync(q, ct));
        admin.MapGet("/payments", (Guid? userId, PremiumService s, CancellationToken ct) => s.PaymentsAsync(userId, ct));
        admin.MapPost("/payments", (RecordPaymentRequest body, ClaimsPrincipal user, PremiumService s, CancellationToken ct) =>
            s.RecordPaymentAsync(user.GetUserId(), body, ct));
        admin.MapPost("/payments/{id:guid}/refund", (Guid id, RefundPaymentRequest body, PremiumService s, CancellationToken ct) =>
            s.RefundAsync(id, body, ct));
        admin.MapGet("/promo-codes", (PremiumService s, CancellationToken ct) => s.PromoCodesAsync(ct));
        admin.MapPost("/promo-codes", (CreatePromoCodeRequest body, PremiumService s, CancellationToken ct) => s.CreatePromoAsync(body, ct));
        return app;
    }
}
