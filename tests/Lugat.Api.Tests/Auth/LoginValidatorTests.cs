using Lugat.Api.Auth;
using Lugat.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Lugat.Api.Tests.Auth;

/// <summary>Spec §13.2: wrong or stale Telegram hashes and wrong Google tokens are refused.</summary>
public class LoginValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);

    private static IOptionsMonitor<T> Monitor<T>(T value) where T : class => new StaticMonitor<T>(value);

    private TelegramLoginValidator Telegram(string token = TelegramWidget.BotToken) =>
        new(Monitor(new TelegramLoginOptions { BotToken = token, BotUsername = "lugat_bot" }), _time);

    [Fact]
    public void A_signed_widget_answer_is_accepted_with_name_and_username()
    {
        var result = Telegram().Validate(TelegramWidget.Build(1001, Now.AddMinutes(-3)));

        Assert.Equal(TelegramLoginError.None, result.Error);
        Assert.Equal(new Lugat.Application.Users.VerifiedLogin(LoginProvider.Telegram, "1001", "Ali Valiyev", "@ali"), result.Login);
    }

    [Fact]
    public void A_changed_field_breaks_the_hash()
    {
        var data = TelegramWidget.Build(1001, Now);
        data["id"] = "1002";

        Assert.Equal(TelegramLoginError.BadHash, Telegram().Validate(data).Error);
    }

    [Fact]
    public void An_answer_signed_for_another_bot_is_refused() =>
        Assert.Equal(TelegramLoginError.BadHash, Telegram().Validate(TelegramWidget.Build(1001, Now, botToken: "999:OTHER")).Error);

    [Fact]
    public void A_field_added_by_the_client_is_part_of_the_hash()
    {
        var data = TelegramWidget.Build(1001, Now);
        data["photo_url"] = "https://example.com/x.jpg";

        Assert.Equal(TelegramLoginError.BadHash, Telegram().Validate(data).Error);
    }

    [Theory]
    [InlineData(-25 * 60)] // older than 24 hours
    [InlineData(2)]        // from the future
    public void An_answer_outside_the_time_window_is_expired(int minutes) =>
        Assert.Equal(TelegramLoginError.Expired, Telegram().Validate(TelegramWidget.Build(1001, Now.AddMinutes(minutes))).Error);

    [Fact]
    public void Missing_or_malformed_fields_are_refused()
    {
        var noHash = TelegramWidget.Build(1001, Now);
        noHash.Remove("hash");
        var badHex = TelegramWidget.Build(1001, Now);
        badHex["hash"] = new string('z', 64);

        Assert.Equal(TelegramLoginError.Malformed, Telegram().Validate(null).Error);
        Assert.Equal(TelegramLoginError.Malformed, Telegram().Validate(noHash).Error);
        Assert.Equal(TelegramLoginError.Malformed, Telegram().Validate(badHex).Error);
    }

    [Fact]
    public void Without_a_bot_token_telegram_sign_in_is_not_configured() =>
        Assert.Equal(TelegramLoginError.NotConfigured, Telegram(token: "").Validate(TelegramWidget.Build(1001, Now)).Error);

    private GoogleIdTokenValidator Google(FakeGoogle keys, string clientId = FakeGoogle.ClientId) =>
        new(Monitor(new GoogleOptions { ClientId = clientId }), keys, _time, NullLogger<GoogleIdTokenValidator>.Instance);

    [Fact]
    public async Task A_google_token_gives_its_subject_and_verified_email()
    {
        var google = new FakeGoogle();

        var result = await Google(google).ValidateAsync(google.Token(Now), CancellationToken.None);

        Assert.Equal(new Lugat.Application.Users.VerifiedLogin(LoginProvider.Google, "google-sub-1", "Ali Google", "ali@example.com"), result.Login);
    }

    [Fact]
    public async Task An_unverified_email_is_not_shown()
    {
        var google = new FakeGoogle();

        var result = await Google(google).ValidateAsync(google.Token(Now, emailVerified: false), CancellationToken.None);

        Assert.Null(result.Login!.Display);
    }

    [Fact]
    public async Task Wrong_audience_issuer_signature_or_age_is_refused()
    {
        var google = new FakeGoogle();
        var validator = Google(google);

        string[] bad =
        [
            google.Token(Now, audience: "someone-else.apps.googleusercontent.com"),
            google.Token(Now, issuer: "https://evil.example.com"),
            google.Token(Now, key: FakeGoogle.NewKey("google-1")), // same key id, different key
            google.Token(Now.AddHours(-3)),                         // expired two hours ago
            "not-a-jwt",
        ];

        foreach (var token in bad)
        {
            Assert.Equal(GoogleLoginError.Invalid, (await validator.ValidateAsync(token, CancellationToken.None)).Error);
        }
    }

    [Fact]
    public async Task A_rotated_key_is_fetched_once_more()
    {
        var google = new FakeGoogle();
        var rotated = FakeGoogle.NewKey("google-2");
        var validator = Google(google);
        await validator.ValidateAsync(google.Token(Now), CancellationToken.None);
        google.Published.Add(rotated);

        var result = await validator.ValidateAsync(google.Token(Now, key: rotated), CancellationToken.None);

        Assert.NotNull(result.Login);
        Assert.Equal(2, google.Fetches);
    }

    [Fact]
    public async Task Without_a_client_id_google_sign_in_is_not_configured()
    {
        var google = new FakeGoogle();

        Assert.Equal(GoogleLoginError.NotConfigured, (await Google(google, clientId: "").ValidateAsync(google.Token(Now), CancellationToken.None)).Error);
    }

    private sealed class StaticMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
