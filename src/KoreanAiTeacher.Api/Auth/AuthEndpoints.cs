using System.Globalization;
using System.Security.Claims;
using KoreanAiTeacher.Application.Users;
using KoreanAiTeacher.Contracts;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace KoreanAiTeacher.Api.Auth;

public sealed record TelegramAuthRequest(string? InitData);

public sealed record RefreshRequest(string? RefreshToken);

public static class AuthEndpoints
{
    public const string TelegramIdClaim = "tg_id";
    public const string UsernameClaim = "tg_username";
    public const string UserIdClaim = "uid";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, IHostEnvironment env)
    {
        var auth = app.MapGroup("/api/auth").AllowAnonymous();

        auth.MapPost("/telegram", SignInWithTelegram);
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

    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> SignInWithTelegram(
        TelegramAuthRequest request,
        TelegramInitDataValidator validator,
        IOptionsMonitor<TelegramOptions> telegram,
        UserService users,
        ILogger<TelegramAuthRequest> logger,
        CancellationToken ct)
    {
        var result = validator.Validate(request.InitData);
        if (result.User is not { } user)
        {
            logger.LogInformation("Telegram sign-in rejected: {Reason}", result.Error);
            return result.Error == InitDataError.NotConfigured
                ? Problem(StatusCodes.Status503ServiceUnavailable, "Kirish vaqtincha ishlamayapti. Keyinroq urinib ko‘ring.")
                : Problem(StatusCodes.Status401Unauthorized, "Telegram orqali kirish tasdiqlanmadi. Ilovani Telegram ichidan qayta oching.");
        }

        if (!telegram.CurrentValue.IsAllowed(user.Id))
        {
            logger.LogWarning("Telegram user {UserId} is not on the allow-list", user.Id);
            return Problem(StatusCodes.Status403Forbidden, "Bu ilova hozircha faqat taklif qilingan foydalanuvchilar uchun.");
        }

        var appUser = await users.EnsureAsync(user.Id, user.FirstName, user.Username, ct);
        return SignIn(CreatePrincipal(appUser.Id, user.Id, user.FirstName, user.Username));
    }

    private static Results<SignInHttpResult, ProblemHttpResult> Refresh(
        RefreshRequest request,
        IOptionsMonitor<BearerTokenOptions> bearer,
        IOptionsMonitor<TelegramOptions> telegram,
        TimeProvider time)
    {
        var protector = bearer.Get(BearerTokenDefaults.AuthenticationScheme).RefreshTokenProtector;
        var ticket = string.IsNullOrEmpty(request.RefreshToken) ? null : protector.Unprotect(request.RefreshToken);

        if (ticket?.Properties.ExpiresUtc is not { } expires || time.GetUtcNow() >= expires)
        {
            return Problem(StatusCodes.Status401Unauthorized, "Sessiya muddati tugadi. Ilovani Telegram ichidan qayta oching.");
        }

        var principal = ticket.Principal;
        if (!telegram.CurrentValue.IsAllowed(GetTelegramUserId(principal)))
        {
            return Problem(StatusCodes.Status403Forbidden, "Bu ilova hozircha faqat taklif qilingan foydalanuvchilar uchun.");
        }

        return SignIn(principal);
    }

    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> DevSignIn(
        IOptionsMonitor<AuthOptions> auth,
        IOptionsMonitor<TelegramOptions> telegram,
        UserService users,
        CancellationToken ct)
    {
        if (auth.CurrentValue.DevLoginUserId is not { } id || !telegram.CurrentValue.IsAllowed(id))
        {
            return Problem(StatusCodes.Status403Forbidden, "Dev kirish sozlanmagan.");
        }

        var appUser = await users.EnsureAsync(id, "Dev", null, ct);
        return SignIn(CreatePrincipal(appUser.Id, id, "Dev", null));
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, long telegramUserId, string? firstName, string? username)
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
