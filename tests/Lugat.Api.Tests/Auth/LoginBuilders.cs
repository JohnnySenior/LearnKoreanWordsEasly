using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Lugat.Api.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Lugat.Api.Tests.Auth;

/// <summary>What the Telegram Login Widget would return, signed with a bot token as Telegram does.</summary>
internal static class TelegramWidget
{
    public const string BotToken = "123456:TEST-TOKEN";

    public static Dictionary<string, string> Build(long id, DateTimeOffset authDate, string botToken = BotToken,
        string? firstName = "Ali", string? lastName = "Valiyev", string? username = "ali")
    {
        var data = new Dictionary<string, string>
        {
            ["id"] = id.ToString(CultureInfo.InvariantCulture),
            ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        };
        if (firstName is not null) data["first_name"] = firstName;
        if (lastName is not null) data["last_name"] = lastName;
        if (username is not null) data["username"] = username;
        data["hash"] = Hash(data, botToken);
        return data;
    }

    public static string Hash(IReadOnlyDictionary<string, string> data, string botToken)
    {
        var check = string.Join('\n', data.Where(p => p.Key != "hash").OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        return Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(check)));
    }
}

/// <summary>Signs ID tokens the way Google does (RS256), with a key the test controls.</summary>
internal sealed class FakeGoogle : IGoogleSigningKeys
{
    public const string ClientId = "test-client.apps.googleusercontent.com";

    public RsaSecurityKey Key { get; } = NewKey("google-1");

    /// <summary>Keys the next refresh returns; lets a test rotate keys.</summary>
    public List<SecurityKey> Published { get; } = [];

    public int Fetches { get; private set; }

    public FakeGoogle() => Published.Add(Key);

    private IReadOnlyList<SecurityKey>? _cached;

    /// <summary>Like the real key source: a cached copy until a refresh is asked for.</summary>
    public Task<IReadOnlyList<SecurityKey>> GetAsync(bool refresh, CancellationToken ct)
    {
        if (refresh || _cached is null)
        {
            Fetches++;
            _cached = [.. Published];
        }

        return Task.FromResult(_cached);
    }

    public static RsaSecurityKey NewKey(string id) => new(RSA.Create(2048)) { KeyId = id };

    public string Token(DateTimeOffset now, string subject = "google-sub-1", string? audience = ClientId, string issuer = "https://accounts.google.com",
        TimeSpan? lifetime = null, SecurityKey? key = null, string? email = "ali@example.com", bool emailVerified = true, string? name = "Ali Google")
    {
        var claims = new Dictionary<string, object> { ["sub"] = subject, ["email_verified"] = emailVerified };
        if (email is not null) claims["email"] = email;
        if (name is not null) claims["name"] = name;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = (now + (lifetime ?? TimeSpan.FromHours(1))).UtcDateTime,
            SigningCredentials = new SigningCredentials(key ?? Key, SecurityAlgorithms.RsaSha256),
        });
    }
}
