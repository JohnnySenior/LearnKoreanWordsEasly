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
        // "uz" pins the language where the model accepts it; a model that does not answers 400, and is asked again
        // without it (Uzbek Latin text is detected on its own).
        var (status, body, bytes) = await SendAsync(text, "uz", ct);
        if (status == 400) (status, body, bytes) = await SendAsync(text, null, ct);
        if (status is not (>= 200 and < 300))
            throw new ServiceUnavailableException($"ElevenLabs ovozni tayyorlamadi ({status}{Detail(body)}). Sozlamalar va limitni tekshiring.");
        if (bytes.Length < 3 || !(bytes.AsSpan().StartsWith("ID3"u8) || bytes[0] == 0xff && (bytes[1] & 0xe0) == 0xe0))
            throw new ServiceUnavailableException("ElevenLabs yaroqli MP3 ovoz qaytarmadi.");
        return bytes;
    }

    private async Task<(int Status, string? Error, byte[] Audio)> SendAsync(string text, string? language, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(options.Value.VoiceId!)}?output_format=mp3_44100_128")
        {
            Content = language is null
                ? JsonContent.Create(new { text, model_id = Model })
                : JsonContent.Create(new { text, model_id = Model, language_code = language }),
        };
        message.Headers.Add("xi-api-key", options.Value.ApiKey);
        message.Headers.Accept.ParseAdd("audio/mpeg");
        using var response = await http.SendAsync(message, ct);
        return response.IsSuccessStatusCode
            ? ((int)response.StatusCode, null, await response.Content.ReadAsByteArrayAsync(ct))
            : ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct), []);
    }

    /// <summary>ElevenLabs' own reason ({"detail":{"status":…,"message":…}}), short; it never holds the key.</summary>
    private static string Detail(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        string text;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var detail = doc.RootElement.TryGetProperty("detail", out var d) ? d : doc.RootElement;
            text = detail.ValueKind == System.Text.Json.JsonValueKind.Object
                ? string.Join(": ", new[] { "status", "message" }.Select(k => detail.TryGetProperty(k, out var v) ? v.ToString() : null).Where(v => !string.IsNullOrEmpty(v)))
                : detail.ToString();
        }
        catch (System.Text.Json.JsonException)
        {
            text = body;
        }

        text = text.Trim();
        return text.Length == 0 ? "" : ": " + (text.Length > 200 ? text[..200] + "…" : text);
    }
}
