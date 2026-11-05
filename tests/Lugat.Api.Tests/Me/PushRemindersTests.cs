using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Abstractions;
using Lugat.Application.Reminders;
using Lugat.Application.Users;
using Lugat.Contracts;
using Lugat.Domain.Learning;
using Lugat.Domain.Users;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Me;

internal sealed class FakePush : IWebPushSender
{
    public List<(PushTarget Target, PushNotification Notification)> Sent { get; } = [];

    public PushOutcome NextOutcome { get; set; } = PushOutcome.Sent;

    public bool IsConfigured => true;

    public string? PublicKey => "BTestPublicKey";

    public Task<PushOutcome> SendAsync(PushTarget target, PushNotification notification, CancellationToken ct)
    {
        if (NextOutcome == PushOutcome.Sent)
        {
            Sent.Add((target, notification));
        }

        return Task.FromResult(NextOutcome);
    }
}

/// <summary>L-63: reminders by Web Push, for Google learners too, and never twice when the bot is linked as well.</summary>
public class PushRemindersTests
{
    private static readonly DateTimeOffset MondayEvening = new DateTimeOffset(2026, 10, 5, 20, 1, 0, TimeSpan.FromHours(9)).ToUniversalTime();

    private readonly FakeMessenger _telegram = new();

    private readonly FakePush _push = new();

    private ApiFactory NewFactory(bool push = true)
    {
        var factory = new ApiFactory { Messenger = _telegram, Push = push ? _push : null };
        factory.Settings["Reminders:PollInterval"] = "01:00:00";
        factory.Time.SetUtcNow(MondayEvening.AddHours(-1));
        return factory;
    }

    private static PushSubscribeRequest Device(string name) => new($"https://push.example/{name}", "p256dh-" + name, "auth-" + name);

