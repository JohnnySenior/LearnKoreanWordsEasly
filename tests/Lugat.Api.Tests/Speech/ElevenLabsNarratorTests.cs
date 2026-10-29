using System.Net;
using System.Text;
using System.Text.Json;
using Lugat.Application;
using Lugat.Infrastructure.Speech;
using Microsoft.Extensions.Options;

namespace Lugat.Api.Tests.Speech;

public class ElevenLabsNarratorTests
{
    [Fact]
    public async Task Sends_the_configured_voice_model_language_and_server_only_key_and_rejects_a_non_mp3_success_response()
    {
        var handler = new Handler();
        var voice = new ElevenLabsNarrator(new HttpClient(handler), Options.Create(new ElevenLabsOptions { ApiKey = "test-only", VoiceId = "voice-id", Model = "eleven_v3" }));
        Assert.True(voice.IsConfigured);
        Assert.Equal("ID3audio"u8.ToArray(), await voice.SynthesizeAsync("Salom, boshlaymiz.", default));
        Assert.Equal("test-only", handler.Key);
        Assert.Equal("uz", handler.Language);
        Assert.Equal("eleven_v3", handler.Model);
        Assert.Contains("voice-id", handler.Url);
        Assert.DoesNotContain("test-only", handler.Url);
        handler.Body = "{}";
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => voice.SynthesizeAsync("Salom.", default));
    }
    private sealed class Handler : HttpMessageHandler
    {
        public string Body = "ID3audio";
        public string? Key, Language, Model, Url;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Key = request.Headers.GetValues("xi-api-key").Single(); Url = request.RequestUri!.ToString();
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Language = json.RootElement.GetProperty("language_code").GetString();
            Model = json.RootElement.GetProperty("model_id").GetString();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(Body)) };
        }
    }
}
