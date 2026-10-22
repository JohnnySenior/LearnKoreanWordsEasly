using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Lugat.Application.Users;
using Lugat.Domain.Users;
using Microsoft.Extensions.Options;

namespace Lugat.Api.Auth;

public sealed class TelegramLoginOptions
{
    public const string SectionName = "Telegram";

    /// <summary>Bot token from BotFather. Secret: set via environment/hosting secrets, never in the repo.</summary>
    public string BotToken { get; set; } = "";

    /// <summary>Bot username (without @) for the Login Widget; its domain is set in BotFather (/setdomain).</summary>
    public string BotUsername { get; set; } = "";

    /// <summary>How old the widget's <c>auth_date</c> may be (spec §6.3: 24 hours).</summary>
    public TimeSpan LoginMaxAge { get; set; } = TimeSpan.FromHours(24);
}

public enum TelegramLoginError
{
    None,
    NotConfigured,
    Malformed,
    BadHash,
    Expired,
}

public sealed record TelegramLoginResult(VerifiedLogin? Login, TelegramLoginError Error);

/// <summary>
/// Telegram Login Widget check (https://core.telegram.org/widgets/login, spec §6.3): every field but <c>hash</c>,
/// sorted by key, joined as <c>key=value</c> lines; <c>secret = SHA256(bot_token)</c>;
/// <c>hash == hex(HMAC_SHA256(secret, data_check_string))</c>; <c>auth_date</c> at most 24 hours old.
/// This differs from the Mini App initData check (KAT D-04), which is not used here.
/// </summary>
public sealed class TelegramLoginValidator(IOptionsMonitor<TelegramLoginOptions> options, TimeProvider time)
{
    /// <summary>A widget answer from slightly in the future is a clock difference, not an attack.</summary>
    private static readonly TimeSpan FutureSkew = TimeSpan.FromMinutes(1);

    public TelegramLoginResult Validate(IReadOnlyDictionary<string, string>? data)
    {
        var o = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(o.BotToken))
        {
            return new(null, TelegramLoginError.NotConfigured);
        }

        if (data is null
            || !data.TryGetValue("hash", out var hash) || hash.Length != 64
            || !data.TryGetValue("id", out var id) || !long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0
            || !data.TryGetValue("auth_date", out var authDate) || !long.TryParse(authDate, NumberStyles.None, CultureInfo.InvariantCulture, out var authSeconds)
            || data.Keys.Any(k => k.Contains('\n') || k.Contains('=')))
        {
            return new(null, TelegramLoginError.Malformed);
        }

        byte[] given;
        try
        {
            given = Convert.FromHexString(hash);
        }
        catch (FormatException)
        {
            return new(null, TelegramLoginError.Malformed);
        }

        var checkString = string.Join('\n', data.Where(p => p.Key != "hash").OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(o.BotToken));
        var expected = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(checkString));
        if (!CryptographicOperations.FixedTimeEquals(expected, given))
        {
            return new(null, TelegramLoginError.BadHash);
        }

        var signedAt = DateTimeOffset.FromUnixTimeSeconds(authSeconds);
        var now = time.GetUtcNow();
        if (now - signedAt > o.LoginMaxAge || signedAt - now > FutureSkew)
        {
            return new(null, TelegramLoginError.Expired);
        }

        var name = string.Join(' ', new[] { data.GetValueOrDefault("first_name"), data.GetValueOrDefault("last_name") }.Where(n => !string.IsNullOrWhiteSpace(n)));
        var username = data.GetValueOrDefault("username");
        return new(new VerifiedLogin(
            LoginProvider.Telegram,
            userId.ToString(CultureInfo.InvariantCulture),
            name.Length == 0 ? null : name,
            string.IsNullOrWhiteSpace(username) ? null : "@" + username), TelegramLoginError.None);
    }
}
