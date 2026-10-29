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
    [Fact]
    public async Task A_model_that_refuses_the_language_code_is_asked_again_without_it_and_a_refusal_names_its_reason()
    {
        var handler = new Handler { RejectLanguage = true };
        var voice = new ElevenLabsNarrator(new HttpClient(handler), Options.Create(new ElevenLabsOptions { ApiKey = "test-only", VoiceId = "voice-id", Model = "eleven_v3" }));
        Assert.Equal("ID3audio"u8.ToArray(), await voice.SynthesizeAsync("Salom.", default));
        Assert.Equal(["uz", null], handler.Languages);

        handler.Error = """{"detail":{"status":"voice_not_found","message":"A voice with that id does not exist."}}""";
        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => voice.SynthesizeAsync("Salom.", default));
        Assert.Contains("voice_not_found", error.Message);
        Assert.DoesNotContain("test-only", error.Message);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public string Body = "ID3audio";
        public string? Key, Language, Model, Url, Error;
        public bool RejectLanguage;
        public List<string?> Languages = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Key = request.Headers.GetValues("xi-api-key").Single(); Url = request.RequestUri!.ToString();
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Language = json.RootElement.TryGetProperty("language_code", out var l) ? l.GetString() : null;
            Model = json.RootElement.GetProperty("model_id").GetString();
            Languages.Add(Language);
            return Language is not null && RejectLanguage || Error is not null
                ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(Error ?? """{"detail":{"status":"unsupported_language","message":"Model does not support uz."}}""") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(Body)) };
        }
    }
}
