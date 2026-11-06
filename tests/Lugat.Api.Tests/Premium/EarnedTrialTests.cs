using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Api.Tests.Me;
using Lugat.Application.Reminders;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Lugat.Domain.Premium;
using Lugat.Domain.Users;
using Lugat.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Premium;

/// <summary>
/// L-64: three days of Premium the learner starts after their first unit — once, never after any earlier trial, and
/// with one calm note a day before it ends.
/// </summary>
public class EarnedTrialTests
{
    private readonly FakeMessenger _telegram = new();

    private readonly FakePush _push = new();

    private ApiFactory NewFactory(int? signupTrialDays = null, int? earnedDays = null)
    {
        var factory = new ApiFactory { Premium = false, Messenger = _telegram, Push = _push };
        factory.Settings["Reminders:PollInterval"] = "01:00:00";
        if (signupTrialDays is { } signup)
        {
            factory.Settings["Premium:TrialDays"] = signup.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (earnedDays is { } earned)
        {
            factory.Settings["Premium:EarnedTrialDays"] = earned.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return factory;
    }

    private static async Task<Guid> UserIdAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var id = ApiFactory.UserA.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserLogins
            .Where(l => l.Provider == LoginProvider.Telegram && l.ProviderUserId == id).Select(l => l.UserId).SingleAsync();
    }

    private static async Task PassFirstUnitAsync(ApiFactory factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await ApiFactory.OpenUnitAsync(scope.ServiceProvider, userId, 1, ApiFactory.Words("가", 3));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var progress = await db.UnitProgress.SingleAsync(p => p.UserId == userId);
        (progress.Status, progress.PassedAt, progress.PassedBy) = (UnitProgressStatus.Passed, factory.Time.GetUtcNow(), UnitPassedBy.Test);
        await db.SaveChangesAsync();
    }

    private static async Task<TrialOfferDto> OfferAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<TrialOfferDto>("/api/premium/trial", ApiFactory.Json))!;

