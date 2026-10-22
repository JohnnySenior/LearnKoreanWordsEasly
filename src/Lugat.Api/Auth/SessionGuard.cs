using System.Security.Claims;
using Lugat.Application.Users;
using Lugat.Domain.Users;

namespace Lugat.Api.Auth;

/// <summary>
/// Runs after authentication on every request (L-13). A token of a deleted user, or one issued before the user
/// signed out, is treated as no token at all, so protected endpoints answer 401 at once instead of after the access
/// token expires. For a valid token the user's current roles are added from the database, so a role given or taken
/// away counts on the next request.
/// </summary>
public static class SessionGuard
{
    public static IApplicationBuilder UseSessionGuard(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var session = Guid.TryParse(http.User.FindFirstValue(AuthEndpoints.UserIdClaim), out var userId)
                ? await http.RequestServices.GetRequiredService<UserService>().CheckSessionAsync(userId, http.RequestAborted)
                : null;
            if (session is null || session.SessionVersion != http.User.GetSessionVersion())
            {
                http.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
            else if (http.User.Identity is ClaimsIdentity identity)
            {
                foreach (var role in Enum.GetValues<UserRoles>().Where(r => r != UserRoles.None && session.Roles.HasFlag(r)))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
                }
            }
        }

        await next(http);
    });
}
