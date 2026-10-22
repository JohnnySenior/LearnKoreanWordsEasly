using System.Security.Cryptography;
using System.Text;

namespace KoreanAiTeacher.Api.Tests.Auth;

/// <summary>Builds initData the way Telegram does, signed with a given bot token.</summary>
internal static class InitDataBuilder
{
    public const string BotToken = "123456:TEST-bot-token";

    public static string Build(
        long userId,
        DateTimeOffset authDate,
        string botToken = BotToken,
        IDictionary<string, string>? extra = null)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(),
            ["query_id"] = "AAHdF6IQAAAAAN0XohDhrOrc",
            ["user"] = $$"""{"id":{{userId}},"first_name":"Ali","last_name":"","username":"ali_k","language_code":"uz","allows_write_to_pm":true}""",
        };
        foreach (var (k, v) in extra ?? new Dictionary<string, string>())
        {
            fields[k] = v;
        }

        var dataCheckString = string.Join("\n", fields.Select(f => f.Key + "=" + f.Value));
        using var outer = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secret = outer.ComputeHash(Encoding.UTF8.GetBytes(botToken));
        using var inner = new HMACSHA256(secret);
        var hash = Convert.ToHexStringLower(inner.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString)));

        fields["hash"] = hash;
        return string.Join("&", fields.Select(f => f.Key + "=" + Uri.EscapeDataString(f.Value)));
    }
}
