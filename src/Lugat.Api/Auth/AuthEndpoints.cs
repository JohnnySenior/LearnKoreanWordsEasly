using System.Globalization;
using System.Security.Claims;
using Lugat.Application.Users;
using Lugat.Contracts;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Lugat.Api.Auth;

public sealed record RefreshRequest(string? RefreshToken);

public static class AuthEndpoints
{
    public const string TelegramIdClaim = "tg_id";
    public const string UsernameClaim = "tg_username";
    public const string UserIdClaim = "uid";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, IHostEnvironment env)
    {
        var auth = app.MapGroup("/api/auth").AllowAnonymous();

        // Telegram Login Widget and Google sign-in come in stage 1 (spec §6.3). The Mini App initData sign-in and
        // its allow-list were removed with the Mini App (spec §3.1).
        auth.MapPost("/refresh", Refresh);
        if (env.IsDevelopment())
        {
            auth.MapPost("/dev", DevSignIn);
        }

        app.MapGet("/api/me", (ClaimsPrincipal user) => new MeResponse(
                GetTelegramUserId(user),
                user.FindFirstValue(ClaimTypes.GivenName),
                user.FindFirstValue(UsernameClaim)))
            .RequireAuthorization();

        return app;
    }

    public static long GetTelegramUserId(ClaimsPrincipal user) =>
        long.Parse(user.FindFirstValue(TelegramIdClaim)!, CultureInfo.InvariantCulture);

    /// <summary>Internal user ID. Every owned query filters by this, never by an ID sent from the client.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(UserIdClaim) ?? throw new InvalidOperationException("Missing user id claim."));

    private static Results<SignInHttpResult, ProblemHttpResult> Refresh(
        RefreshRequest request,
        IOptionsMonitor<BearerTokenOptions> bearer,
        TimeProvider time)
    {
        var protector = bearer.Get(BearerTokenDefaults.AuthenticationScheme).RefreshTokenProtector;
        var ticket = string.IsNullOrEmpty(request.RefreshToken) ? null : protector.Unprotect(request.RefreshToken);

        if (ticket?.Properties.ExpiresUtc is not { } expires || time.GetUtcNow() >= expires)
        {
            return Problem(StatusCodes.Status401Unauthorized, "Sessiya muddati tugadi. Qaytadan kiring.");
        }

        return SignIn(ticket.Principal);
    }

    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> DevSignIn(
        IOptionsMonitor<AuthOptions> auth,
        UserService users,
        CancellationToken ct)
    {
        if (auth.CurrentValue.DevLoginUserId is not { } id)
        {
            return Problem(StatusCodes.Status403Forbidden, "Dev kirish sozlanmagan.");
        }

        var appUser = await users.EnsureAsync(id, "Dev", null, ct);
        return SignIn(CreatePrincipal(appUser.Id, id, "Dev", null));
    }

    /// <summary>The signed-in identity carried by the bearer token. Public so tests can sign in without a provider.</summary>
    public static ClaimsPrincipal CreatePrincipal(Guid userId, long telegramUserId, string? firstName, string? username)
    {
        var claims = new List<Claim>
        {
            new(UserIdClaim, userId.ToString()),
            new(TelegramIdClaim, telegramUserId.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.NameIdentifier, telegramUserId.ToString(CultureInfo.InvariantCulture)),
        };
        if (!string.IsNullOrEmpty(firstName))
        {
            claims.Add(new Claim(ClaimTypes.GivenName, firstName));
        }

        if (!string.IsNullOrEmpty(username))
        {
            claims.Add(new Claim(UsernameClaim, username));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, BearerTokenDefaults.AuthenticationScheme));
    }

    private static SignInHttpResult SignIn(ClaimsPrincipal principal) =>
        TypedResults.SignIn(principal, authenticationScheme: BearerTokenDefaults.AuthenticationScheme);

    private static ProblemHttpResult Problem(int status, string detail) =>
        TypedResults.Problem(detail: detail, statusCode: status);
}
