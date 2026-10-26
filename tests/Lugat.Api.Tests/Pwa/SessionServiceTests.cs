using System.Net;
using System.Text;
using Lugat.Web.Services;
using Microsoft.JSInterop;

namespace Lugat.Api.Tests.Pwa;

/// <summary>
/// The Blazor client's session (audit 2026-10-26): a refresh that fails for a passing reason keeps the saved login,
/// and a sign-out the server did not confirm says so.
/// </summary>
public class SessionServiceTests
{
    private const string RefreshKey = "lugat.refresh";

    [Fact]
    public async Task A_rejected_refresh_forgets_the_saved_login()
    {
        var (session, storage, server) = Create("saved");
        server.Refresh = HttpStatusCode.Unauthorized;

        var state = await session.EnsureSignedInAsync();

        Assert.Equal(SessionStatus.SignedOut, state.Status);
        Assert.False(storage.ContainsKey(RefreshKey));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_refresh_that_fails_for_a_passing_reason_keeps_the_login_and_recovers_without_signing_in(HttpStatusCode status)
    {
        var (session, storage, server) = Create("saved");
        server.Refresh = status;

        var down = await session.EnsureSignedInAsync();

        Assert.Equal(SessionStatus.Unavailable, down.Status);
        Assert.Equal((int)status, down.HttpStatus);
        Assert.Contains("qayta kirish shart emas", down.Message);
        Assert.Equal("saved", storage[RefreshKey]);

        server.Refresh = HttpStatusCode.OK; // the server is back
        var up = await session.EnsureSignedInAsync();

        Assert.Equal(SessionStatus.SignedIn, up.Status);
        Assert.Equal("Ali", up.Me?.DisplayName);
        Assert.Equal("refresh-2", storage[RefreshKey]);
        Assert.Equal(2, server.RefreshCalls);
    }

    [Fact]
    public async Task A_dropped_connection_on_restore_keeps_the_login()
    {
        var (session, storage, server) = Create("saved");
        server.RefreshThrows = true;

        var state = await session.EnsureSignedInAsync();

        Assert.Equal(SessionStatus.Unavailable, state.Status);
        Assert.Equal("saved", storage[RefreshKey]);
    }

    [Fact]
    public async Task A_confirmed_sign_out_forgets_the_login_without_a_warning()
    {
        var (session, storage, server) = Create("saved");
        await session.EnsureSignedInAsync();

        var everywhere = await session.LogoutAsync();

        Assert.True(everywhere);
        Assert.Equal(1, server.LogoutCalls);
        Assert.Equal(SessionStatus.SignedOut, session.State.Status);
        Assert.Null(session.State.Message);
        Assert.False(storage.ContainsKey(RefreshKey));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    [InlineData(HttpStatusCode.OK, true)] // OK here stands for "no connection": the request throws
    public async Task A_sign_out_the_server_did_not_confirm_signs_out_here_and_says_other_devices_stay_signed_in(HttpStatusCode status, bool throws)
    {
        var (session, storage, server) = Create("saved");
        await session.EnsureSignedInAsync();
        server.Logout = status;
        server.LogoutThrows = throws;

        var everywhere = await session.LogoutAsync();

        Assert.False(everywhere);
        Assert.Equal(SessionStatus.SignedOut, session.State.Status);
        Assert.Equal(SessionService.LogoutNotConfirmed, session.State.Message);
        Assert.False(storage.ContainsKey(RefreshKey));
    }

    private static (SessionService, Dictionary<string, string>, FakeServer) Create(string? savedRefresh)
    {
        var storage = new Dictionary<string, string>();
        if (savedRefresh is not null)
        {
            storage[RefreshKey] = savedRefresh;
        }

        var server = new FakeServer();
        var http = new HttpClient(server) { BaseAddress = new Uri("https://koreyscha.test/") };
        return (new SessionService(http, new BrowserInterop(new FakeJs(storage))), storage, server);
    }

    private sealed class FakeServer : HttpMessageHandler
    {
        public HttpStatusCode Refresh { get; set; } = HttpStatusCode.OK;
        public bool RefreshThrows { get; set; }
        public HttpStatusCode Logout { get; set; } = HttpStatusCode.NoContent;
        public bool LogoutThrows { get; set; }
        public int RefreshCalls { get; private set; }
        public int LogoutCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/api/auth/refresh":
                    RefreshCalls++;
                    if (RefreshThrows)
                    {
                        throw new HttpRequestException("offline");
                    }

                    return Task.FromResult(Refresh == HttpStatusCode.OK
                        ? Json(HttpStatusCode.OK, "{\"accessToken\":\"access\",\"refreshToken\":\"refresh-2\",\"expiresIn\":3600}")
                        : Json(Refresh, "{\"detail\":\"x\"}"));
                case "/api/me":
                    return Task.FromResult(Json(HttpStatusCode.OK, "{\"displayName\":\"Ali\"}"));
                case "/api/auth/logout":
                    LogoutCalls++;
                    if (LogoutThrows)
                    {
                        throw new HttpRequestException("offline");
                    }

                    return Task.FromResult(new HttpResponseMessage(Logout) { Content = new StringContent("") });
                default:
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    /// <summary>wwwroot/js/app.js storage over a dictionary.</summary>
    private sealed class FakeJs(Dictionary<string, string> storage) : IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => identifier switch
        {
            "import" => new((TValue)(object)this),
            "storageGet" => new((TValue)(object?)storage.GetValueOrDefault((string)args![0]!)!),
            "storageSet" => Set((string)args![0]!, (string)args[1]!, out var ok) ? new((TValue)(object)ok) : default,
            "storageRemove" => Remove<TValue>((string)args![0]!),
            _ => throw new NotSupportedException(identifier),
        };

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        private bool Set(string key, string value, out bool ok)
        {
            storage[key] = value;
            return ok = true;
        }

        private ValueTask<TValue> Remove<TValue>(string key)
        {
            storage.Remove(key);
            return default;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
