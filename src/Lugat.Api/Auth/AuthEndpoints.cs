using System.Globalization;
using System.Security.Claims;
using Lugat.Application.Users;
using Lugat.Contracts;
using Lugat.Domain.Users;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Lugat.Api.Auth;

public sealed record RefreshRequest(string? RefreshToken);

public static class AuthEndpoints
{
    public const string UserIdClaim = "uid";

    /// <summary>The user's <see cref="User.SessionVersion"/> when the token was issued (L-13).</summary>
    public const string SessionVersionClaim = "sv";

    /// <summary>Rate limit policy for sign-in endpoints: 10 a minute per IP (spec §6.3, §6.4).</summary>
    public const string RateLimitPolicy = "auth";

    public const string RefreshRateLimitPolicy = "auth-refresh";

    /// <summary>"123456:ABC…" → 123456; null when the token does not start with a number.</summary>
    internal static long? BotId(string token) =>
        long.TryParse(token.AsSpan(0, Math.Max(0, token.IndexOf(':'))), out var id) && id > 0 ? id : null;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, IHostEnvironment env)
    {
        var auth = app.MapGroup("/api/auth").AllowAnonymous();

        auth.MapGet("/config", (IOptionsMonitor<TelegramLoginOptions> telegram, IOptionsMonitor<GoogleOptions> google) => new AuthConfigDto(
            telegram.CurrentValue is { BotToken.Length: > 0, BotUsername.Length: > 0 } t ? t.BotUsername : null,
            google.CurrentValue.ClientId is { Length: > 0 } id ? id : null,
            env.IsDevelopment(),
            telegram.CurrentValue is { BotToken.Length: > 0, BotUsername.Length: > 0 } bot ? BotId(bot.BotToken) : null));

        auth.MapPost("/telegram-login", SignInWithTelegram).RequireRateLimiting(RateLimitPolicy);
        auth.MapPost("/google", SignInWithGoogle).RequireRateLimiting(RateLimitPolicy);
        auth.MapPost("/refresh", Refresh).RequireRateLimiting(RefreshRateLimitPolicy);
        if (env.IsDevelopment())
        {
            auth.MapPost("/dev", DevSignIn).RequireRateLimiting(RateLimitPolicy);
        }

        // Signs out on every device: all tokens issued so far stop working (L-13).
        app.MapPost("/api/auth/logout", async (ClaimsPrincipal user, UserService users, CancellationToken ct) =>
        {
            await users.LogoutAsync(user.GetUserId(), ct);
            return TypedResults.NoContent();
        }).RequireAuthorization();

        return app;
    }

    /// <summary>Internal user ID. Every owned query filters by this, never by an ID sent from the client.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(UserIdClaim) ?? throw new InvalidOperationException("Missing user id claim."));

    public static int GetSessionVersion(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(SessionVersionClaim), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : -1;

    /// <summary>Checks a Telegram Login Widget answer; the problem for the caller when it does not check out.</summary>
    public static (VerifiedLogin? Login, ProblemHttpResult? Problem) Verify(TelegramLoginValidator validator, IReadOnlyDictionary<string, string>? data, ILogger logger)
    {
        var result = validator.Validate(data);
        if (result.Login is { } login)
        {
            return (login, null);
        }

        logger.LogInformation("Telegram sign-in rejected: {Reason}", result.Error);
        return (null, result.Error switch
        {
            TelegramLoginError.NotConfigured => Problem(StatusCodes.Status503ServiceUnavailable, "Telegram orqali kirish hozircha sozlanmagan."),
            TelegramLoginError.Expired => Problem(StatusCodes.Status401Unauthorized, "Telegram tasdig‘i eskirgan. Qaytadan «Telegram orqali kirish» ni bosing."),
            _ => Problem(StatusCodes.Status401Unauthorized, "Telegram orqali kirish tasdiqlanmadi. Qaytadan urinib ko‘ring."),
        });
    }

    public static async Task<(VerifiedLogin? Login, ProblemHttpResult? Problem)> VerifyAsync(GoogleIdTokenValidator validator, string? idToken, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(idToken, ct);
        return result.Login is { } login
            ? (login, null)
            : (null, result.Error switch
            {
                GoogleLoginError.NotConfigured => Problem(StatusCodes.Status503ServiceUnavailable, "Google orqali kirish hozircha sozlanmagan."),
                GoogleLoginError.Unavailable => Problem(StatusCodes.Status503ServiceUnavailable, "Google bilan aloqa bo‘lmadi. Birozdan keyin urinib ko‘ring."),
                _ => Problem(StatusCodes.Status401Unauthorized, "Google orqali kirish tasdiqlanmadi. Qaytadan urinib ko‘ring."),
            });
    }

    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> SignInWithTelegram(
        TelegramLoginRequest request,
        TelegramLoginValidator validator,
        UserService users,
        ILogger<TelegramLoginRequest> logger,
        CancellationToken ct)
    {
        var (login, problem) = Verify(validator, request.Data, logger);
        return login is null ? problem! : SignIn(await users.SignInAsync(login, ct));
    }

    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> SignInWithGoogle(
        GoogleLoginRequest request,
        GoogleIdTokenValidator validator,
        UserService users,
        CancellationToken ct)
    {
        var (login, problem) = await VerifyAsync(validator, request.IdToken, ct);
        return login is null ? problem! : SignIn(await users.SignInAsync(login, ct));
    }

    /// <summary>A new pair of tokens without signing in again, unless the user signed out or was deleted since (L-13).</summary>
    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> Refresh(
        RefreshRequest request,
        IOptionsMonitor<BearerTokenOptions> bearer,
        UserService users,
        TimeProvider time,
        CancellationToken ct)
    {
        var protector = bearer.Get(BearerTokenDefaults.AuthenticationScheme).RefreshTokenProtector;
        var ticket = string.IsNullOrEmpty(request.RefreshToken) ? null : protector.Unprotect(request.RefreshToken);
        if (ticket?.Properties.ExpiresUtc is not { } expires || time.GetUtcNow() >= expires
            || ticket.Principal.FindFirstValue(UserIdClaim) is not { } id || !Guid.TryParse(id, out var userId)
            || await users.CheckSessionAsync(userId, ct) is not { } session || session.SessionVersion != ticket.Principal.GetSessionVersion())
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

        var login = new VerifiedLogin(LoginProvider.Telegram, id.ToString(CultureInfo.InvariantCulture), "Dev", null);
        return SignIn(await users.SignInAsync(login, ct));
    }

    /// <summary>The signed-in identity carried by the bearer token: the user id and session version only (L-13).</summary>
    public static ClaimsPrincipal CreatePrincipal(User user) => new(new ClaimsIdentity(
        [
            new Claim(UserIdClaim, user.Id.ToString()),
            new Claim(SessionVersionClaim, user.SessionVersion.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        ],
        BearerTokenDefaults.AuthenticationScheme));

    private static SignInHttpResult SignIn(User user) => SignIn(CreatePrincipal(user));

    private static SignInHttpResult SignIn(ClaimsPrincipal principal) =>
        TypedResults.SignIn(principal, authenticationScheme: BearerTokenDefaults.AuthenticationScheme);

    private static ProblemHttpResult Problem(int status, string detail) =>
        TypedResults.Problem(detail: detail, statusCode: status);
}
