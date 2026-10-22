using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KoreanAiTeacher.Contracts;

namespace KoreanAiTeacher.Web.Services;

public enum SessionStatus
{
    Unknown,
    SignedIn,
    NotInTelegram,
    Rejected,
    Forbidden,
    Unavailable,
}

public sealed record SessionState(SessionStatus Status, MeResponse? Me, string? Message, int? HttpStatus);

/// <summary>Outcome of an API call. <see cref="Error"/> is the server's Uzbek message, ready to show.</summary>
public sealed record ApiResult<T>(T? Value, int Status, string? Error)
{
    public bool Ok => Status is >= 200 and < 300;
}

/// <summary>
/// Exchanges Telegram initData for a short server session and keeps tokens in memory only.
/// On 401 it tries the refresh token once (spec UX07: an expired login must not lose the learner's work).
/// </summary>
public sealed class SessionService(HttpClient http, TelegramInterop telegram)
{
    private sealed record TokenResponse(string AccessToken, string RefreshToken, long ExpiresIn);

    private string? _accessToken;
    private string? _refreshToken;
    private Task<SessionState>? _signIn;

    public SessionState State { get; private set; } = new(SessionStatus.Unknown, null, null, null);

    public Task<SessionState> EnsureSignedInAsync()
    {
        // A sign-in that failed only for lack of connection is tried again next time.
        if (_signIn is { IsCompleted: true } done && done.Result.Status == SessionStatus.Unavailable)
        {
            _signIn = null;
        }

        return _signIn ??= SignInAsync();
    }

    private async Task<SessionState> SignInAsync()
    {
        var info = await telegram.GetInfoAsync();
        HttpResponseMessage response;
        using var cts = new CancellationTokenSource(DefaultTimeout);
        try
        {
            response = info.InsideTelegram
                ? await http.PostAsJsonAsync("api/auth/telegram", new { initData = info.InitData }, cts.Token)
                : await http.PostAsync("api/auth/dev", null, cts.Token); // exists only in Development
        }
        catch (Exception ex) when (IsNetworkFailure(ex))
        {
            return State = new(SessionStatus.Unavailable, null, "Server bilan aloqa yo‘q. Internetni tekshirib, qayta oching.", null);
        }

        if (!info.InsideTelegram && response.StatusCode == HttpStatusCode.NotFound)
        {
            return State = new(SessionStatus.NotInTelegram, null, "Ilovani Telegram ichidan oching.", 404);
        }

        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode switch
            {
                HttpStatusCode.Forbidden => SessionStatus.Forbidden,
                HttpStatusCode.Unauthorized => SessionStatus.Rejected,
                _ => SessionStatus.Unavailable,
            };
            return State = new(status, null, await ReadDetailAsync(response), (int)response.StatusCode);
        }

        StoreTokens((await response.Content.ReadFromJsonAsync<TokenResponse>())!);
        MeResponse? me = null;
        try
        {
            me = await GetFromJsonAsync<MeResponse>("api/me");
        }
        catch (Exception ex) when (IsNetworkFailure(ex))
        {
            // Signed in; the name is only for the greeting.
        }

        return State = new(SessionStatus.SignedIn, me, null, 200);
    }

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

    /// <summary>A 43 MB book over a phone connection.</summary>
    public static readonly TimeSpan UploadTimeout = TimeSpan.FromMinutes(10);

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

            var detail = await ReadDetailAsync(response, cts.Token) ?? "Kutilmagan xato yuz berdi. Qayta urinib ko‘ring.";
            return new ApiResult<T>(default, (int)response.StatusCode, detail);
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
            return response;
        }

        StoreTokens((await refreshed.Content.ReadFromJsonAsync<TokenResponse>())!);
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

    private void StoreTokens(TokenResponse tokens)
    {
        _accessToken = tokens.AccessToken;
        _refreshToken = tokens.RefreshToken;
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
