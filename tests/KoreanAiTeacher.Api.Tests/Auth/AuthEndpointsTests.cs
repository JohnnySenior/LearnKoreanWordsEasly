using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Api.Auth;
using Microsoft.AspNetCore.Authentication.BearerToken;
using KoreanAiTeacher.Api.Tests.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace KoreanAiTeacher.Api.Tests.Auth;

public class AuthEndpointsTests
{
    private const long AllowedUser = ApiFactory.UserA;
    private const long OtherUser = 3003;

    private FakeTimeProvider _time = null!;

    private ApiFactory Factory(string environment = "Production", string botToken = InitDataBuilder.BotToken)
    {
        var factory = new ApiFactory { EnvironmentName = environment, BotToken = botToken };
        _time = factory.Time;
        return factory;
    }

    private static Task<HttpResponseMessage> SignIn(HttpClient client, string initData) =>
        client.PostAsJsonAsync("/api/auth/telegram", new TelegramAuthRequest(initData));

    [Fact]
    public async Task Valid_init_data_gives_a_token_that_opens_me()
    {
        await using var factory = Factory();
        var client = factory.CreateClient();

        var response = await SignIn(client, InitDataBuilder.Build(AllowedUser, _time.GetUtcNow()));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me");

        Assert.Equal(AllowedUser, me!.TelegramUserId);
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
        Assert.Contains("Telegram", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Stale_init_data_is_rejected()
    {
        await using var factory = Factory();

        var response = await SignIn(factory.CreateClient(), InitDataBuilder.Build(AllowedUser, _time.GetUtcNow().AddMinutes(-6)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forged_init_data_is_rejected()
    {
        await using var factory = Factory();

        var response = await SignIn(factory.CreateClient(), InitDataBuilder.Build(AllowedUser, _time.GetUtcNow(), botToken: "1:wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task User_not_on_allow_list_is_forbidden()
    {
        await using var factory = Factory();

        var response = await SignIn(factory.CreateClient(), InitDataBuilder.Build(OtherUser, _time.GetUtcNow()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Missing_bot_token_is_503()
    {
        await using var factory = Factory(botToken: "");

        var response = await SignIn(factory.CreateClient(), InitDataBuilder.Build(AllowedUser, _time.GetUtcNow()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_issues_a_new_session()
    {
        await using var factory = Factory();
        var client = factory.CreateClient();
        var first = await (await SignIn(client, InitDataBuilder.Build(AllowedUser, _time.GetUtcNow())))
            .Content.ReadFromJsonAsync<AccessTokenResponse>();

        // Well past the initData freshness window: refresh must not depend on it.
        _time.Advance(TimeSpan.FromHours(2));
        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(first!.RefreshToken));

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
        var first = await (await SignIn(client, InitDataBuilder.Build(AllowedUser, _time.GetUtcNow())))
            .Content.ReadFromJsonAsync<AccessTokenResponse>();

        _time.Advance(TimeSpan.FromDays(8));
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(first!.RefreshToken));

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