    private static async Task<string?> RefusalAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/premium/trial", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json))!.Detail;
    }

    [Fact]
    public async Task The_trial_opens_after_the_first_unit_runs_three_days_and_comes_once()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var userId = await UserIdAsync(factory);

        Assert.Equal(new TrialOfferDto(false, 3), await OfferAsync(client));
        Assert.Equal("Sinov birinchi bo‘lim testidan keyin ochiladi.", await RefusalAsync(client));

        await PassFirstUnitAsync(factory, userId);
        Assert.Equal(new TrialOfferDto(true, 3), await OfferAsync(client));

        var started = await client.PostAsync("/api/premium/trial", null);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var status = (await started.Content.ReadFromJsonAsync<PremiumStatusDto>(ApiFactory.Json))!;
        Assert.True(status.IsPremium);
        Assert.Equal(SubscriptionPlan.Trial, status.Plan);
        Assert.Equal(factory.Time.GetUtcNow().AddDays(3), status.EndsAt);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/mistakes")).StatusCode); // Premium features open

        // A second tap changes nothing.
        var again = (await (await client.PostAsync("/api/premium/trial", null)).Content.ReadFromJsonAsync<PremiumStatusDto>(ApiFactory.Json))!;
        Assert.Equal(status.EndsAt, again.EndsAt);
        Assert.Equal(new TrialOfferDto(false, 3), await OfferAsync(client));

        factory.Time.Advance(TimeSpan.FromDays(3) + TimeSpan.FromMinutes(1));
        client = await factory.SignedInClientAsync(ApiFactory.UserA); // the access token has expired meanwhile
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/mistakes")).StatusCode); // it simply ends
        Assert.Equal(new TrialOfferDto(false, 3), await OfferAsync(client));
        Assert.Equal("Sinov muddatidan avval foydalangansiz.", await RefusalAsync(client));

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions.CountAsync(s => s.UserId == userId));
    }

    [Fact]
    public async Task A_learner_who_had_the_signup_trial_does_not_get_the_earned_one()
    {
        await using var factory = NewFactory(signupTrialDays: 7);
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var userId = await UserIdAsync(factory);
        await PassFirstUnitAsync(factory, userId);

        Assert.Equal(new TrialOfferDto(false, 3), await OfferAsync(client)); // the signup trial runs now
        factory.Time.Advance(TimeSpan.FromDays(8));
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        Assert.Equal(new TrialOfferDto(false, 3), await OfferAsync(client)); // and after it ended
        Assert.Equal("Sinov muddatidan avval foydalangansiz.", await RefusalAsync(client));
    }

    [Fact]
    public async Task Turned_off_it_is_not_offered()
    {
        await using var factory = NewFactory(earnedDays: 0);
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await PassFirstUnitAsync(factory, await UserIdAsync(factory));

        Assert.Equal(new TrialOfferDto(false, 0), await OfferAsync(client));
        Assert.Equal("Sinov muddati hozir yo‘q.", await RefusalAsync(client));
    }

    [Fact]
    public async Task One_note_comes_a_day_before_the_trial_ends_and_not_when_reminders_are_off()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var userId = await UserIdAsync(factory);
        await PassFirstUnitAsync(factory, userId);
        (await client.PostAsync("/api/premium/trial", null)).EnsureSuccessStatusCode();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PushSubscriptionService>()
                .SubscribeAsync(userId, new PushSubscribeRequest("https://push.example/phone", "p256dh", "auth"), "test", CancellationToken.None);
        }

        factory.Time.Advance(TimeSpan.FromDays(1)); // two days left
        Assert.Equal(0, await RemindersTests.RunAsync(factory));

        factory.Time.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1)); // under a day left
        Assert.Equal(1, await RemindersTests.RunAsync(factory));
        Assert.Equal(0, await RemindersTests.RunAsync(factory)); // once
        factory.Time.Advance(TimeSpan.FromHours(2));
        Assert.Equal(0, await RemindersTests.RunAsync(factory));

        var (_, note) = Assert.Single(_push.Sent);
        Assert.Equal(ReminderService.TrialEndText, note.Body);
        Assert.Equal("/premium", note.Url);
        Assert.Empty(_telegram.Sent); // the phone took it
    }

    [Fact]
    public async Task No_note_when_reminders_are_off_or_a_payment_follows_the_trial()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var userId = await UserIdAsync(factory);
        await PassFirstUnitAsync(factory, userId);
        (await client.PostAsync("/api/premium/trial", null)).EnsureSuccessStatusCode();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.SingleAsync(u => u.Id == userId)).RemindersEnabled = false;
            await db.SaveChangesAsync();
        }

        factory.Time.Advance(TimeSpan.FromDays(2) + TimeSpan.FromMinutes(1));
        Assert.Equal(0, await RemindersTests.RunAsync(factory));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.SingleAsync(u => u.Id == userId)).RemindersEnabled = true;
            var trial = await db.Subscriptions.SingleAsync(s => s.UserId == userId);
            db.Subscriptions.Add(new Subscription
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                Plan = SubscriptionPlan.Monthly,
                StartsAt = trial.EndsAt,
                EndsAt = trial.EndsAt.AddDays(30),
                Source = SubscriptionSource.Admin,
                CreatedAt = factory.Time.GetUtcNow(),
            });
            await db.SaveChangesAsync();
        }

        Assert.Equal(0, await RemindersTests.RunAsync(factory));
        Assert.Empty(_push.Sent);
        Assert.Empty(_telegram.Sent);
    }

    [Fact]
    public async Task Premium_page_views_are_counted_per_known_feature_and_day()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        foreach (var feature in new[] { "xatolar", "xatolar", "talaffuz", "nimadir", "" })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/premium/views", new PremiumViewRequest(feature), ApiFactory.Json)).StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var views = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PremiumViews.OrderBy(v => v.Feature).Select(v => new { v.Feature, v.Count }).ToListAsync();
        Assert.Equal([new { Feature = "talaffuz", Count = 1 }, new { Feature = "xatolar", Count = 2 }], views);
    }
}
