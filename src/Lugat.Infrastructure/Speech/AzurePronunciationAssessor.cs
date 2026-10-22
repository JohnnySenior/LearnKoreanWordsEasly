using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lugat.Application.Ai;
using Lugat.Application.Speech;
using Lugat.Contracts;
using Microsoft.Extensions.Options;

namespace Lugat.Infrastructure.Speech;

/// <summary>
/// Azure Speech short-audio recognition (REST, ko-KR) with the Pronunciation-Assessment header (D-32). Same key and
/// region as the voice (D-31).
/// </summary>
public sealed class AzurePronunciationAssessor(HttpClient http, IOptions<SpeechOptions> options) : IPronunciationAssessor
{
    private AzureSpeechOptions Options => options.Value.Azure;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Options.Key) && !string.IsNullOrWhiteSpace(Options.Region);

    public async Task<SpeakingResultDto> AssessAsync(byte[] wav, string? referenceText, CancellationToken ct)
    {
        var url = $"https://{Options.Region!.Trim()}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language=ko-KR&format=detailed";
        using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(wav) };
        // Azure's documented value has an unquoted "/" in a parameter, which the typed header parser rejects.
        message.Content.Headers.TryAddWithoutValidation("Content-Type", "audio/wav; codecs=audio/pcm; samplerate=16000");
        message.Headers.Add("Ocp-Apim-Subscription-Key", Options.Key);
        message.Headers.Accept.ParseAdd("application/json");
        if (referenceText is not null)
        {
            var config = new JsonObject
            {
                ["ReferenceText"] = referenceText,
                ["GradingSystem"] = "HundredMark",
                ["Granularity"] = "Word",
                ["Dimension"] = "Comprehensive",
                ["EnableMiscue"] = true,
            };
            message.Headers.Add("Pronunciation-Assessment", Convert.ToBase64String(Encoding.UTF8.GetBytes(config.ToJsonString())));
        }

        using var response = await http.SendAsync(message, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiProviderException($"Azure nutq: xato {(int)response.StatusCode}", transient: (int)response.StatusCode is 429 or >= 500);
        }

        return Parse(body);
    }

    /// <summary>Reads scores both where the REST API puts them and inside "PronunciationAssessment" (older shape).</summary>
    public static SpeakingResultDto Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var status = root.TryGetProperty("RecognitionStatus", out var s) ? s.GetString() : null;
        if (status != "Success" || !root.TryGetProperty("NBest", out var nbest) || nbest.GetArrayLength() == 0)
        {
            var message = status switch
            {
                "InitialSilenceTimeout" or "BabbleTimeout" => "Ovoz eshitilmadi. Mikrofonga yaqinroq, balandroq gapiring.",
                "NoMatch" => "Nutq tanilmadi. Aniqroq va koreyscha ayting.",
                _ => "Nutq tanilmadi. Qayta urinib ko‘ring.",
            };
            return new SpeakingResultDto(false, "", null, null, null, null, [], message);
        }

        var best = nbest[0];
        var text = root.TryGetProperty("DisplayText", out var d) ? d.GetString() ?? "" : "";
        var words = new List<PronunciationWordDto>();
        if (best.TryGetProperty("Words", out var list))
        {
            foreach (var w in list.EnumerateArray())
            {
                var word = w.TryGetProperty("Word", out var wt) ? wt.GetString() ?? "" : "";
                words.Add(new PronunciationWordDto(word, Score(w, "AccuracyScore"), Text(w, "ErrorType") ?? "None"));
            }
        }

        return new SpeakingResultDto(
            true,
            text,
            Score(best, "PronScore"),
            Score(best, "AccuracyScore"),
            Score(best, "FluencyScore"),
            Score(best, "CompletenessScore"),
            words,
            null);
    }

    private static double? Score(JsonElement e, string name)
    {
        if (e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
        {
            return Math.Round(v.GetDouble(), 1);
        }

        return e.TryGetProperty("PronunciationAssessment", out var pa) && pa.TryGetProperty(name, out var inner) && inner.ValueKind == JsonValueKind.Number
            ? Math.Round(inner.GetDouble(), 1)
            : null;
    }

    private static string? Text(JsonElement e, string name)
    {
        if (e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
        {
            return v.GetString();
        }

        return e.TryGetProperty("PronunciationAssessment", out var pa) && pa.TryGetProperty(name, out var inner) && inner.ValueKind == JsonValueKind.String
            ? inner.GetString()
            : null;
    }
}
