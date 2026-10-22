using System.Net;
using System.Text;
using System.Text.Json;
using Lugat.Application.Ai;
using Lugat.Infrastructure.Ai;
using Microsoft.Extensions.Options;

namespace Lugat.Api.Tests.Ai;

/// <summary>
/// The provider adapters against recorded response shapes. Real calls need keys and are checked with
/// "Ulanishni tekshirish" in Settings after deploy.
/// </summary>
public class AiProviderHttpTests
{
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly AiJsonRequest Request = new("sentence-check", "instructions", "<learner_sentence>안녕</learner_sentence>", "sentence_check",
        """{"type":"object","additionalProperties":false,"required":["ok"],"properties":{"ok":{"type":"boolean"}}}""", 100);

    private static IOptions<AiOptions> Options() => Microsoft.Extensions.Options.Options.Create(new AiOptions
    {
        Anthropic = { ApiKey = "sk-ant-test", Model = "claude-test", BaseUrl = "https://anthropic.test/" },
        OpenAi = { ApiKey = "sk-openai-test", Model = "gpt-test", BaseUrl = "https://openai.test/" },
    });

    [Fact]
    public async Task Anthropic_forces_the_schema_tool_and_reads_its_input()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            {"id":"msg_1","type":"message","model":"claude-test-2026","content":[{"type":"tool_use","id":"t1","name":"sentence_check","input":{"ok":true}}],
             "usage":{"input_tokens":42,"output_tokens":7}}
            """);
        var provider = new AnthropicProvider(new HttpClient(handler), Options());

        var response = await provider.CompleteJsonAsync(Request, CancellationToken.None);

        Assert.Equal("""{"ok":true}""", response.Json);
        Assert.Equal(("claude-test-2026", 42, 7), (response.Model, response.InputTokens, response.OutputTokens));
        Assert.Equal("https://anthropic.test/v1/messages", handler.Request!.RequestUri!.ToString());
        Assert.Equal("sk-ant-test", handler.Request.Headers.GetValues("x-api-key").Single());
        Assert.Equal(AnthropicProvider.ApiVersion, handler.Request.Headers.GetValues("anthropic-version").Single());
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("claude-test", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("instructions", body.RootElement.GetProperty("system").GetString());
        Assert.Equal("sentence_check", body.RootElement.GetProperty("tool_choice").GetProperty("name").GetString());
        Assert.Equal("object", body.RootElement.GetProperty("tools")[0].GetProperty("input_schema").GetProperty("type").GetString());
    }

    [Fact]
    public async Task OpenAi_uses_a_strict_json_schema_and_reads_the_message()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            {"id":"c1","model":"gpt-test-2026","choices":[{"index":0,"message":{"role":"assistant","content":"{\"ok\":true}","refusal":null}}],
             "usage":{"prompt_tokens":30,"completion_tokens":5}}
            """);
        var provider = new OpenAiProvider(new HttpClient(handler), Options());

        var response = await provider.CompleteJsonAsync(Request, CancellationToken.None);

        Assert.Equal("""{"ok":true}""", response.Json);
        Assert.Equal(("gpt-test-2026", 30, 5), (response.Model, response.InputTokens, response.OutputTokens));
        Assert.Equal("https://openai.test/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer sk-openai-test", handler.Request.Headers.Authorization!.ToString());
        using var body = JsonDocument.Parse(handler.RequestBody!);
        var format = body.RootElement.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("json_schema").GetProperty("strict").GetBoolean());
        Assert.Equal("system", body.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":{"type":"authentication_error"}}""", false, "API kaliti")]
    [InlineData(HttpStatusCode.NotFound, """{"error":{"type":"not_found_error"}}""", false, "model")]
    [InlineData(HttpStatusCode.TooManyRequests, """{"error":{"code":"insufficient_quota"}}""", false, "mablag‘")]
    [InlineData(HttpStatusCode.TooManyRequests, """{"error":{"type":"rate_limit_error"}}""", true, "ko‘p")]
    [InlineData((HttpStatusCode)529, """{"error":{"type":"overloaded_error"}}""", true, "vaqtincha")]
    public async Task Http_errors_become_short_uzbek_reasons_without_the_key(HttpStatusCode status, string body, bool transient, string words)
    {
        var anthropic = new AnthropicProvider(new HttpClient(new Handler(status, body)), Options());
        var openAi = new OpenAiProvider(new HttpClient(new Handler(status, body)), Options());

        foreach (IAiProvider provider in new IAiProvider[] { anthropic, openAi })
        {
            var ex = await Assert.ThrowsAsync<AiProviderException>(() => provider.CompleteJsonAsync(Request, CancellationToken.None));
            Assert.Equal(transient, ex.Transient);
            Assert.Contains(words, ex.Message);
            Assert.DoesNotContain("sk-", ex.Message);
        }
    }

    [Fact]
    public void Provider_is_not_configured_without_a_key()
    {
        var provider = new OpenAiProvider(new HttpClient(), Microsoft.Extensions.Options.Options.Create(new AiOptions()));

        Assert.False(provider.IsConfigured);
    }

    [Fact]
    public async Task A_pdf_page_is_sent_as_a_document_to_claude_and_as_a_file_to_chatgpt()
    {
        var withPage = Request with { Attachments = [new AiAttachment("application/pdf", "page-15.pdf", [1, 2, 3])] };
        var claudeHandler = new Handler(HttpStatusCode.OK, """{"model":"m","content":[{"type":"tool_use","input":{"ok":true}}]}""");
        var gptHandler = new Handler(HttpStatusCode.OK, """{"model":"m","choices":[{"message":{"content":"{\"ok\":true}"}}]}""");

        await new AnthropicProvider(new HttpClient(claudeHandler), Options()).CompleteJsonAsync(withPage, CancellationToken.None);
        await new OpenAiProvider(new HttpClient(gptHandler), Options()).CompleteJsonAsync(withPage, CancellationToken.None);

        using var claude = JsonDocument.Parse(claudeHandler.RequestBody!);
        var blocks = claude.RootElement.GetProperty("messages")[0].GetProperty("content");
        Assert.Equal("document", blocks[0].GetProperty("type").GetString());
        Assert.Equal("AQID", blocks[0].GetProperty("source").GetProperty("data").GetString());
        Assert.Equal("text", blocks[1].GetProperty("type").GetString());

        using var gpt = JsonDocument.Parse(gptHandler.RequestBody!);
        var parts = gpt.RootElement.GetProperty("messages")[1].GetProperty("content");
        Assert.Equal("file", parts[0].GetProperty("type").GetString());
        Assert.Equal("data:application/pdf;base64,AQID", parts[0].GetProperty("file").GetProperty("file_data").GetString());
        Assert.Equal("text", parts[1].GetProperty("type").GetString());
    }

    [Fact]
    public async Task ChatGPT_gets_room_to_think_and_an_empty_cut_off_answer_is_explained()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            {"model":"gpt-test","choices":[{"message":{"role":"assistant","content":""},"finish_reason":"length"}],"usage":{"prompt_tokens":10,"completion_tokens":1600}}
            """);
        var provider = new OpenAiProvider(new HttpClient(handler), Options());

        var ex = await Assert.ThrowsAsync<AiProviderException>(() => provider.CompleteJsonAsync(Request with { MaxOutputTokens = 400 }, CancellationToken.None));

        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(OpenAiProvider.MinCompletionTokens, body.RootElement.GetProperty("max_completion_tokens").GetInt32());
        Assert.Contains("token chegarasi", ex.Message);
        Assert.True(ex.Transient);
    }
}
