using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Abstractions;
using Lugat.Application.Reminders;
using Lugat.Contracts;
using Lugat.Domain.Learning;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Me;

internal sealed class FakeMessenger : ITelegramMessenger
{
    public List<(long ChatId, string Text)> Sent { get; } = [];

    public SendOutcome NextOutcome { get; set; } = SendOutcome.Sent;

    public bool IsConfigured => true;

    public Task<SendResult> SendAsync(long chatId, string text, string openAppButton, CancellationToken ct)
    {
        if (NextOutcome == SendOutcome.Sent)
        {
            Sent.Add((chatId, text));
            return Task.FromResult(new SendResult(SendOutcome.Sent, Sent.Count));
        }

        return Task.FromResult(new SendResult(NextOutcome, Error: "fake"));
    }

    public List<(long ChatId, string FileName, byte[] Content, string Caption)> Documents { get; } = [];

    public Task<SendResult> SendDocumentAsync(long chatId, string fileName, byte[] content, string caption, CancellationToken ct)
    {
        if (NextOutcome == SendOutcome.Sent)
        {
            Documents.Add((chatId, fileName, content, caption));
            return Task.FromResult(new SendResult(SendOutcome.Sent, Documents.Count));
        }

        return Task.FromResult(new SendResult(NextOutcome, Error: "fake"));
    }
}

