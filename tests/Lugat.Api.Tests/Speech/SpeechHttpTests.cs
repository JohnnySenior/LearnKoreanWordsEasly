using System.Net;
using Lugat.Application.Ai;
using Lugat.Application.Speech;
using Lugat.Domain.Ai;
using Lugat.Infrastructure.Speech;
using Microsoft.Extensions.Options;

namespace Lugat.Api.Tests.Speech;

/// <summary>The speech adapters against the documented request shapes; real voices are checked on the phone.</summary>
public class SpeechHttpTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("ID3audio"u8.ToArray()) };
        }
    }

    private static IOptions<SpeechOptions> Speech(string? key = "azure-key") => Options.Create(new SpeechOptions
    {
        Azure = { Key = key, Region = "koreacentral", Voice = "ko-KR-SunHiNeural" },
    });

    private static IOptions<AiOptions> Ai() => Options.Create(new AiOptions { OpenAi = { ApiKey = "sk-test", Model = "gpt-test", BaseUrl = "https://openai.test/" } });

    [Fact]
    public async Task Azure_sends_escaped_ssml_to_the_region_endpoint()
    {
        var handler = new Handler();
        var azure = new AzureSpeechSynthesizer(new HttpClient(handler), Speech());

        var audio = await azure.SynthesizeAsync("학교 <b>&", CancellationToken.None);

        Assert.Equal("https://koreacentral.tts.speech.microsoft.com/cognitiveservices/v1", handler.Request!.RequestUri!.ToString());
        Assert.Equal("azure-key", handler.Request.Headers.GetValues("Ocp-Apim-Subscription-Key").Single());
        Assert.Equal("audio-24khz-48kbitrate-mono-mp3", handler.Request.Headers.GetValues("X-Microsoft-OutputFormat").Single());
        Assert.Equal("application/ssml+xml", handler.Request.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("""<voice name="ko-KR-SunHiNeural">학교 &lt;b&gt;&amp;</voice>""", handler.Body);
        Assert.Equal("ID3audio"u8.ToArray(), audio);
    }

    [Fact]
    public void Azure_is_used_when_its_key_is_set_otherwise_openai()
    {
        var withAzure = new SpeechSynthesizerChooser(
            new AzureSpeechSynthesizer(new HttpClient(new Handler()), Speech()),
            new OpenAiSpeechSynthesizer(new HttpClient(new Handler()), Ai(), Speech()),
            Speech());
        var withoutAzure = new SpeechSynthesizerChooser(
            new AzureSpeechSynthesizer(new HttpClient(new Handler()), Speech(key: null)),
            new OpenAiSpeechSynthesizer(new HttpClient(new Handler()), Ai(), Speech(key: null)),
            Speech(key: null));

        Assert.Equal((AiProviderKind.Azure, "ko-KR-SunHiNeural"), (withAzure.Kind, withAzure.Model));
        Assert.Equal(AiProviderKind.OpenAi, withoutAzure.Kind);
        Assert.True(withoutAzure.IsConfigured);
    }
    [Fact]
    public async Task Azure_assessment_sends_wav_with_the_reference_in_the_header()
    {
        var handler = new JsonHandler("""{"RecognitionStatus":"Success","DisplayText":"학교","NBest":[{"PronScore":90,"Words":[]}]}""");
        var azure = new AzurePronunciationAssessor(new HttpClient(handler), Speech());

        var result = await azure.AssessAsync("RIFFwav"u8.ToArray(), "학교", CancellationToken.None);
        var free = await azure.AssessAsync("RIFFwav"u8.ToArray(), null, CancellationToken.None);

        Assert.StartsWith("https://koreacentral.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language=ko-KR", handler.Requests[0].Url);
        Assert.Equal("audio/wav; codecs=audio/pcm; samplerate=16000", handler.Requests[0].ContentType);
        var config = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(handler.Requests[0].Assessment!));
        Assert.Contains("\"ReferenceText\":\"학교\"", System.Text.RegularExpressions.Regex.Unescape(config));
        Assert.Null(handler.Requests[1].Assessment); // free speech: transcript only
        Assert.Equal(90, result.PronScore);
        Assert.True(free.Recognized);
    }

    private sealed class JsonHandler(string body) : HttpMessageHandler
    {
        public List<(string Url, string? ContentType, string? Assessment)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.ToString(), string.Join(";", request.Content!.Headers.GetValues("Content-Type")),
                request.Headers.TryGetValues("Pronunciation-Assessment", out var v) ? v.Single() : null));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
