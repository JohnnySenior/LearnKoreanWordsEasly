using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Lugat.Application.Ai;
using Lugat.Application.Speech;
using Lugat.Domain.Ai;
using Microsoft.Extensions.Options;

namespace Lugat.Infrastructure.Speech;

/// <summary>OpenAI text-to-speech (POST v1/audio/speech), with the same key as the ChatGPT provider (D-29).</summary>
public sealed class OpenAiSpeechSynthesizer(HttpClient http, IOptions<AiOptions> ai, IOptions<SpeechOptions> options) : ISpeechSynthesizer
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ai.Value.OpenAi.ApiKey) && !string.IsNullOrWhiteSpace(options.Value.Model);

    public AiProviderKind Kind => AiProviderKind.OpenAi;

    public string Model => $"{options.Value.Model}/{options.Value.Voice}";

    public string DialogueModel => $"{options.Value.Model}/{options.Value.Voice}+{options.Value.MaleVoice}";

    public Task<byte[]> SynthesizeAsync(string text, CancellationToken ct) => SynthesizeAsync(text, options.Value.Voice, ct);

    /// <summary>
    /// OpenAI has one voice per request: each turn is recorded alone and the MP3s are joined (MP3 frames can be
    /// concatenated), without a pause between turns. Azure is preferred for dialogues (D-53).
    /// </summary>
    public async Task<byte[]> SynthesizeDialogueAsync(IReadOnlyList<SpeechLine> lines, CancellationToken ct)
    {
        using var all = new MemoryStream();
        foreach (var line in lines)
        {
            var part = await SynthesizeAsync(line.Text, line.Male ? options.Value.MaleVoice : options.Value.Voice, ct);
            all.Write(part);
        }

        return all.ToArray();
    }

    private async Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = options.Value.Model,
            ["voice"] = voice,
            ["input"] = text,
            ["response_format"] = "mp3",
        };
        if (!options.Value.Model.StartsWith("tts-1", StringComparison.Ordinal))
        {
            body["instructions"] = "Read the Korean text naturally and clearly, in standard Seoul pronunciation, like a patient teacher.";
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(ai.Value.OpenAi.BaseUrl), "v1/audio/speech"))
        {
            Content = JsonContent.Create(body),
        };
        message.Headers.Authorization = new("Bearer", ai.Value.OpenAi.ApiKey);
        using var response = await http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiProviderException($"OpenAI ovoz: xato {(int)response.StatusCode}", transient: (int)response.StatusCode is 429 or >= 500);
        }

        var audio = await response.Content.ReadAsByteArrayAsync(ct);
        return audio.Length == 0 ? throw new AiProviderException("OpenAI ovoz: bo‘sh javob", transient: true) : audio;
    }
}
