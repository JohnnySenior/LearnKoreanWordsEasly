using KoreanAiTeacher.Api.Auth;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace KoreanAiTeacher.Api.Tests.Auth;

public class TelegramInitDataValidatorTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero));

    private TelegramInitDataValidator Validator(string botToken = InitDataBuilder.BotToken) =>
        new(new StaticOptions(new TelegramOptions { BotToken = botToken }), _time);

    [Fact]
    public void Accepts_fresh_correctly_signed_data()
    {
        var r = Validator().Validate(InitDataBuilder.Build(42, _time.GetUtcNow()));

        Assert.Null(r.Error);
        Assert.Equal(42, r.User!.Id);
        Assert.Equal("Ali", r.User.FirstName);
        Assert.Equal("ali_k", r.User.Username);
    }

    [Fact]
    public void Signature_field_is_part_of_the_signed_data()
    {
        var data = InitDataBuilder.Build(42, _time.GetUtcNow(), extra: new Dictionary<string, string> { ["signature"] = "abc" });

        Assert.Null(Validator().Validate(data).Error);
    }

    [Fact]
    public void Rejects_tampered_user()
    {
        var data = InitDataBuilder.Build(42, _time.GetUtcNow()).Replace("%22id%22%3A42", "%22id%22%3A43");

        Assert.Equal(InitDataError.BadSignature, Validator().Validate(data).Error);
    }

    [Fact]
    public void Rejects_data_signed_for_another_bot()
    {
        var data = InitDataBuilder.Build(42, _time.GetUtcNow(), botToken: "999:other");

        Assert.Equal(InitDataError.BadSignature, Validator().Validate(data).Error);
    }

    [Fact]
    public void Rejects_data_older_than_five_minutes()
    {
        var data = InitDataBuilder.Build(42, _time.GetUtcNow());
        _time.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        Assert.Equal(InitDataError.Expired, Validator().Validate(data).Error);
    }

    [Fact]
    public void Rejects_auth_date_in_the_future()
    {
        var data = InitDataBuilder.Build(42, _time.GetUtcNow().AddMinutes(10));

        Assert.Equal(InitDataError.Expired, Validator().Validate(data).Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_missing(string? data) =>
        Assert.Equal(InitDataError.Missing, Validator().Validate(data).Error);

    [Theory]
    [InlineData("user=%7B%7D")]
    [InlineData("auth_date=1&hash=zz")]
    [InlineData("a=1&a=2&hash=00")]
    public void Rejects_malformed(string data) =>
        Assert.Equal(InitDataError.Malformed, Validator().Validate(data).Error);

    [Fact]
    public void Reports_missing_bot_token_as_not_configured() =>
        Assert.Equal(InitDataError.NotConfigured, Validator(botToken: "").Validate("x=1").Error);

    private sealed class StaticOptions(TelegramOptions value) : IOptionsMonitor<TelegramOptions>
    {
        public TelegramOptions CurrentValue => value;

        public TelegramOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<TelegramOptions, string?> listener) => null;
    }
}
