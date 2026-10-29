using System.Net.Http.Json;
using Lugat.Application;
using Lugat.Application.Catalog;
using Microsoft.Extensions.Options;

namespace Lugat.Infrastructure.Speech;

public sealed class ElevenLabsOptions
{
    public string? ApiKey { get; set; }
    public string? VoiceId { get; set; }
    public string Model { get; set; } = "eleven_v3";
}

/// <summary>Each board beat is synthesized separately, after human script approval. Keys never reach the client.</summary>
public sealed class ElevenLabsNarrator(HttpClient http, IOptions<ElevenLabsOptions> options) : IIntroductionNarrator
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey) && !string.IsNullOrWhiteSpace(options.Value.VoiceId);
    public string Model => options.Value.Model;
    public string Profile => $"ElevenLabs/{Model}/{options.Value.VoiceId}";
    public async Task<byte[]> SynthesizeAsync(string text, CancellationToken ct)
    {
        if (!IsConfigured) throw new ServiceUnavailableException("ElevenLabs sozlanmagan.");
        using var message = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(options.Value.VoiceId!)}?output_format=mp3_44100_128")
        {
            Content = JsonContent.Create(new { text, model_id = Model, language_code = "uz" }),
        };
        message.Headers.Add("xi-api-key", options.Value.ApiKey);
        message.Headers.Accept.ParseAdd("audio/mpeg");
        using var response = await http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
            throw new ServiceUnavailableException($"ElevenLabs ovozni tayyorlamadi ({(int)response.StatusCode}). Sozlamalar va limitni tekshiring.");
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length < 3 || !(bytes.AsSpan().StartsWith("ID3"u8) || bytes[0] == 0xff && (bytes[1] & 0xe0) == 0xe0))
            throw new ServiceUnavailableException("ElevenLabs yaroqli MP3 ovoz qaytarmadi.");
        return bytes;
    }
}