public class RemindersTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Monday 5 October 2026, 09:31 in Seoul.</summary>
    private static readonly DateTimeOffset MondayMorning = new DateTimeOffset(2026, 10, 5, 9, 31, 0, TimeSpan.FromHours(9)).ToUniversalTime();

    private readonly FakeMessenger _telegram = new();

    private ApiFactory NewFactory()
    {
        var factory = new ApiFactory
        {
            TableWords = ApiFactory.Words("가", 5),
            Messenger = _telegram,
        };
        factory.Settings["Reminders:PollInterval"] = "01:00:00"; // tests drive the service directly
        factory.Time.SetUtcNow(MondayMorning.AddHours(-1));
        return factory;
    }

    internal static async Task<int> RunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ReminderService>().RunDueAsync(CancellationToken.None);
    }

    internal static async Task ApproveOneTableAsync(HttpClient client)
    {
        (await client.PostAsync(ApiFactory.TestWordsPath, null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Morning_reminder_is_sent_once_even_if_the_worker_runs_again()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveOneTableAsync(client);
        factory.Time.SetUtcNow(MondayMorning);

        var first = await RunAsync(factory);
        var second = await RunAsync(factory); // e.g. after a restart

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        var (chat, text) = Assert.Single(_telegram.Sent);
        Assert.Equal(ApiFactory.UserA, chat);
        Assert.Contains("10 ta yangi kartochka", text);
        Assert.Contains("daqiqa", text);
    }

    [Fact]
    public async Task Nothing_to_practise_means_no_message()
    {
        await using var factory = NewFactory();
        await factory.SignedInClientAsync(ApiFactory.UserA); // user exists, no approved words
        factory.Time.SetUtcNow(MondayMorning);

        Assert.Equal(0, await RunAsync(factory));
        Assert.Empty(_telegram.Sent);
    }

    [Fact]
    public async Task Late_reminder_is_skipped_not_piled_up()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveOneTableAsync(client);
        factory.Time.SetUtcNow(MondayMorning.AddMinutes(40)); // 10:11, the server was down at 09:30

        Assert.Equal(0, await RunAsync(factory));
        Assert.Empty(_telegram.Sent);
    }

    [Fact]
    public async Task Turned_off_reminders_are_not_sent_and_settings_are_saved()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveOneTableAsync(client);

        var saved = await client.PatchAsJsonAsync("/api/me/preferences", new UpdatePreferencesRequest(false, new TimeOnly(8, 15)), Json);
        var prefs = (await saved.Content.ReadFromJsonAsync<PreferencesDto>(Json))!;
        factory.Time.SetUtcNow(MondayMorning);

        Assert.False(prefs.RemindersEnabled);
        Assert.Equal(new TimeOnly(8, 15), prefs.WeekdayReminderTime);
        Assert.Equal("Asia/Seoul", prefs.TimeZone);
        Assert.Equal(0, await RunAsync(factory));
        Assert.Empty(_telegram.Sent);
    }

    [Fact]
    public async Task Blocked_bot_is_shown_in_preferences_until_a_message_gets_through()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        _telegram.NextOutcome = SendOutcome.Blocked;

        var test = await (await client.PostAsync("/api/me/reminders/test", null)).Content.ReadFromJsonAsync<TestReminderResult>(Json);
        var blocked = await client.GetFromJsonAsync<PreferencesDto>("/api/me/preferences", Json);
        _telegram.NextOutcome = SendOutcome.Sent;
        await client.PostAsync("/api/me/reminders/test", null);
        var unblocked = await client.GetFromJsonAsync<PreferencesDto>("/api/me/preferences", Json);

        Assert.False(test!.Delivered);
        Assert.Contains("/start", test.Message);
        Assert.True(blocked!.BotBlocked);
        Assert.False(unblocked!.BotBlocked);
    }

    [Fact]
    public async Task Each_user_gets_only_their_own_reminder()
    {
        await using var factory = NewFactory();
        var a = await factory.SignedInClientAsync(ApiFactory.UserA);
        await factory.SignedInClientAsync(ApiFactory.UserB); // no words yet
        await ApproveOneTableAsync(a);
        factory.Time.SetUtcNow(MondayMorning);

        await RunAsync(factory);

        Assert.Equal([ApiFactory.UserA], _telegram.Sent.Select(s => s.ChatId));
    }

    [Fact]
    public async Task Reminders_go_only_to_learners_with_a_telegram_login()
    {
        await using var factory = NewFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // A learner who signed in with Google only: the bot has no chat to write to (spec §8).
            var users = scope.ServiceProvider.GetRequiredService<Lugat.Application.Users.UserService>();
            var google = await users.SignInAsync(new Lugat.Application.Users.VerifiedLogin(Lugat.Domain.Users.LoginProvider.Google, "g-1", "G", null), CancellationToken.None);
            await ApiFactory.OpenUnitAsync(scope.ServiceProvider, google.Id, 1, [("가가", "ma’no")]);
        }

        var a = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveOneTableAsync(a);
        factory.Time.SetUtcNow(MondayMorning);

        await RunAsync(factory);

        Assert.Equal([ApiFactory.UserA], _telegram.Sent.Select(s => s.ChatId));
    }

    [Fact]
    public async Task Progress_counts_directions_separately()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await ApproveOneTableAsync(client);
        var session = (await (await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, false), Json))
            .Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;

        // Know every word Korean → Uzbek, forget every word Uzbek → Korean once.
        var answered = 0;
        while (session.Current is { } card && answered < 10)
        {
            var answer = card.Direction == ReviewDirection.KoreanToUzbek ? ReviewAnswer.Easy : ReviewAnswer.Forgot;
            session = (await (await client.PostAsJsonAsync($"/api/sessions/{session.Id}/answers", new AnswerRequest(Guid.NewGuid(), session.Position, answer, false, 2000), Json))
                .Content.ReadFromJsonAsync<AnswerResultDto>(Json))!.Session;
            answered++;
        }

        var progress = (await client.GetFromJsonAsync<ProgressDto>("/api/progress", Json))!;

        Assert.Equal(5, progress.Words);
        Assert.Equal(5, progress.Seen);
        Assert.Equal(5, progress.RecalledKoreanToUzbek);
        Assert.Equal(0, progress.RecalledUzbekToKorean);
        Assert.Equal(0, progress.Learned);
        Assert.Equal(10, progress.ReviewsLast7Days);
        Assert.Empty(progress.Hardest); // forgetting a new card is not a lapse
    }
}
