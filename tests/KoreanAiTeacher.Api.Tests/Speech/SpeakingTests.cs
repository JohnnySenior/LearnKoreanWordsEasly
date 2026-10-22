using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KoreanAiTeacher.Api.Tests.Infrastructure;
using KoreanAiTeacher.Application.Speech;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Infrastructure.Speech;

namespace KoreanAiTeacher.Api.Tests.Speech;

/// <summary>Never calls Azure.</summary>
public sealed class FakePronunciation : IPronunciationAssessor
{
    public bool IsConfigured { get; set; } = true;

    public List<(int Bytes, string? Reference)> Calls { get; } = [];

    /// <summary>What was "heard" for a reference; null = the fixed sentence below.</summary>
    public Func<string?, SpeakingResultDto>? Respond { get; set; }

    public Task<SpeakingResultDto> AssessAsync(byte[] wav, string? referenceText, CancellationToken ct)
    {
        Calls.Add((wav.Length, referenceText));
        if (Respond is not null)
        {
            return Task.FromResult(Respond(referenceText));
        }

        return Task.FromResult(new SpeakingResultDto(true, "학교에 가요.", referenceText is null ? null : 88, 90, 85, 100,
            referenceText is null ? [] : [new PronunciationWordDto("학교에", 92, "None"), new PronunciationWordDto("가요", 55, "Mispronunciation")], null));
    }
}

public class SpeakingTests
{
    private static byte[] Wav(int bytes = 8000)
    {
        var data = new byte[bytes];
        "RIFF"u8.CopyTo(data);
        return data;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, byte[] wav, string? reference)
    {
        var audio = new ByteArrayContent(wav);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        using var form = new MultipartFormDataContent { { audio, "audio", "speech.wav" } };
        if (reference is not null)
        {
            form.Add(new StringContent(reference), "reference");
        }

        return await client.PostAsync("/api/speech/assess", form);
    }

    [Fact]
    public async Task A_recording_is_scored_against_the_reference_or_transcribed()
    {
        var azure = new FakePronunciation();
        await using var factory = new ApiFactory { Pronunciation = azure };
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var scored = await PostAsync(client, Wav(), "학교에 가요");
        var transcript = await PostAsync(client, Wav(), null);
        var notWav = await PostAsync(client, new byte[8000], null);
        var latinReference = await PostAsync(client, Wav(), "school");
        var ai = (await client.GetFromJsonAsync<AiStatusDto>("/api/ai/status", Grammar.GrammarImportTests.Json))!;

        var result = (await scored.Content.ReadFromJsonAsync<SpeakingResultDto>(Grammar.GrammarImportTests.Json))!;
        Assert.Equal(HttpStatusCode.OK, scored.StatusCode);
        Assert.Equal((88.0, 2), (result.PronScore, result.Words.Count));
        Assert.Equal(HttpStatusCode.OK, transcript.StatusCode);
        Assert.Equal([(8000, "학교에 가요"), (8000, (string?)null)], azure.Calls);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notWav.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, latinReference.StatusCode);
        Assert.Equal(0, ai.CallsToday); // speech has its own limit
    }

    [Fact]
    public async Task Without_azure_the_learner_is_told_and_can_keep_writing()
    {
        await using var factory = new ApiFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var response = await PostAsync(client, Wav(), "학교");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public void Azure_scores_are_read_from_both_response_shapes()
    {
        const string flat = """
            {"RecognitionStatus":"Success","DisplayText":"학교에 가요.","NBest":[{"AccuracyScore":90.4,"FluencyScore":85,"CompletenessScore":100,"PronScore":88.26,
             "Words":[{"Word":"학교에","AccuracyScore":92,"ErrorType":"None"},{"Word":"가요","AccuracyScore":55,"ErrorType":"Mispronunciation"}]}]}
            """;
        const string nested = """
            {"RecognitionStatus":"Success","DisplayText":"학교","NBest":[{"PronunciationAssessment":{"AccuracyScore":70,"PronScore":72},
             "Words":[{"Word":"학교","PronunciationAssessment":{"AccuracyScore":70,"ErrorType":"None"}}]}]}
            """;

        var a = AzurePronunciationAssessor.Parse(flat);
        var b = AzurePronunciationAssessor.Parse(nested);
        var silent = AzurePronunciationAssessor.Parse("""{"RecognitionStatus":"InitialSilenceTimeout"}""");

        Assert.Equal((true, "학교에 가요.", 88.3, 90.4), (a.Recognized, a.RecognizedText, a.PronScore, a.Accuracy));
        Assert.Equal(("가요", 55.0, "Mispronunciation"), (a.Words[1].Word, a.Words[1].Accuracy!.Value, a.Words[1].ErrorType));
        Assert.Equal((72.0, 70.0, "None"), (b.PronScore!.Value, b.Words[0].Accuracy!.Value, b.Words[0].ErrorType));
        Assert.False(silent.Recognized);
        Assert.Contains("eshitilmadi", silent.Message);
    }
}
