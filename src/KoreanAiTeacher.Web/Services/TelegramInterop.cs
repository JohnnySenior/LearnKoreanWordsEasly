using Microsoft.JSInterop;

namespace KoreanAiTeacher.Web.Services;

public sealed record TelegramInfo(
    bool InsideTelegram,
    string? Platform,
    string? Version,
    string? ColorScheme,
    string? InitData,
    double? ViewportHeight,
    string UserAgent,
    long LoadMs);

public sealed record AudioResult(bool Ok, string? Error);

/// <summary>A finished speaking recording: 16 kHz mono WAV (D-32).</summary>
public sealed record SpeakingResult(bool Ok, string? Error, string? Wav, int DurationMs)
{
    public byte[]? WavBytes => Wav is null ? null : Convert.FromBase64String(Wav);
}

public sealed record RecordingResult(bool Ok, string? Error, string? MimeType, long Bytes, long DurationMs);

/// <summary>C# face of wwwroot/js/telegram.js and wwwroot/js/audio.js.</summary>
public sealed class TelegramInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _telegram;
    private IJSObjectReference? _audio;

    private async ValueTask<IJSObjectReference> Telegram() =>
        _telegram ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/telegram.js");

    private async ValueTask<IJSObjectReference> Audio() =>
        _audio ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/audio.js");

    public async Task InitAsync() => await (await Telegram()).InvokeVoidAsync("init");

    public async Task<TelegramInfo> GetInfoAsync() => await (await Telegram()).InvokeAsync<TelegramInfo>("getInfo");

    public async Task HapticAsync(string kind = "light") => await (await Telegram()).InvokeVoidAsync("haptic", kind);

    public async Task<bool> CopyTextAsync(string text) => await (await Telegram()).InvokeAsync<bool>("copyText", text);

    public async Task<string?> StorageGetAsync(string key) => await (await Telegram()).InvokeAsync<string?>("storageGet", key);

    public async Task<bool> StorageSetAsync(string key, string value) => await (await Telegram()).InvokeAsync<bool>("storageSet", key, value);

    public async Task StorageRemoveAsync(string key) => await (await Telegram()).InvokeVoidAsync("storageRemove", key);

    public async Task<string[]> SupportedRecordingTypesAsync() => await (await Audio()).InvokeAsync<string[]>("supportedMimeTypes");

    public async Task<AudioResult> PlayToneAsync() => await (await Audio()).InvokeAsync<AudioResult>("playTone");

    public async Task<RecordingResult> RecordAsync(int milliseconds) =>
        await (await Audio()).InvokeAsync<RecordingResult>("record", milliseconds);

    public async Task<AudioResult> PlayLastRecordingAsync() => await (await Audio()).InvokeAsync<AudioResult>("playLastRecording");

    public async Task LoadSpeechAsync(string key, byte[] mp3) => await (await Audio()).InvokeAsync<bool>("loadSpeech", key, mp3);

    public async Task<bool> HasSpeechAsync(string key) => await (await Audio()).InvokeAsync<bool>("hasSpeech", key);

    public async Task<AudioResult> PlaySpeechAsync(string key, double rate) => await (await Audio()).InvokeAsync<AudioResult>("playSpeech", key, rate);

    public async Task<AudioResult> SpeakWithDeviceAsync(string text, double rate) => await (await Audio()).InvokeAsync<AudioResult>("speakWithDevice", text, rate);

    public async Task<AudioResult> StartSpeakingAsync(int maxMilliseconds) => await (await Audio()).InvokeAsync<AudioResult>("startSpeaking", maxMilliseconds);

    public async Task<bool> IsSpeakingTimedOutAsync() => await (await Audio()).InvokeAsync<bool>("isSpeakingTimedOut");

    public async Task<SpeakingResult> StopSpeakingAsync(bool discard = false) => await (await Audio()).InvokeAsync<SpeakingResult>("stopSpeaking", discard);

    public async ValueTask DisposeAsync()
    {
        if (_telegram is not null)
        {
            await _telegram.DisposeAsync();
        }

        if (_audio is not null)
        {
            await _audio.DisposeAsync();
        }
    }
}

