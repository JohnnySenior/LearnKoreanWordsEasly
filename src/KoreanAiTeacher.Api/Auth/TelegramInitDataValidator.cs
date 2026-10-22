using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace KoreanAiTeacher.Api.Auth;

public sealed record TelegramUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("language_code")] string? LanguageCode);

public enum InitDataError
{
    NotConfigured,
    Missing,
    Malformed,
    BadSignature,
    Expired,
}

public sealed record InitDataValidationResult(TelegramUser? User, InitDataError? Error)
{
    public static InitDataValidationResult Fail(InitDataError error) => new(null, error);
}

/// <summary>
/// Server-side check of Telegram Mini App <c>initData</c> (spec SEC01).
/// data_check_string = every received field except <c>hash</c>, sorted by key, as <c>key=value</c> joined by '\n';
/// secret_key = HMAC_SHA256(key: "WebAppData", data: bot_token);
/// expected hash = hex(HMAC_SHA256(key: secret_key, data: data_check_string)).
/// <c>initDataUnsafe</c> from the client is never trusted.
/// </summary>
public sealed class TelegramInitDataValidator(IOptionsMonitor<TelegramOptions> options, TimeProvider time)
{
    /// <summary>Tolerated clock difference for an auth_date slightly in the future.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    public InitDataValidationResult Validate(string? initData)
    {
        var opts = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(opts.BotToken))
        {
            return InitDataValidationResult.Fail(InitDataError.NotConfigured);
        }

        if (string.IsNullOrWhiteSpace(initData))
        {
            return InitDataValidationResult.Fail(InitDataError.Missing);
        }

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, values) in QueryHelpers.ParseQuery(initData))
        {
            if (values.Count != 1)
            {
                return InitDataValidationResult.Fail(InitDataError.Malformed);
            }

            fields[key] = values[0]!;
        }

        if (!fields.Remove("hash", out var hashHex) || !TryFromHex(hashHex, out var receivedHash))
        {
            return InitDataValidationResult.Fail(InitDataError.Malformed);
        }

        var dataCheckString = string.Join('\n', fields.Select(f => $"{f.Key}={f.Value}"));
        var expectedHash = ComputeHash(opts.BotToken, dataCheckString);
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, receivedHash))
        {
            return InitDataValidationResult.Fail(InitDataError.BadSignature);
        }

        if (!fields.TryGetValue("auth_date", out var authDateRaw) || !long.TryParse(authDateRaw, out var authDateUnix))
        {
            return InitDataValidationResult.Fail(InitDataError.Malformed);
        }

        var age = time.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(authDateUnix);
        if (age > opts.InitDataMaxAge || age < -ClockSkew)
        {
            return InitDataValidationResult.Fail(InitDataError.Expired);
        }

        if (!fields.TryGetValue("user", out var userJson))
        {
            return InitDataValidationResult.Fail(InitDataError.Malformed);
        }

        TelegramUser? user;
        try
        {
            user = JsonSerializer.Deserialize<TelegramUser>(userJson);
        }
        catch (JsonException)
        {
            return InitDataValidationResult.Fail(InitDataError.Malformed);
        }

        return user is { Id: > 0 }
            ? new InitDataValidationResult(user, null)
            : InitDataValidationResult.Fail(InitDataError.Malformed);
    }

    internal static byte[] ComputeHash(string botToken, string dataCheckString)
    {
        var secretKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        return HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
    }

    private static bool TryFromHex(string hex, out byte[] bytes)
    {
        bytes = [];
        if (hex.Length != 64)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromHexString(hex);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
