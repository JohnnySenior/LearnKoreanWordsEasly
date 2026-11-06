using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Contracts;

namespace Lugat.Web.Services;

public enum SessionStatus
{
    Unknown,
    SignedIn,

    /// <summary>No session: the sign-in screen is shown.</summary>
    SignedOut,
    Unavailable,
}

public sealed record SessionState(SessionStatus Status, MeDto? Me, string? Message, int? HttpStatus);

/// <summary>Outcome of an API call. <see cref="Error"/> is the server's Uzbek message, ready to show.</summary>
public sealed record ApiResult<T>(T? Value, int Status, string? Error)
{
    public bool Ok => Status is >= 200 and < 300;
}

/// <summary>
/// The learner's session (spec §6.3). The short access token lives in memory only; the refresh token is kept on the
/// device so an installed PWA stays signed in for 30 days (L-15). On 401 the refresh token is tried once
/// (spec UX07: an expired login must not lose the learner's work); if that fails too, the sign-in screen comes back.
/// </summary>
public sealed class SessionService(HttpClient http, BrowserInterop browser)
{
    private sealed record TokenResponse(string AccessToken, string RefreshToken, long ExpiresIn);

    private const string RefreshKey = "lugat.refresh";

    private string? _accessToken;
    private string? _refreshToken;
    private Task<SessionState>? _signIn;

    public SessionState State { get; private set; } = new(SessionStatus.Unknown, null, null, null);

    /// <summary>Raised when the learner signs in or out, so the layout can switch between the app and the sign-in screen.</summary>
    public event Action? Changed;

    /// <summary>A Premium feature was asked for without Premium (403 "premium-required", spec §11): show the Premium page.</summary>
    /// <summary>A Premium feature was refused; the argument is its key (PremiumFeatures.KeyOf), or null.</summary>
    public event Action<string?>? PremiumRequired;

    public Task<SessionState> EnsureSignedInAsync()
    {
        // A restore that failed only for lack of connection is tried again next time.
        if (_signIn is { IsCompleted: true } done && done.Result.Status == SessionStatus.Unavailable)
        {
            _signIn = null;
        }

        return _signIn ??= RestoreAsync();
    }

    /// <summary>Continues the session saved on this device, if there is one.</summary>
    private async Task<SessionState> RestoreAsync()
    {
        var saved = await browser.StorageGetAsync(RefreshKey);
        if (string.IsNullOrEmpty(saved))
        {
            return State = SignedOut();
        }

        using var cts = new CancellationTokenSource(DefaultTimeout);
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("api/auth/refresh", new { refreshToken = saved }, cts.Token);
        }
        catch (Exception ex) when (IsNetworkFailure(ex))
        {
            return State = new(SessionStatus.Unavailable, null, "Server bilan aloqa yo‘q. Internetni tekshirib, qayta oching.", null);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // The server turned the session down (expired, signed out everywhere, account deleted).
            await browser.StorageRemoveAsync(RefreshKey);
            return State = SignedOut();
        }

        if (!response.IsSuccessStatusCode)
        {
            // 429 or a server error says nothing about the session: it is kept and tried again (EnsureSignedInAsync).
            return State = new(SessionStatus.Unavailable, null, RestoreFailure(response.StatusCode), (int)response.StatusCode);
        }

