using System.Net.Http.Headers;
using System.Security;
using System.Text;
using Lugat.Application.Ai;
using Lugat.Application.Speech;
using Lugat.Domain.Ai;
using Microsoft.Extensions.Options;

namespace Lugat.Infrastructure.Speech;

/// <summary>Microsoft Azure Speech neural voices (REST, SSML): natural native Korean (D-31).</summary>
public sealed class AzureSpeechSynthesizer(HttpClient http, IOptions<SpeechOptions> options) : ISpeechSynthesizer
{
    private AzureSpeechOptions Options => options.Value.Azure;

    public bool IsConfigured => Options.IsConfigured;

    public AiProviderKind Kind => AiProviderKind.Azure;

    public string Model => Options.Voice;

    public string DialogueModel => $"{Options.Voice}+{Options.MaleVoice}";

    public Task<byte[]> SynthesizeAsync(string text, CancellationToken ct) =>
        SendAsync($"""<speak version="1.0" xml:lang="ko-KR"><voice name="{SecurityElement.Escape(Options.Voice)}">{SecurityElement.Escape(text)}</voice></speak>""", ct);

    /// <summary>One SSML document with a voice element per turn, so the dialogue is a single recording (D-53).</summary>
    public Task<byte[]> SynthesizeDialogueAsync(IReadOnlyList<SpeechLine> lines, CancellationToken ct) =>
        SendAsync(DialogueSsml(lines, Options.Voice, Options.MaleVoice), ct);

    public static string DialogueSsml(IReadOnlyList<SpeechLine> lines, string female, string male)
    {
        var sb = new StringBuilder("""<speak version="1.0" xml:lang="ko-KR">""");
        for (var i = 0; i < lines.Count; i++)
        {
            var pause = i == 0 ? "" : """<break time="700ms"/>""";
            sb.Append($"""<voice name="{SecurityElement.Escape(lines[i].Male ? male : female)}">{pause}{SecurityElement.Escape(lines[i].Text)}</voice>""");
        }

        return sb.Append("</speak>").ToString();
    }

    private async Task<byte[]> SendAsync(string ssml, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"https://{Options.Region!.Trim()}.tts.speech.microsoft.com/cognitiveservices/v1")
        {
            Content = new StringContent(ssml, Encoding.UTF8),
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/ssml+xml") { CharSet = "utf-8" };
        message.Headers.Add("Ocp-Apim-Subscription-Key", Options.Key);
        message.Headers.Add("X-Microsoft-OutputFormat", "audio-24khz-48kbitrate-mono-mp3");
        message.Headers.UserAgent.ParseAdd("topik-yordamchi");
        using var response = await http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiProviderException($"Azure ovoz: xato {(int)response.StatusCode}", transient: (int)response.StatusCode is 429 or >= 500);
        }

        var audio = await response.Content.ReadAsByteArrayAsync(ct);
        return audio.Length == 0 ? throw new AiProviderException("Azure ovoz: bo‘sh javob", transient: true) : audio;
    }
}

/// <summary>Azure when configured (or chosen), otherwise OpenAI (D-31).</summary>
public sealed class SpeechSynthesizerChooser(AzureSpeechSynthesizer azure, OpenAiSpeechSynthesizer openAi, IOptions<SpeechOptions> options) : ISpeechSynthesizer
{
    private ISpeechSynthesizer Current => options.Value.Provider?.Trim().ToLowerInvariant() switch
    {
        "openai" => openAi,
        "azure" => azure,
        _ => azure.IsConfigured ? azure : openAi,
    };

    public bool IsConfigured => Current.IsConfigured;

    public AiProviderKind Kind => Current.Kind;

    public string Model => Current.Model;

    public string DialogueModel => Current.DialogueModel;

    public Task<byte[]> SynthesizeAsync(string text, CancellationToken ct) => Current.SynthesizeAsync(text, ct);

    public Task<byte[]> SynthesizeDialogueAsync(IReadOnlyList<SpeechLine> lines, CancellationToken ct) => Current.SynthesizeDialogueAsync(lines, ct);
}
