using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lugat.Contracts;
using Lugat.Api.Auth;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Lugat.Api.Tests.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace Lugat.Api.Tests.Auth;

public class AuthEndpointsTests
{
    private const long User = ApiFactory.UserA;

    private FakeTimeProvider _time = null!;

    private ApiFactory Factory(string environment = "Production")
    {
        var factory = new ApiFactory { EnvironmentName = environment };
        _time = factory.Time;
        return factory;
    }

    /// <summary>The bearer tokens of a fresh sign-in, as a sign-in provider would return them.</summary>
    private static async Task<AccessTokenResponse> TokensAsync(HttpClient client) =>
        (await (await client.PostAsync($"{ApiFactory.TestSignInPath}?tg={User}", null)).Content.ReadFromJsonAsync<AccessTokenResponse>())!;

    [Fact]
    public async Task Token_opens_me()
    {
        await using var factory = Factory();

        var me = await (await factory.SignedInClientAsync(User, "Ali")).GetFromJsonAsync<MeResponse>("/api/me");

        Assert.Equal(User, me!.TelegramUserId);
        Assert.Equal("Ali", me.FirstName);
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
    public async Task Refresh_token_issues_a_new_session()
    {
        await using var factory = Factory();
        var client = factory.CreateClient();
        var first = await TokensAsync(client);

        _time.Advance(TimeSpan.FromHours(2)); // past the 1-hour access token
        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(first.RefreshToken));

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
        var client = factory.CreateClient();
        var first = await TokensAsync(client);

        _time.Advance(TimeSpan.FromDays(8));
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(first.RefreshToken));

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
}
