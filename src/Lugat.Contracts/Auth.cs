using Lugat.Domain.Users;

namespace Lugat.Contracts;

/// <summary>What the sign-in screen can offer on this server (GET /api/auth/config).</summary>
/// <param name="TelegramBot">Bot username for the Telegram Login Widget; null when not configured.</param>
/// <param name="GoogleClientId">Google Identity Services client id; null when not configured.</param>
/// <param name="DevLogin">The Development-only sign-in exists.</param>
/// <param name="TelegramBotId">The bot's numeric id (the part of the token before ":"), public like its username; the
/// sign-in button opens Telegram's own login window with it (UI handoff 1a). The rest of the token stays secret.</param>
public sealed record AuthConfigDto(string? TelegramBot, string? GoogleClientId, bool DevLogin, long? TelegramBotId = null);

/// <summary>
/// The fields the Telegram Login Widget returned (id, first_name, last_name, username, photo_url, auth_date, hash),
/// all as strings. Kept as a map so a field Telegram adds later still goes into the hash check (spec §6.3).
/// </summary>
public sealed record TelegramLoginRequest(Dictionary<string, string>? Data);

/// <summary>The ID token (JWT credential) from Google Identity Services.</summary>
public sealed record GoogleLoginRequest(string? IdToken);

/// <summary>Adds a second way to sign in to the current account (Profil → "Hisobni bog‘lash").</summary>
public sealed record LinkLoginRequest(LoginProvider Provider, Dictionary<string, string>? Telegram, string? GoogleIdToken);

public sealed record LoginDto(LoginProvider Provider, string? Display, DateTimeOffset CreatedAt);

public sealed record MeDto(
    Guid Id,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<LoginDto> Logins,
    string TimeZone,
    bool ShowRomanization,
    ThemeChoice Theme);

/// <summary>Null fields are left unchanged.</summary>
public sealed record UpdateMeRequest(string? DisplayName, bool? ShowRomanization, ThemeChoice? Theme);
