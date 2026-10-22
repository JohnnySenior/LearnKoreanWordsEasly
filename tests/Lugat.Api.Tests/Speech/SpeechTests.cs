using System.Net.Http.Json;
using System.Net;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Speech;

namespace Lugat.Api.Tests.Speech;

/// <summary>Never calls OpenAI.</summary>
public sealed class FakeSpeech : ISpeechSynthesizer
{
    public bool IsConfigured { get; set; } = true;

    public Lugat.Domain.Ai.AiProviderKind Kind => Lugat.Domain.Ai.AiProviderKind.Azure;

    public string Model => "fake-tts";

    public List<string> Texts { get; } = [];

    public string DialogueModel => "fake-tts-dialogue";

    public List<IReadOnlyList<SpeechLine>> Dialogues { get; } = [];

    public Task<byte[]> SynthesizeAsync(string text, CancellationToken ct)
    {
        Texts.Add(text);
        return Task.FromResult("ID3fake-mp3"u8.ToArray());
    }

    public Task<byte[]> SynthesizeDialogueAsync(IReadOnlyList<SpeechLine> lines, CancellationToken ct)
    {
        Dialogues.Add(lines);
        return Task.FromResult("ID3fake-dialogue"u8.ToArray());
    }
}

public class SpeechTests
{
    [Fact]
    public async Task Korean_text_is_recorded_once_and_then_served_from_the_store()
    {
        var speech = new FakeSpeech();
        await using var factory = new ApiFactory { Speech = speech };
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var first = await client.GetAsync("/api/tts?text=" + Uri.EscapeDataString("학교"));
        var second = await client.GetAsync("/api/tts?text=" + Uri.EscapeDataString(" 학교 "));
        var latin = await client.GetAsync("/api/tts?text=school");
        var tooLong = await client.GetAsync("/api/tts?text=" + Uri.EscapeDataString(new string('가', 201)));
        var anonymous = await factory.CreateClient().GetAsync("/api/tts?text=" + Uri.EscapeDataString("학교"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("audio/mpeg", first.Content.Headers.ContentType?.MediaType);
        Assert.Equal("ID3fake-mp3"u8.ToArray(), await second.Content.ReadAsByteArrayAsync());
        Assert.Equal(["학교"], speech.Texts); // the second request came from the store
        Assert.Equal(HttpStatusCode.UnprocessableEntity, latin.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Without_a_key_the_client_is_told_to_use_the_device_voice()
    {
        await using var factory = new ApiFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var response = await client.GetAsync("/api/tts?text=" + Uri.EscapeDataString("학교"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task New_recordings_have_a_daily_limit_that_does_not_touch_the_ai_limit()
    {
        var speech = new FakeSpeech();
        await using var factory = new ApiFactory { Speech = speech };
        factory.Settings["Ai:Tts:MaxNewPerUserPerDay"] = "2";
        factory.Settings["Ai:MaxCallsPerUserPerDay"] = "1";
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var a = await client.GetAsync("/api/tts?text=" + Uri.EscapeDataString("가"));
        var b = await client.GetAsync("/api/tts?text=" + Uri.EscapeDataString("나"));
        var c = await client.GetAsync("/api/tts?text=" + Uri.EscapeDataString("다"));
        var cached = await client.GetAsync("/api/tts?text=" + Uri.EscapeDataString("가"));
        var ai = await client.GetFromJsonAsync<Lugat.Contracts.AiStatusDto>("/api/ai/status", ApiFactory.Json);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, (HttpStatusCode)429, HttpStatusCode.OK), (a.StatusCode, b.StatusCode, c.StatusCode, cached.StatusCode));
        Assert.Equal(0, ai!.CallsToday);
    }
}
