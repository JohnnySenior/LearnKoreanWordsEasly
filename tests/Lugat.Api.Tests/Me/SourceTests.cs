using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Contracts;
using Lugat.Domain;
using Lugat.Domain.Users;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Me;

/// <summary>Ad channel links (L-40): visits per channel, the channel a new account came with, and the admin report.</summary>
public class SourceTests
{
    [Fact]
    public async Task Visits_and_new_accounts_are_counted_per_channel_with_their_seventh_day()
    {
        await using var factory = new ApiFactory { AdminTelegramIds = [ApiFactory.UserB] };
        var anonymous = factory.CreateClient();
        foreach (var source in new[] { "TG", "tg", "ig", "bad source!", "" })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/api/visits", new SourceRequest(source))).StatusCode);
        }

        var learner = await factory.SignedInClientAsync(ApiFactory.UserA);
        (await learner.PostAsJsonAsync("/api/me/source", new SourceRequest("tg"))).EnsureSuccessStatusCode();
        (await learner.PostAsJsonAsync("/api/me/source", new SourceRequest("ig"))).EnsureSuccessStatusCode(); // the first one stays
        await factory.SignedInClientAsync(ApiFactory.UserB); // signed up, but came without a link

        // Seven days later the learner opens the app again.
        factory.Time.Advance(TimeSpan.FromDays(7));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var learnerId = await db.Users.Where(u => u.SignupSource == "tg").Select(u => u.Id).SingleAsync();
            db.ActivityDays.Add(new UserActivityDay { UserId = learnerId, Day = KoreaTime.Day(factory.Time.GetUtcNow()) });
            await db.SaveChangesAsync();
        }

        var admin = await factory.SignedInClientAsync(ApiFactory.UserB); // a fresh session, a week-old account
        learner = await factory.SignedInClientAsync(ApiFactory.UserA);
        (await admin.PostAsJsonAsync("/api/me/source", new SourceRequest("fb"))).EnsureSuccessStatusCode(); // too old to be "brought"
        var report = (await admin.GetFromJsonAsync<List<SourceRowDto>>("/api/admin/sources?days=30", ApiFactory.Json))!;
        var forbidden = await learner.GetAsync("/api/admin/sources");

        Assert.Equal(["tg", "ig"], report.Select(r => r.Source));
        var tg = report[0];
        Assert.Equal((2, 1, 1, 1, 0), (tg.Visits, tg.SignUps, tg.D7.Cohort, tg.D7.Returned, tg.Paying));
        Assert.Equal((1, 0), (report[1].Visits, report[1].SignUps));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
