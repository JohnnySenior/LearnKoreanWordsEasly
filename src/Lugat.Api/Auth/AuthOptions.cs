namespace Lugat.Api.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>30 days: an installed PWA should not ask to sign in every week (spec §6.3).</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Development only: lets a browser sign in as this Telegram user id without a real login.
    /// The endpoint is not even mapped outside the Development environment.
    /// </summary>
    public long? DevLoginUserId { get; set; }

    /// <summary>Sign-in requests allowed per minute from one IP (spec §6.4).</summary>
    public int SignInPerMinute { get; set; } = 10;

    /// <summary>Tests a user may start per hour (spec §6.4).</summary>
    public int TestStartsPerHour { get; set; } = 20;
}

/// <summary>Authorization policies by role (spec §6.4). Roles come from the database on every request (L-13).</summary>
public static class Policies
{
    public const string Admin = "Admin";

    /// <summary>Content work: editors and admins (spec §10).</summary>
    public const string Editor = "Editor";
}
