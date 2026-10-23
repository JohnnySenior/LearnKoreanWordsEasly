using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Admin;
using Lugat.Contracts;

namespace Lugat.Api.Admin;

public static class AdminEndpoints
{
    /// <summary>The admin panel (spec §10): admins only. Editors reach only the content part (CatalogEndpoints).</summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Policies.Admin);
        admin.MapGet("/users/{id:guid}", (Guid id, AdminService s, CancellationToken ct) => s.UserAsync(id, ct));
        admin.MapPut("/users/{id:guid}/roles", (Guid id, SetRolesRequest body, ClaimsPrincipal user, AdminService s, CancellationToken ct) =>
            s.SetRolesAsync(user.GetUserId(), id, body, ct));
        admin.MapPost("/users/{id:guid}/premium", (Guid id, AdjustPremiumRequest body, ClaimsPrincipal user, AdminService s, CancellationToken ct) =>
            s.AdjustPremiumAsync(user.GetUserId(), id, body, ct));
        admin.MapPut("/promo-codes/{id:guid}/active", async (Guid id, SetPromoActiveRequest body, ClaimsPrincipal user, AdminService s, CancellationToken ct) =>
        {
            await s.SetPromoActiveAsync(user.GetUserId(), id, body.IsActive, ct);
            return Results.NoContent();
        });
        admin.MapGet("/commissions", (AdminService s, CancellationToken ct) => s.CommissionsAsync(ct));
        admin.MapPost("/commissions/paid", (MarkCommissionPaidRequest body, ClaimsPrincipal user, AdminService s, CancellationToken ct) =>
            s.MarkPaidAsync(user.GetUserId(), body, ct));
        admin.MapGet("/usage", (int? days, AdminService s, CancellationToken ct) => s.UsageAsync(days ?? 30, ct));
        admin.MapGet("/metrics", (AdminService s, CancellationToken ct) => s.MetricsAsync(ct));
        return app;
    }
}