    /// <summary>A learner with an opened unit and <paramref name="due"/> cards due now.</summary>
    private static async Task<Guid> LearnerAsync(ApiFactory factory, VerifiedLogin login, int due)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserService>().SignInAsync(login, CancellationToken.None);
        await ApiFactory.OpenUnitAsync(scope.ServiceProvider, user.Id, 1, ApiFactory.Words("가", 3));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Both directions of <due> words, so "N ta so‘z" is exact.
        var items = (await db.Cards.Where(c => c.OwnerId == user.Id).Select(c => c.LearningItemId).Distinct().ToListAsync()).Order().Take(due).ToList();
        var cards = await db.Cards.Where(c => c.OwnerId == user.Id && items.Contains(c.LearningItemId)).ToListAsync();
        foreach (var card in cards)
        {
            (card.Phase, card.Stage, card.DueAt, card.Interval) = (LearningPhase.Review, 1, MondayEvening.AddHours(-2), TimeSpan.FromDays(1));
        }

        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task SubscribeAsync(ApiFactory factory, Guid userId, PushSubscribeRequest device)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PushSubscriptionService>().SubscribeAsync(userId, device, "test", CancellationToken.None);
    }

    private static VerifiedLogin Google(string id) => new(LoginProvider.Google, id, "G", null);

    [Fact]
    public async Task A_google_learner_with_a_subscribed_phone_gets_the_reminder_by_push()
    {
        await using var factory = NewFactory();
        var learner = await LearnerAsync(factory, Google("g-1"), due: 2);
        await SubscribeAsync(factory, learner, Device("phone"));
        factory.Time.SetUtcNow(MondayEvening);

        Assert.Equal(1, await RemindersTests.RunAsync(factory));
        Assert.Equal(0, await RemindersTests.RunAsync(factory)); // once per occurrence

        var (target, note) = Assert.Single(_push.Sent);
        Assert.Equal(("https://push.example/phone", "p256dh-phone", "auth-phone"), (target.Endpoint, target.P256dh, target.Auth));
        Assert.StartsWith("Bugun 2 ta so‘zni takrorlash vaqti. ~", note.Body);
        Assert.Equal(("/takrorlash", ReminderService.PushTag), (note.Url, note.Tag)); // tapping opens practice
        Assert.Empty(_telegram.Sent);
    }

    [Fact]
    public async Task With_both_the_bot_and_a_phone_the_reminder_comes_once_to_the_phone()
    {
        await using var factory = NewFactory();
        var learner = await LearnerAsync(factory, ApiFactory.TelegramLogin(ApiFactory.UserA, "A"), due: 1);
        await SubscribeAsync(factory, learner, Device("phone"));
        factory.Time.SetUtcNow(MondayEvening);

        Assert.Equal(1, await RemindersTests.RunAsync(factory));
        Assert.Single(_push.Sent);
        Assert.Empty(_telegram.Sent);
    }

    [Fact]
    public async Task A_gone_device_is_forgotten_and_the_bot_takes_that_reminder()
    {
        await using var factory = NewFactory();
        var learner = await LearnerAsync(factory, ApiFactory.TelegramLogin(ApiFactory.UserA, "A"), due: 1);
        await SubscribeAsync(factory, learner, Device("old-phone"));
        _push.NextOutcome = PushOutcome.Gone; // the push service answered 410
        factory.Time.SetUtcNow(MondayEvening);

        Assert.Equal(1, await RemindersTests.RunAsync(factory));

        Assert.Empty(_push.Sent);
        Assert.Equal([ApiFactory.UserA], _telegram.Sent.Select(s => s.ChatId));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushSubscriptions.AnyAsync());
    }

    [Fact]
    public async Task Nothing_due_means_no_push_and_a_google_learner_without_a_device_gets_nothing()
    {
        await using var factory = NewFactory();
        var idle = await LearnerAsync(factory, Google("g-idle"), due: 0);
        await SubscribeAsync(factory, idle, Device("idle"));
        await LearnerAsync(factory, Google("g-none"), due: 2); // no device, no bot
        factory.Time.SetUtcNow(MondayEvening);

        Assert.Equal(0, await RemindersTests.RunAsync(factory));
        Assert.Empty(_push.Sent);
        Assert.Empty(_telegram.Sent);
    }

    [Fact]
    public async Task The_device_endpoints_keep_to_their_owner_and_the_test_message_goes_by_push()
    {
        await using var factory = NewFactory();
        var a = await factory.SignedInClientAsync(ApiFactory.UserA);
        var b = await factory.SignedInClientAsync(ApiFactory.UserB);
        Assert.Equal("BTestPublicKey", (await a.GetFromJsonAsync<PushKeyDto>("/api/me/push/key", ApiFactory.Json))!.PublicKey);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync("/api/me/push", Device("a-phone"), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await a.PostAsJsonAsync("/api/me/push", new PushSubscribeRequest("http://insecure", "k", "a"), ApiFactory.Json)).StatusCode);

        // B cannot remove A's device, even knowing its endpoint.
        using var other = new HttpRequestMessage(HttpMethod.Delete, "/api/me/push") { Content = JsonContent.Create(new PushUnsubscribeRequest("https://push.example/a-phone"), options: ApiFactory.Json) };
        Assert.Equal(HttpStatusCode.NoContent, (await b.SendAsync(other)).StatusCode);
        var prefs = await a.GetFromJsonAsync<PreferencesDto>("/api/me/preferences", ApiFactory.Json);
        Assert.Equal((true, true, 1), (prefs!.RemindersAvailable, prefs.PushAvailable, prefs.PushDevices));

        var test = await (await a.PostAsync("/api/me/reminders/test", null)).Content.ReadFromJsonAsync<TestReminderResult>(ApiFactory.Json);
        Assert.True(test!.Delivered);
        Assert.Contains("Bildirishnoma", test.Message);
        Assert.StartsWith("Sinov xabari", Assert.Single(_push.Sent).Notification.Body);
        Assert.Empty(_telegram.Sent);

        using var own = new HttpRequestMessage(HttpMethod.Delete, "/api/me/push") { Content = JsonContent.Create(new PushUnsubscribeRequest("https://push.example/a-phone"), options: ApiFactory.Json) };
        Assert.Equal(HttpStatusCode.NoContent, (await a.SendAsync(own)).StatusCode);
        Assert.Equal(0, (await a.GetFromJsonAsync<PreferencesDto>("/api/me/preferences", ApiFactory.Json))!.PushDevices);
    }

    [Fact]
    public async Task Without_vapid_keys_push_is_off_and_says_so()
    {
        await using var factory = NewFactory(push: false);
        var a = await factory.SignedInClientAsync(ApiFactory.UserA);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await a.GetAsync("/api/me/push/key")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await a.PostAsJsonAsync("/api/me/push", Device("x"), ApiFactory.Json)).StatusCode);
        var prefs = await a.GetFromJsonAsync<PreferencesDto>("/api/me/preferences", ApiFactory.Json);
        Assert.Equal((true, false, true), (prefs!.RemindersAvailable, prefs.PushAvailable, prefs.HasTelegram)); // the bot still works
    }

    [Fact]
    public async Task The_metrics_show_reminder_channels_and_the_return_after_the_first_unit_with_and_without_push()
    {
        // L-63: P has a phone subscribed and practises 1 and 7 days after the first unit; T (Telegram only) only after 1.
        const long P = 3001, T = 3002, Admin = 3009;
        await using var factory = new ApiFactory { Messenger = _telegram, Push = _push, TableWords = ApiFactory.Words("가", 3), AdminTelegramIds = [Admin] };
        factory.Settings["Reminders:PollInterval"] = "01:00:00";
        var start = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(9)).ToUniversalTime();
        factory.Time.SetUtcNow(start);
        var p = await factory.SignedInClientAsync(P, "P");
        var t = await factory.SignedInClientAsync(T, "T");
        foreach (var c in new[] { p, t })
        {
            await RemindersTests.ApproveOneTableAsync(c);
        }

        Assert.Equal(System.Net.HttpStatusCode.NoContent, (await p.PostAsJsonAsync("/api/me/push", Device("p-phone"), ApiFactory.Json)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var progress in await db.UnitProgress.ToListAsync())
            {
                progress.PassedAt = start; // the first unit is done on day 0 (the date is what the figure reads)
            }

            await db.SaveChangesAsync();
        }

        var none = await LearnerAsync(factory, Google("g-none"), due: 0); // active, no device, no bot, no unit passed

        async Task PractiseAsync(HttpClient client)
        {
            var session = (await (await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, false), ApiFactory.Json))
                .Content.ReadFromJsonAsync<StartSessionResponse>(ApiFactory.Json))!.Session!;
            (await client.PostAsJsonAsync($"/api/sessions/{session.Id}/answers", new AnswerRequest(Guid.NewGuid(), session.Position, ReviewAnswer.Forgot, false, 2000), ApiFactory.Json)).EnsureSuccessStatusCode();
        }

        factory.Time.SetUtcNow(start.AddDays(1));
        await PractiseAsync(await factory.SignedInClientAsync(P, "P"));
        await PractiseAsync(await factory.SignedInClientAsync(T, "T"));
        factory.Time.SetUtcNow(start.AddDays(7));
        await PractiseAsync(await factory.SignedInClientAsync(P, "P"));
        factory.Time.SetUtcNow(start.AddDays(8));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ActivityDays.Add(new UserActivityDay { UserId = none, Day = Lugat.Domain.KoreaTime.Day(start.AddDays(8)) });
            await db.SaveChangesAsync();
        }

        var admin = await factory.SignedInClientAsync(Admin, "Admin");
        var m = await admin.GetFromJsonAsync<MetricsDto>("/api/admin/metrics", ApiFactory.Json);

        var ch = m!.Channels!;
        Assert.Equal((1, 1), (ch.Push, ch.None)); // P by push; the Google learner has none
        Assert.Equal(ch.Active - 2, ch.Telegram); // T and the admin, both Telegram logins
        var withPush = Assert.Single(m.FirstUnitReturn!, r => r.WithPush);
        var without = Assert.Single(m.FirstUnitReturn!, r => !r.WithPush);
        Assert.Equal((1, 1, 1, 1), (withPush.Day1.Cohort, withPush.Day1.Returned, withPush.Day7.Cohort, withPush.Day7.Returned));
        Assert.Equal((1, 1, 1, 0), (without.Day1.Cohort, without.Day1.Returned, without.Day7.Cohort, without.Day7.Returned));
    }
}
