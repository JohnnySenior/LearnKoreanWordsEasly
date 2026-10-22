namespace Lugat.Api.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Development only: lets a browser sign in as this Telegram user id without a real login.
    /// The endpoint is not even mapped outside the Development environment.
    /// </summary>
    public long? DevLoginUserId { get; set; }
}
