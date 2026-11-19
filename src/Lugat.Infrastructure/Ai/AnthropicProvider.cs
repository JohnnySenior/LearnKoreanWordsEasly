using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lugat.Application.Ai;
using Lugat.Domain.Ai;
using Microsoft.Extensions.Options;

namespace Lugat.Infrastructure.Ai;

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

    public string ModelFor(AiJsonRequest request) => Options.ModelFor(request);

    /// <summary>
    /// L-88: from Claude Opus 5.5, Sonnet 5.5 and Fable 5.1 on, a forced tool call is refused (400) and thinking is
    /// always on; these models get the schema as a JSON output (<c>output_config.format</c>) instead.
    /// </summary>
    private static readonly string[] JsonOutputModels = ["claude-opus-5-5", "claude-sonnet-5-5", "claude-fable-5-1", "claude-fable-5-2", "claude-mythos-5"];

    /// <summary>Room for the model's thinking on top of the answer, for the JSON-output models.</summary>
    public const int MinThinkingOutputTokens = 16000;

    public static bool UsesJsonOutput(string model) => JsonOutputModels.Any(p => model.StartsWith(p, StringComparison.Ordinal));

    public async Task<AiJsonResponse> CompleteJsonAsync(AiJsonRequest request, CancellationToken ct)
    {
        var model = Options.ModelFor(request);
        var json = UsesJsonOutput(model);
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = json ? Math.Max(request.MaxOutputTokens * 2, MinThinkingOutputTokens) : request.MaxOutputTokens,
            ["system"] = request.Instructions,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = Content(request) }),
        };
        if (json)
        {
            body["output_config"] = new JsonObject
            {
                ["format"] = new JsonObject { ["type"] = "json_schema", ["schema"] = Supported(JsonNode.Parse(request.JsonSchema)) },
            };
        }
        else
        {
            body["tools"] = new JsonArray(new JsonObject
            {
                ["name"] = request.SchemaName,
                ["description"] = "Return the result.",
                ["input_schema"] = JsonNode.Parse(request.JsonSchema),
            });
            body["tool_choice"] = new JsonObject { ["type"] = "tool", ["name"] = request.SchemaName };
        }

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
        string result;
        if (json)
        {
            var stop = root.TryGetProperty("stop_reason", out var r) ? r.GetString() : null;
            if (stop == "refusal")
            {
                throw new AiProviderException("Claude javob berishdan bosh tortdi", transient: false);
            }

            if (stop == "max_tokens")
            {
                throw new AiProviderException("Claude javobi token chegarasiga yetib, chala qoldi", transient: true);
            }

            // Thinking blocks come first; the answer is the text.
            result = string.Concat(root.GetProperty("content").EnumerateArray()
                .Where(c => c.GetProperty("type").GetString() == "text").Select(c => c.GetProperty("text").GetString()));
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new AiProviderException("Claude natija qaytarmadi", transient: true);
            }
        }
        else
        {
            var input = root.GetProperty("content").EnumerateArray()
                .FirstOrDefault(c => c.GetProperty("type").GetString() == "tool_use");
            if (input.ValueKind == JsonValueKind.Undefined)
            {
                throw new AiProviderException("Claude natija qaytarmadi", transient: true);
            }

            result = input.GetProperty("input").GetRawText();
        }

        var usage = root.TryGetProperty("usage", out var u) ? u : default;
        return new AiJsonResponse(
            result,
            root.TryGetProperty("model", out var m) ? m.GetString() ?? model : model,
            AiHttp.Int(usage, "input_tokens"),
            AiHttp.Int(usage, "output_tokens"));
    }

    /// <summary>JSON outputs accept no numeric, length or item-count limits: the schema without them (the app checks those itself).</summary>
    private static JsonNode? Supported(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in new[] { "maxItems", "minItems", "minLength", "maxLength", "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "multipleOf", "pattern" })
            {
                obj.Remove(key);
            }

            foreach (var (_, child) in obj.ToList())
            {
                Supported(child);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                Supported(child);
            }
        }

        return node;
    }

    /// <summary>Plain text, or document blocks (PDF pages) and image blocks (photos, L-69) followed by the text.</summary>
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
                ["type"] = f.MediaType.StartsWith("image/", StringComparison.Ordinal) ? "image" : "document",
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

    public string ModelFor(AiJsonRequest request) => Options.ModelFor(request);

    public async Task<AiJsonResponse> CompleteJsonAsync(AiJsonRequest request, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = Options.ModelFor(request),
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
            root.TryGetProperty("model", out var m) ? m.GetString() ?? Options.ModelFor(request) : Options.ModelFor(request),
            AiHttp.Int(usage, "prompt_tokens"),
            AiHttp.Int(usage, "completion_tokens"));
    }

    /// <summary>Plain text, or file parts (PDF pages, read with vision) and image parts (photos, L-69) followed by the text.</summary>
    private static JsonNode Content(AiJsonRequest request)
    {
        if (request.Attachments is not { Count: > 0 } files)
        {
            return request.UserContent;
        }

        var parts = new JsonArray();
        foreach (var f in files)
        {
            if (f.MediaType.StartsWith("image/", StringComparison.Ordinal))
            {
                parts.Add(new JsonObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JsonObject { ["url"] = $"data:{f.MediaType};base64,{Convert.ToBase64String(f.Content)}" },
                });
                continue;
            }

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
            400 when HasInsufficientCredit(body) => new AiProviderException($"{name}: hisobda API mablag‘i yetarli emas", transient: false),
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

    /// <summary>Anthropic reports low credit as HTTP 400. Classify it without exposing its response text.</summary>
    private static bool HasInsufficientCredit(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.String)
            {
                var message = value.GetString()!;
                return message.Contains("credit balance is too low", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("insufficient credits", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (JsonException) { }
        return false;
    }

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