        return await CompleteAsync(response);
    }

    private static string RestoreFailure(HttpStatusCode status) => status == HttpStatusCode.TooManyRequests
        ? "Juda ko‘p urinish bo‘ldi. Bir daqiqadan keyin qayta urinib ko‘ring — qayta kirish shart emas."
        : "Server hozir javob bermayapti. Birozdan keyin qayta urinib ko‘ring — qayta kirish shart emas.";

    public Task<SessionState> SignInWithTelegramAsync(Dictionary<string, string> data) =>
        SignInAsync(() => http.PostAsJsonAsync("api/auth/telegram-login", new TelegramLoginRequest(data), Json));

    public Task<SessionState> SignInWithGoogleAsync(string idToken) =>
        SignInAsync(() => http.PostAsJsonAsync("api/auth/google", new GoogleLoginRequest(idToken), Json));

    /// <summary>Development only: the server has no such endpoint elsewhere.</summary>
    public Task<SessionState> DevSignInAsync() => SignInAsync(() => http.PostAsync("api/auth/dev", null));

    private async Task<SessionState> SignInAsync(Func<Task<HttpResponseMessage>> post)
    {
        HttpResponseMessage response;
        try
        {
            response = await post();
        }
        catch (Exception ex) when (IsNetworkFailure(ex))
        {
            return new(SessionStatus.Unavailable, null, "Server bilan aloqa yo‘q. Internetni tekshirib, qayta urinib ko‘ring.", null);
        }

        if (!response.IsSuccessStatusCode)
        {
            return new(SessionStatus.SignedOut, null, await ReadDetailAsync(response) ?? "Kirish tasdiqlanmadi.", (int)response.StatusCode);
        }

        var state = await CompleteAsync(response);
        _signIn = Task.FromResult(state);
        Changed?.Invoke();
        return state;
    }

    private async Task<SessionState> CompleteAsync(HttpResponseMessage response)
    {
        await StoreTokensAsync((await response.Content.ReadFromJsonAsync<TokenResponse>())!);
        MeDto? me = null;
        try
        {
            me = await GetFromJsonAsync<MeDto>("api/me");
        }
        catch (Exception ex) when (IsNetworkFailure(ex))
        {
            // Signed in; the name is only for the greeting.
        }

        return State = new(SessionStatus.SignedIn, me, null, 200);
    }

    /// <summary>Loads the account again after it changed (name, linked logins, settings).</summary>
    public async Task<MeDto?> ReloadMeAsync()
    {
        var me = await GetAsync<MeDto>("api/me");
        if (me.Value is { } value)
        {
            State = State with { Me = value };
            Changed?.Invoke();
        }

        return me.Value;
    }

    /// <summary>
    /// Signs out on every device: the server stops accepting all tokens issued so far (L-13). This device forgets the
    /// session either way; when the server did not confirm, the sign-in screen says that other devices may still be
    /// signed in. True when the server confirmed.
    /// </summary>
    public async Task<bool> LogoutAsync()
    {
        var result = await SendJsonAsync<object>(HttpMethod.Post, "api/auth/logout");
        // 401: the session was already gone on the server, so there is nothing left to end.
        var everywhere = result.Ok || result.Status == (int)HttpStatusCode.Unauthorized;
        await ForgetAsync(everywhere ? null : LogoutNotConfirmed);
        return everywhere;
    }

    public const string LogoutNotConfirmed =
        "Bu qurilmadan chiqdingiz, lekin server bilan aloqa bo‘lmagani uchun boshqa qurilmalardagi kirishlar bekor qilinmadi. "
        + "Ularni ham yopish uchun qayta kirib, yana \"Chiqish\" ni bosing.";

    /// <summary>Deletes the account on the server (spec §5.3), then forgets it here.</summary>
    public async Task<ApiResult<object>> DeleteAccountAsync()
    {
        var result = await SendJsonAsync<object>(HttpMethod.Delete, "api/me");
        if (result.Ok)
        {
            await ForgetAsync();
        }

        return result;
    }

    private async Task ForgetAsync(string? message = null)
    {
        _accessToken = null;
        _refreshToken = null;
        await browser.StorageRemoveAsync(RefreshKey);
        State = SignedOut() with { Message = message };
        _signIn = Task.FromResult(State);
        Changed?.Invoke();
    }

    private static SessionState SignedOut() => new(SessionStatus.SignedOut, null, null, null);

    /// <summary>
    /// A dropped connection (HttpRequestException) or a request that timed out (TaskCanceledException)
    /// is shown as "no connection", never as a crash (spec REL03).
    /// </summary>
    private static bool IsNetworkFailure(Exception ex) => ex is HttpRequestException or TaskCanceledException;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>AI checks may try two services one after another.</summary>
    public static readonly TimeSpan AiTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Long operations such as a backup run.</summary>
    public static readonly TimeSpan UploadTimeout = TimeSpan.FromMinutes(10);

    /// <summary>A call that needs no session (the sign-in screen's configuration).</summary>
    public async Task<ApiResult<T>> GetAnonymousAsync<T>(string uri)
    {
        using var cts = new CancellationTokenSource(DefaultTimeout);
        try
        {
            using var response = await http.GetAsync(uri, cts.Token);
            return response.IsSuccessStatusCode
                ? new ApiResult<T>(await response.Content.ReadFromJsonAsync<T>(Json, cts.Token), (int)response.StatusCode, null)
                : new ApiResult<T>(default, (int)response.StatusCode, await ReadDetailAsync(response, cts.Token) ?? "Kutilmagan xato yuz berdi.");
        }
        catch (Exception ex) when (IsNetworkFailure(ex))
        {
            return new ApiResult<T>(default, 0, "Server bilan aloqa yo‘q. Internetni tekshirib, qayta urinib ko‘ring.");
        }
    }

    /// <summary>A small report that needs no session (an ad link visit, L-40); failures are ignored by the caller.</summary>
    public async Task<bool> PostAnonymousAsync(string uri, object body)
    {
        using var cts = new CancellationTokenSource(DefaultTimeout);
        try
        {
            using var response = await http.PostAsJsonAsync(uri, body, Json, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (IsNetworkFailure(ex))
        {
            return false;
        }
    }

    public Task<ApiResult<T>> GetAsync<T>(string uri) =>
        CallAsync<T>(() => new HttpRequestMessage(HttpMethod.Get, uri), DefaultTimeout);

    public Task<ApiResult<T>> SendJsonAsync<T>(HttpMethod method, string uri, object? body = null, TimeSpan? timeout = null) =>
        CallAsync<T>(() => new HttpRequestMessage(method, uri)
        {
            Content = body is null ? null : JsonContent.Create(body, options: Json),
        }, timeout ?? DefaultTimeout);

    /// <summary>Binary download (pronunciation MP3). Null with the error text when it failed.</summary>
    public async Task<ApiResult<byte[]>> GetBytesAsync(string uri)
    {
        await EnsureSignedInAsync();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        try
        {
            using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, uri), cts.Token);
            if (response.IsSuccessStatusCode)
            {
                return new ApiResult<byte[]>(await response.Content.ReadAsByteArrayAsync(cts.Token), (int)response.StatusCode, null);
            }

            return new ApiResult<byte[]>(null, (int)response.StatusCode, await ReadDetailAsync(response, cts.Token) ?? "Ovoz yuklanmadi.");
        }
        catch (Exception ex) when (IsNetworkFailure(ex))
        {
            return new ApiResult<byte[]>(null, 0, "Server bilan aloqa yo‘q.");
        }
    }

    /// <summary>A speaking recording (WAV) and, for pronunciation, the text that should have been said (D-32).</summary>
    public Task<ApiResult<T>> PostSpeechAsync<T>(string uri, byte[] wav, string? reference) =>
        PostSpeechAsync<T>(uri, wav, reference is null ? [] : new Dictionary<string, string> { ["reference"] = reference });

    /// <summary>A WAV recording with extra form fields (e.g. "assisted" for an oral test answer, D-38).</summary>
    public Task<ApiResult<T>> PostSpeechAsync<T>(string uri, byte[] wav, IReadOnlyDictionary<string, string> fields) =>
        CallAsync<T>(() =>
        {
            var audio = new ByteArrayContent(wav);
            audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            var form = new MultipartFormDataContent { { audio, "audio", "speech.wav" } };
            foreach (var (name, value) in fields)
            {
                form.Add(new StringContent(value), name);
            }

            return new HttpRequestMessage(HttpMethod.Post, uri) { Content = form };
        }, AiTimeout);

    /// <summary>Multipart upload. The bytes are kept so the request can be replayed after a token refresh.</summary>
    public Task<ApiResult<T>> UploadAsync<T>(string uri, byte[] content, string fileName, string contentType) =>
        CallAsync<T>(() =>
        {
            var file = new ByteArrayContent(content);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new MultipartFormDataContent { { file, "file", fileName } },
            };
        }, UploadTimeout);

    private async Task<ApiResult<T>> CallAsync<T>(Func<HttpRequestMessage> create, TimeSpan timeout)
    {
        await EnsureSignedInAsync();
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            using var response = await SendAsync(create, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var value = response.Content.Headers.ContentLength == 0
                    ? default
                    : await response.Content.ReadFromJsonAsync<T>(Json, cts.Token);
                return new ApiResult<T>(value, (int)response.StatusCode, null);
            }

            var (detail, type) = await ReadProblemAsync(response, cts.Token);
            if (type == "premium-required")
            {
                PremiumRequired?.Invoke(PremiumFeatures.KeyOf(response.RequestMessage?.RequestUri?.AbsolutePath));
            }

            return new ApiResult<T>(default, (int)response.StatusCode, detail ?? "Kutilmagan xato yuz berdi. Qayta urinib ko‘ring.");
        }
        catch (Exception ex) when (IsNetworkFailure(ex))
        {
            return new ApiResult<T>(default, 0, "Server bilan aloqa yo‘q yoki javob kechikdi. Internetni tekshirib, qayta urinib ko‘ring.");
        }
    }

    private async Task<T?> GetFromJsonAsync<T>(string uri)
    {
        using var cts = new CancellationTokenSource(DefaultTimeout);
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, uri), cts.Token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Json);
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> create, CancellationToken ct)
    {
        var response = await http.SendAsync(WithToken(create()), ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized || _refreshToken is null)
        {
            return response;
        }

        var refreshed = await http.PostAsJsonAsync("api/auth/refresh", new { refreshToken = _refreshToken }, ct);
        if (!refreshed.IsSuccessStatusCode)
        {
            if (refreshed.StatusCode == HttpStatusCode.Unauthorized)
            {
                await ForgetAsync(); // signed out elsewhere, deleted, or expired: back to the sign-in screen
            }

            return response;
        }

        await StoreTokensAsync((await refreshed.Content.ReadFromJsonAsync<TokenResponse>())!);
        return await http.SendAsync(WithToken(create()), ct);
    }

    private HttpRequestMessage WithToken(HttpRequestMessage request)
    {
        if (_accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        return request;
    }

    private async Task StoreTokensAsync(TokenResponse tokens)
    {
        _accessToken = tokens.AccessToken;
        _refreshToken = tokens.RefreshToken;
        await browser.StorageSetAsync(RefreshKey, tokens.RefreshToken);
    }

    private static async Task<(string? Detail, string? Type)> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            return (root.TryGetProperty("detail", out var d) ? d.GetString() : null, root.TryGetProperty("type", out var t) ? t.GetString() : null);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static async Task<string?> ReadDetailAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
