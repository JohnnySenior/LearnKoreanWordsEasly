using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KoreanAiTeacher.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KoreanAiTeacher.Infrastructure.Telegram;

public sealed class TelegramBotOptions
{
    /// <summary>Same secret as sign-in (config "Telegram:BotToken").</summary>
    public string BotToken { get; set; } = "";

    /// <summary>
    /// HTTPS address of the Mini App for the reminder button. Falls back to Railway's public domain.
    /// </summary>
    public string MiniAppUrl { get; set; } = "";

    public static TelegramBotOptions From(IConfiguration config)
    {
        var options = config.GetSection("Telegram").Get<TelegramBotOptions>() ?? new TelegramBotOptions();
        if (string.IsNullOrWhiteSpace(options.MiniAppUrl) && config["RAILWAY_PUBLIC_DOMAIN"] is { Length: > 0 } domain)
        {
            options.MiniAppUrl = $"https://{domain}";
        }

        return options;
    }
}

/// <summary>
/// Telegram Bot API <c>sendMessage</c> with an inline button that opens the Mini App.
/// The bot token only appears in the request path and is never logged.
/// </summary>
public sealed class TelegramBotMessenger(HttpClient http, TelegramBotOptions options, ILogger<TelegramBotMessenger> logger) : ITelegramMessenger
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.BotToken) && !string.IsNullOrWhiteSpace(options.MiniAppUrl);

    /// <summary>
    /// Bot API method address. The token has a colon ("123:ABC"), so as a bare relative string "bot123:ABC/…" would be
    /// read as an absolute URI with the scheme "bot123" and HttpClient refuses it. The "./" keeps it relative.
    /// </summary>
    private Uri Method(string name) => new($"./bot{options.BotToken}/{name}", UriKind.Relative);

    public async Task<SendResult> SendDocumentAsync(long chatId, string fileName, byte[] content, string caption, CancellationToken ct)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        using var form = new MultipartFormDataContent
        {
            { new StringContent(chatId.ToString(System.Globalization.CultureInfo.InvariantCulture)), "chat_id" },
            { new StringContent(caption), "caption" },
            { file, "document", fileName },
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(Method("sendDocument"), form, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Telegram sendDocument did not complete: {Error}", ex.GetType().Name);
            return new SendResult(SendOutcome.Uncertain, Error: "Telegram javob bermadi.");
        }

        return await ReadAsync(response, "sendDocument", ct);
    }

    public async Task<SendResult> SendAsync(long chatId, string text, string openAppButton, CancellationToken ct)
    {
        var body = new
        {
            chat_id = chatId,
            text,
            reply_markup = new
            {
                inline_keyboard = new[] { new[] { new { text = openAppButton, web_app = new { url = options.MiniAppUrl } } } },
            },
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(Method("sendMessage"), body, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Telegram sendMessage did not complete: {Error}", ex.GetType().Name);
            return new SendResult(SendOutcome.Uncertain, Error: "Telegram javob bermadi.");
        }

        return await ReadAsync(response, "sendMessage", ct);
    }

    private async Task<SendResult> ReadAsync(HttpResponseMessage response, string method, CancellationToken ct)
    {
        using (response)
        {
            ApiResponse? payload = null;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<ApiResponse>(ct);
            }
            catch (JsonException)
            {
            }

            if (response.IsSuccessStatusCode && payload?.Ok == true)
            {
                return new SendResult(SendOutcome.Sent, payload.Result?.MessageId);
            }

            var description = payload?.Description ?? response.ReasonPhrase ?? "unknown";
            logger.LogWarning("Telegram {Method} refused: {Status} {Description}", method, (int)response.StatusCode, description);
            return response.StatusCode switch
            {
                // 403: blocked by the user. 400 "chat not found": the user never pressed Start.
                HttpStatusCode.Forbidden => new SendResult(SendOutcome.Blocked, Error: description),
                HttpStatusCode.BadRequest when description.Contains("chat not found", StringComparison.OrdinalIgnoreCase)
                    => new SendResult(SendOutcome.Blocked, Error: description),
                HttpStatusCode.TooManyRequests => new SendResult(SendOutcome.Retryable, Error: description,
                    RetryAfter: TimeSpan.FromSeconds(payload?.Parameters?.RetryAfter ?? 30)),
                >= HttpStatusCode.InternalServerError => new SendResult(SendOutcome.Retryable, Error: description),
                _ => new SendResult(SendOutcome.Failed, Error: description),
            };
        }
    }

    private sealed record ApiResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("result")] MessageResult? Result,
        [property: JsonPropertyName("parameters")] ResponseParameters? Parameters);

    private sealed record MessageResult([property: JsonPropertyName("message_id")] long MessageId);

    private sealed record ResponseParameters([property: JsonPropertyName("retry_after")] int? RetryAfter);
}
