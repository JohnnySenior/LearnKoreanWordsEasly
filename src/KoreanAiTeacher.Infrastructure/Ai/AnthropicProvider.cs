using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using KoreanAiTeacher.Application.Ai;
using KoreanAiTeacher.Domain.Ai;
using Microsoft.Extensions.Options;

namespace KoreanAiTeacher.Infrastructure.Ai;

/// <summary>
/// Claude through the Anthropic Messages API. The JSON shape is enforced by forcing a single tool call whose input
/// schema is the expected result, so the answer is always an object to validate (spec AI01).
/// </summary>
public sealed class AnthropicProvider(HttpClient http, IOptions<AiOptions> options) : IAiProvider
{
    public const string ApiVersion = "2023-06-01";

    private AiProviderOptions Options => options.Value.Anthropic;

    public AiProviderKind Kind => AiProviderKind.Anthropic;

    public bool IsConfigured => Options.IsConfigured;

    public string Model => Options.Model;

    public async Task<AiJsonResponse> CompleteJsonAsync(AiJsonRequest request, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = Options.Model,
            ["max_tokens"] = request.MaxOutputTokens,
            ["system"] = request.Instructions,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = Content(request) }),
            ["tools"] = new JsonArray(new JsonObject
            {
                ["name"] = request.SchemaName,
                ["description"] = "Return the result.",
                ["input_schema"] = JsonNode.Parse(request.JsonSchema),
            }),
            ["tool_choice"] = new JsonObject { ["type"] = "tool", ["name"] = request.SchemaName },
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(Options.BaseUrl), "v1/messages"))
        {
            Content = JsonContent.Create(body),
        };
        message.Headers.Add("x-api-key", Options.ApiKey);
        message.Headers.Add("anthropic-version", ApiVersion);

        using var response = await http.SendAsync(message, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw AiHttp.Failure("Claude", response.StatusCode, text);
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var input = root.GetProperty("content").EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("type").GetString() == "tool_use");
        if (input.ValueKind == JsonValueKind.Undefined)
        {
            throw new AiProviderException("Claude natija qaytarmadi", transient: true);
        }

        var usage = root.TryGetProperty("usage", out var u) ? u : default;
        return new AiJsonResponse(
            input.GetProperty("input").GetRawText(),
            root.TryGetProperty("model", out var m) ? m.GetString() ?? Options.Model : Options.Model,
            AiHttp.Int(usage, "input_tokens"),
            AiHttp.Int(usage, "output_tokens"));
    }

    /// <summary>Plain text, or document blocks (PDF pages) followed by the text.</summary>
    private static JsonNode Content(AiJsonRequest request)
    {
        if (request.Attachments is not { Count: > 0 } files)
        {
            return request.UserContent;
        }

        var blocks = new JsonArray();
        foreach (var f in files)
        {
            blocks.Add(new JsonObject
            {
                ["type"] = "document",
                ["source"] = new JsonObject { ["type"] = "base64", ["media_type"] = f.MediaType, ["data"] = Convert.ToBase64String(f.Content) },
            });
        }

        blocks.Add(new JsonObject { ["type"] = "text", ["text"] = request.UserContent });
        return blocks;
    }
}

/// <summary>ChatGPT through the OpenAI Chat Completions API with a strict JSON schema response format.</summary>
public sealed class OpenAiProvider(HttpClient http, IOptions<AiOptions> options) : IAiProvider
{
    public const int MinCompletionTokens = 8000;

    private AiProviderOptions Options => options.Value.OpenAi;

    public AiProviderKind Kind => AiProviderKind.OpenAi;

    public bool IsConfigured => Options.IsConfigured;

    public string Model => Options.Model;

