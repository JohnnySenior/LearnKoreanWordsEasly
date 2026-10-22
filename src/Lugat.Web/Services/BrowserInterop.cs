using Microsoft.JSInterop;

namespace Lugat.Web.Services;

/// <summary>What the device check page shows about the browser.</summary>
/// <param name="Standalone">Opened as an installed app (home screen), not in a browser tab.</param>
public sealed record DeviceInfo(bool Standalone, string? ColorScheme, string UserAgent, long LoadMs);

public sealed record AudioResult(bool Ok, string? Error);

/// <summary>A finished speaking recording: 16 kHz mono WAV (D-32).</summary>
public sealed record SpeakingResult(bool Ok, string? Error, string? Wav, int DurationMs)
{
    public byte[]? WavBytes => Wav is null ? null : Convert.FromBase64String(Wav);
}

public sealed record RecordingResult(bool Ok, string? Error, string? MimeType, long Bytes, long DurationMs);

/// <summary>C# face of wwwroot/js/app.js and wwwroot/js/audio.js. The Telegram Mini App SDK is not loaded (spec §6.2).</summary>
public sealed class BrowserInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _app;
    private IJSObjectReference? _audio;

    private async ValueTask<IJSObjectReference> App() =>
        _app ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/app.js");

    private async ValueTask<IJSObjectReference> Audio() =>
        _audio ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/audio.js");

    public async Task<DeviceInfo> GetInfoAsync() => await (await App()).InvokeAsync<DeviceInfo>("getInfo");

    public async Task VibrateAsync() => await (await App()).InvokeVoidAsync("vibrate");

    public async Task<bool> CopyTextAsync(string text) => await (await App()).InvokeAsync<bool>("copyText", text);

    public async Task<string?> StorageGetAsync(string key) => await (await App()).InvokeAsync<string?>("storageGet", key);

    public async Task<bool> StorageSetAsync(string key, string value) => await (await App()).InvokeAsync<bool>("storageSet", key, value);

    public async Task StorageRemoveAsync(string key) => await (await App()).InvokeVoidAsync("storageRemove", key);

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
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        if (_audio is not null)
        {
            await _audio.DisposeAsync();
        }
    }
}

