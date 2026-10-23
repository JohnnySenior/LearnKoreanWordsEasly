using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lugat.Api.Auth;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Contracts;
using Lugat.Domain.Users;
using Lugat.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Lugat.Api.Tests.Auth;

/// <summary>Sign-in, sessions and accounts (spec §6.3, §13.2).</summary>
public class AuthEndpointsTests
{
    private const long User = ApiFactory.UserA;

    private static readonly JsonSerializerOptions Json = ApiFactory.Json;

    private FakeTimeProvider _time = null!;

    private readonly FakeGoogle _google = new();

    private ApiFactory Factory(string environment = "Production", long[]? admins = null)
    {
        var factory = new ApiFactory { EnvironmentName = environment, GoogleKeys = _google, AdminTelegramIds = admins ?? [] };
        factory.Settings["Google:ClientId"] = FakeGoogle.ClientId;
        _time = factory.Time;
        return factory;
    }

    private Task<HttpResponseMessage> TelegramAsync(HttpClient client, long id, DateTimeOffset? at = null, string botToken = TelegramWidget.BotToken) =>
        client.PostAsJsonAsync("/api/auth/telegram-login", new TelegramLoginRequest(TelegramWidget.Build(id, at ?? _time.GetUtcNow(), botToken)), Json);

    private Task<HttpResponseMessage> GoogleAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(token), Json);

    /// <summary>A client carrying the access token of a successful sign-in, and the tokens themselves.</summary>
    private static async Task<(HttpClient Client, AccessTokenResponse Tokens)> SignedInAsync(ApiFactory factory, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = (await response.Content.ReadFromJsonAsync<AccessTokenResponse>())!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return (client, tokens);
    }

    [Fact]
    public async Task Telegram_widget_sign_in_creates_the_user_and_opens_me()
    {
        await using var factory = Factory();

        var (client, _) = await SignedInAsync(factory, await TelegramAsync(factory.CreateClient(), User));
        var me = (await client.GetFromJsonAsync<MeDto>("/api/me", Json))!;

        Assert.Equal("Ali Valiyev", me.DisplayName);
        Assert.Equal(["Learner"], me.Roles);
        var login = Assert.Single(me.Logins);
        Assert.Equal((LoginProvider.Telegram, "@ali"), (login.Provider, login.Display));
        Assert.True(me.ShowRomanization);
        Assert.Equal(ThemeChoice.Device, me.Theme);
    }

    [Fact]
    public async Task One_telegram_account_never_makes_two_users()
    {
        await using var factory = Factory();
        var anonymous = factory.CreateClient();

        var first = await SignedInAsync(factory, await TelegramAsync(anonymous, User));
        var parallel = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => TelegramAsync(anonymous, User)));
        var firstId = (await first.Client.GetFromJsonAsync<MeDto>("/api/me", Json))!.Id;

        Assert.All(parallel, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(firstId, (await db.UserLogins.SingleAsync()).UserId);
    }

    [Fact]
    public async Task Wrong_or_stale_telegram_answers_are_401_and_a_missing_bot_is_503()
    {
        await using var factory = Factory();
        var client = factory.CreateClient();

        var forged = await TelegramAsync(client, User, botToken: "999:OTHER");
        var stale = await TelegramAsync(client, User, at: _time.GetUtcNow().AddHours(-25));

        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        Assert.Equal(0, await CountUsersAsync(factory));

        await using var noBot = new ApiFactory { BotToken = "" };
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await noBot.CreateClient().PostAsJsonAsync("/api/auth/telegram-login",
            new TelegramLoginRequest(TelegramWidget.Build(User, noBot.Time.GetUtcNow())), Json)).StatusCode);
    }

    [Fact]
    public async Task Google_sign_in_works_and_a_bad_token_is_401()
    {
        await using var factory = Factory();
        var client = factory.CreateClient();

        var (signedIn, _) = await SignedInAsync(factory, await GoogleAsync(client, _google.Token(_time.GetUtcNow())));
        var me = (await signedIn.GetFromJsonAsync<MeDto>("/api/me", Json))!;
        var wrongAudience = await GoogleAsync(client, _google.Token(_time.GetUtcNow(), audience: "other"));
        var expired = await GoogleAsync(client, _google.Token(_time.GetUtcNow().AddHours(-2)));

        Assert.Equal("Ali Google", me.DisplayName);
        Assert.Equal((LoginProvider.Google, "ali@example.com"), (me.Logins[0].Provider, me.Logins[0].Display));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongAudience.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal(1, await CountUsersAsync(factory));
    }

    [Fact]
    public async Task Google_without_a_client_id_is_503()
    {
        await using var factory = new ApiFactory { GoogleKeys = _google };

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(_google.Token(factory.Time.GetUtcNow())), Json);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task A_linked_google_login_signs_in_to_the_same_account()
    {
        await using var factory = Factory();
        var (client, _) = await SignedInAsync(factory, await TelegramAsync(factory.CreateClient(), User));
        var token = _google.Token(_time.GetUtcNow());

        var linked = await client.PostAsJsonAsync("/api/me/logins", new LinkLoginRequest(LoginProvider.Google, null, token), Json);
        var me = (await linked.Content.ReadFromJsonAsync<MeDto>(Json))!;
        var (viaGoogle, _) = await SignedInAsync(factory, await GoogleAsync(factory.CreateClient(), token));
        var again = await client.PostAsJsonAsync("/api/me/logins", new LinkLoginRequest(LoginProvider.Google, null, token), Json);

        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        Assert.Equal([LoginProvider.Telegram, LoginProvider.Google], me.Logins.Select(l => l.Provider));
        Assert.Equal(me.Id, (await viaGoogle.GetFromJsonAsync<MeDto>("/api/me", Json))!.Id);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode); // linking the same login again changes nothing
        Assert.Equal(1, await CountUsersAsync(factory));
    }

    [Fact]
    public async Task A_login_of_another_account_is_not_merged()
    {
        await using var factory = Factory();
        var token = _google.Token(_time.GetUtcNow());
        await SignedInAsync(factory, await GoogleAsync(factory.CreateClient(), token)); // the Google account has its own user
        var (telegramUser, _) = await SignedInAsync(factory, await TelegramAsync(factory.CreateClient(), User));

        var link = await telegramUser.PostAsJsonAsync("/api/me/logins", new LinkLoginRequest(LoginProvider.Google, null, token), Json);
        var forged = await telegramUser.PostAsJsonAsync("/api/me/logins",
            new LinkLoginRequest(LoginProvider.Google, null, _google.Token(_time.GetUtcNow(), key: FakeGoogle.NewKey("google-1"))), Json);

        Assert.Equal(HttpStatusCode.Conflict, link.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
        Assert.Single((await telegramUser.GetFromJsonAsync<MeDto>("/api/me", Json))!.Logins);
    }

    [Fact]
    public async Task Profile_settings_are_saved()
    {
        await using var factory = Factory();
        var client = await factory.SignedInClientAsync(User);

        var saved = await client.PatchAsJsonAsync("/api/me", new UpdateMeRequest(" Jamshid ", false, ThemeChoice.Dark), Json);
        var empty = await client.PatchAsJsonAsync("/api/me", new UpdateMeRequest("   ", null, null), Json);
        var me = (await saved.Content.ReadFromJsonAsync<MeDto>(Json))!;

        Assert.Equal(("Jamshid", false, ThemeChoice.Dark), (me.DisplayName, me.ShowRomanization, me.Theme));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, empty.StatusCode);
    }

    [Fact]
    public async Task Logout_ends_every_token_at_once()
    {
        await using var factory = Factory();
        var (client, tokens) = await SignedInAsync(factory, await TelegramAsync(factory.CreateClient(), User));
        var (otherDevice, _) = await SignedInAsync(factory, await TelegramAsync(factory.CreateClient(), User));

        var logout = await client.PostAsync("/api/auth/logout", null);
        var refresh = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        var (back, _) = await SignedInAsync(factory, await TelegramAsync(factory.CreateClient(), User));
        Assert.Equal(HttpStatusCode.OK, (await back.GetAsync("/api/me")).StatusCode); // signing in again works
    }

    [Fact]
    public async Task Deleting_the_account_removes_personal_data_and_ends_the_session()
    {
        await using var factory = new ApiFactory { TableWords = ApiFactory.Words("가", 5) };
        var client = await factory.SignedInClientAsync(User);
        (await client.PostAsync(ApiFactory.TestWordsPath, null)).EnsureSuccessStatusCode();
        var session = (await (await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, false), Json))
            .Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!;
        (await client.PostAsJsonAsync($"/api/sessions/{session.Id}/answers", new AnswerRequest(Guid.NewGuid(), 0, Lugat.Domain.Learning.ReviewAnswer.Easy, false, 1000), Json))
            .EnsureSuccessStatusCode();
        var oldId = (await client.GetFromJsonAsync<MeDto>("/api/me", Json))!.Id;

        var deleted = await client.DeleteAsync("/api/me");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == oldId);
            Assert.NotNull(user.DeletedAt);
            Assert.Null(user.DisplayName);
            Assert.False(await db.UserLogins.AnyAsync(l => l.UserId == oldId));
            Assert.False(await db.LearningItems.AnyAsync(w => w.OwnerId == oldId));
            Assert.False(await db.Cards.AnyAsync(c => c.OwnerId == oldId));
            Assert.False(await db.ReviewEvents.AnyAsync(e => e.OwnerId == oldId));
            Assert.False(await db.StudySessions.AnyAsync(s => s.OwnerId == oldId));
        }

        // The same Telegram account can start over: it becomes a new, empty user.
        var fresh = await factory.SignedInClientAsync(User);
        var me = (await fresh.GetFromJsonAsync<MeDto>("/api/me", Json))!;
        Assert.NotEqual(oldId, me.Id);
        Assert.Equal(0, (await fresh.GetFromJsonAsync<ProgressDto>("/api/progress", Json))!.Words); // no cards of the old account
    }

    [Fact]
    public async Task Owner_only_endpoints_need_the_admin_role()
    {
        await using var factory = Factory(admins: [User]);
        var learner = await factory.SignedInClientAsync(ApiFactory.UserB);
        var admin = await factory.SignedInClientAsync(User);

        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync("/api/backups/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.PostAsync("/api/backups/run", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync("/api/ai/budget")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.PostAsync("/api/ai/ping/OpenAi", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await learner.GetAsync("/api/ai/status")).StatusCode); // own usage stays open
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/backups/status")).StatusCode);
        Assert.Contains("Admin", (await admin.GetFromJsonAsync<MeDto>("/api/me", Json))!.Roles);
    }

    [Fact]
    public async Task A_role_taken_away_counts_on_the_next_request()
    {
        await using var factory = Factory(admins: [User]);
        var admin = await factory.SignedInClientAsync(User);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/backups/status")).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync();
            user.Roles = UserRoles.Learner;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/backups/status")).StatusCode);
    }

    [Fact]
    public async Task Behind_the_proxy_the_limit_counts_the_client_ip_the_proxy_added()
    {
        await using var factory = Factory();
        factory.Settings["Auth:SignInPerMinute"] = "1";
        factory.Settings["ForwardedHeaders:Enabled"] = "true";
        var client = factory.CreateClient();

        async Task<HttpStatusCode> FromAsync(string forwardedFor)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/telegram-login")
            {
                Content = JsonContent.Create(new TelegramLoginRequest(TelegramWidget.Build(User, _time.GetUtcNow(), TelegramWidget.BotToken)), options: Json),
            };
            request.Headers.Add("X-Forwarded-For", forwardedFor);
            return (await client.SendAsync(request)).StatusCode;
        }

        Assert.Equal(HttpStatusCode.OK, await FromAsync("203.0.113.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await FromAsync("203.0.113.1"));
        Assert.Equal(HttpStatusCode.OK, await FromAsync("203.0.113.2")); // another learner is not blocked
        // A client cannot pick its own address: only the entry the proxy added counts.
        Assert.Equal(HttpStatusCode.TooManyRequests, await FromAsync("198.51.100.7, 203.0.113.2"));
    }

    [Fact]
    public async Task A_class_on_one_network_can_refresh_after_the_sign_in_limit_is_used_up()
    {
        await using var factory = Factory();
        factory.Settings["Auth:SignInPerMinute"] = "3";
        var client = factory.CreateClient();
        var tokens = new List<AccessTokenResponse>();
        for (var i = 0; i < 3; i++)
        {
            tokens.Add((await (await TelegramAsync(client, User + i)).Content.ReadFromJsonAsync<AccessTokenResponse>())!);
        }

        // Sign-in is used up for this IP, yet all three learners still refresh their sessions (twice each).
        Assert.Equal(HttpStatusCode.TooManyRequests, (await TelegramAsync(client, User + 9)).StatusCode);
        foreach (var t in tokens.Concat(tokens))
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(t.RefreshToken))).StatusCode);
        }
    }

    [Fact]
    public async Task Sign_in_is_rate_limited_per_ip()
    {
        await using var factory = Factory();
        factory.Settings["Auth:SignInPerMinute"] = "3";
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await TelegramAsync(client, User)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task Starting_tests_is_rate_limited_per_user()
    {
        await using var factory = new ApiFactory { TableWords = ApiFactory.Words("가", 5) };
        factory.Settings["Auth:TestStartsPerHour"] = "2";
        var a = await factory.SignedInClientAsync(User);
        var b = await factory.SignedInClientAsync(ApiFactory.UserB);
        (await a.PostAsync(ApiFactory.TestWordsPath, null)).EnsureSuccessStatusCode();
        (await b.PostAsync(ApiFactory.TestWordsPath, null)).EnsureSuccessStatusCode();

        Task<HttpResponseMessage> Start(HttpClient c) => c.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [ApiFactory.Unit(1)], 10), Json);
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await Start(a)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.TooManyRequests], statuses);
        Assert.Equal(HttpStatusCode.Created, (await Start(b)).StatusCode); // another learner has their own limit
    }

    [Fact]
    public async Task Config_tells_the_sign_in_screen_what_is_available()
    {
        await using var factory = Factory();
        factory.Settings["Telegram:BotUsername"] = "lugat_bot";

        var config = (await factory.CreateClient().GetFromJsonAsync<AuthConfigDto>("/api/auth/config", Json))!;

        Assert.Equal(new AuthConfigDto("lugat_bot", FakeGoogle.ClientId, false), config);
    }

    [Fact]
    public async Task Me_without_token_is_401_with_uzbek_problem_details()
    {
        await using var factory = Factory();

        var response = await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("Kirish talab qilinadi", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Mini_app_sign_in_is_gone()
    {
        // The Telegram Mini App and its initData allow-list are not part of Lug‘at (spec §3.1).
        await using var factory = Factory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/telegram", new { initData = "user=%7B%22id%22%3A1001%7D&hash=x" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_issues_a_new_session_for_30_days()
    {
        await using var factory = Factory();
        var (_, tokens) = await SignedInAsync(factory, await TelegramAsync(factory.CreateClient(), User));

        _time.Advance(TimeSpan.FromDays(29)); // an installed PWA opened weeks later (spec §6.3)
        var refreshed = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task Garbage_refresh_token_is_401()
    {
        await using var factory = Factory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new RefreshRequest("not-a-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_refresh_token_is_401()
    {
        await using var factory = Factory();
        var (_, tokens) = await SignedInAsync(factory, await TelegramAsync(factory.CreateClient(), User));

        _time.Advance(TimeSpan.FromDays(31));
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dev_login_does_not_exist_outside_development()
    {
        await using var factory = Factory("Production");

        var response = await factory.CreateClient().PostAsync("/api/auth/dev", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Dev_login_works_in_development()
    {
        await using var factory = Factory("Development");

        var response = await factory.CreateClient().PostAsync("/api/auth/dev", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_route_is_json_404_not_spa()
    {
        await using var factory = Factory();

        var response = await factory.CreateClient().GetAsync("/api/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Health_is_public()
    {
        await using var factory = Factory();

        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<int> CountUsersAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync();
    }
}