    public async Task<AiJsonResponse> CompleteJsonAsync(AiJsonRequest request, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = Options.Model,
            // Reasoning models (gpt-5 family) spend part of this budget thinking before any visible text,
            // so a short answer still needs room; a too-small budget returns empty content.
            ["max_completion_tokens"] = Math.Max(request.MaxOutputTokens * 4, MinCompletionTokens),
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = request.Instructions },
                new JsonObject { ["role"] = "user", ["content"] = Content(request) }),
            ["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = request.SchemaName,
                    ["strict"] = true,
                    ["schema"] = JsonNode.Parse(request.JsonSchema),
                },
            },
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(Options.BaseUrl), "v1/chat/completions"))
        {
            Content = JsonContent.Create(body),
        };
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Options.ApiKey);

        using var response = await http.SendAsync(message, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw AiHttp.Failure("ChatGPT", response.StatusCode, text);
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var choice = root.GetProperty("choices")[0].GetProperty("message");
        if (choice.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.String)
        {
            throw new AiProviderException("ChatGPT javob berishdan bosh tortdi", transient: false);
        }

        var content = choice.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
        if (string.IsNullOrWhiteSpace(content))
        {
            var finish = root.GetProperty("choices")[0].TryGetProperty("finish_reason", out var f) ? f.GetString() : null;
            throw new AiProviderException(
                finish == "length" ? "ChatGPT javobi token chegarasiga yetib, bo‘sh qoldi" : "ChatGPT bo‘sh javob qaytardi",
                transient: true);
        }

        var usage = root.TryGetProperty("usage", out var u) ? u : default;
        return new AiJsonResponse(
            content,
            root.TryGetProperty("model", out var m) ? m.GetString() ?? Options.Model : Options.Model,
            AiHttp.Int(usage, "prompt_tokens"),
            AiHttp.Int(usage, "completion_tokens"));
    }

    /// <summary>Plain text, or file parts (PDF pages, read with vision) followed by the text.</summary>
    private static JsonNode Content(AiJsonRequest request)
    {
        if (request.Attachments is not { Count: > 0 } files)
        {
            return request.UserContent;
        }

        var parts = new JsonArray();
        foreach (var f in files)
        {
            parts.Add(new JsonObject
            {
                ["type"] = "file",
                ["file"] = new JsonObject
                {
                    ["filename"] = f.FileName,
                    ["file_data"] = $"data:{f.MediaType};base64,{Convert.ToBase64String(f.Content)}",
                },
            });
        }

        parts.Add(new JsonObject { ["type"] = "text", ["text"] = request.UserContent });
        return parts;
    }
}

internal static class AiHttp
{
    /// <summary>Turns an HTTP error into a short Uzbek reason. The response body is not echoed (it may quote the request).</summary>
    public static AiProviderException Failure(string name, HttpStatusCode status, string body)
    {
        var code = (int)status;
        var errorType = TryErrorType(body);
        return code switch
        {
            401 or 403 => new AiProviderException($"{name}: API kaliti noto‘g‘ri yoki ruxsat yo‘q", transient: false),
            404 => new AiProviderException($"{name}: model topilmadi, model nomini tekshiring", transient: false),
            400 => new AiProviderException($"{name}: so‘rov qabul qilinmadi ({errorType ?? "400"})", transient: false),
            402 => new AiProviderException($"{name}: hisobda mablag‘ yetarli emas", transient: false),
            429 when errorType is "insufficient_quota" => new AiProviderException($"{name}: hisobda mablag‘ yoki limit tugagan", transient: false),
            429 => new AiProviderException($"{name}: so‘rovlar juda ko‘p, birozdan keyin", transient: true),
            _ when code >= 500 => new AiProviderException($"{name}: xizmat vaqtincha ishlamayapti ({code})", transient: true),
            _ => new AiProviderException($"{name}: kutilmagan javob ({code})", transient: false),
        };
    }

    public static int Int(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;

    private static string? TryErrorType(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in new[] { "code", "type" })
                {
                    if (error.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                    {
                        var s = v.GetString()!;
                        return s.Length > 60 ? s[..60] : s;
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
