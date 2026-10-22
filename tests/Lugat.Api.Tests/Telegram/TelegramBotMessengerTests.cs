using System.Net;
using Lugat.Application.Abstractions;
using Lugat.Infrastructure.Telegram;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lugat.Api.Tests.Telegram;

/// <summary>
/// The real bot adapter against the Bot API request shape. A real token has a colon ("123:ABC"); the address used to be
/// read as an absolute URI with the scheme "bot123", so no reminder or backup file ever left the server.
/// </summary>
public class TelegramBotMessengerTests
{
    private const string Token = "8757196319:AAH-test_secret";

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static (TelegramBotMessenger Bot, Handler Handler) Create(HttpStatusCode status = HttpStatusCode.OK, string body = """{"ok":true,"result":{"message_id":42}}""")
    {
        var handler = new Handler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.telegram.org/") };
        var options = new TelegramBotOptions { BotToken = Token, MiniAppUrl = "https://lugat.example" };
        return (new TelegramBotMessenger(http, options, NullLogger<TelegramBotMessenger>.Instance), handler);
    }

    [Fact]
    public async Task A_message_goes_to_the_bot_method_address_with_a_token_containing_a_colon()
    {
        var (bot, handler) = Create();

        var result = await bot.SendAsync(1001, "Bugungi mashq tayyor", "Mashqni ochish", CancellationToken.None);

        Assert.Equal((SendOutcome.Sent, 42L), (result.Outcome, result.MessageId));
        Assert.Equal($"https://api.telegram.org/bot{Token}/sendMessage", handler.Request!.RequestUri!.ToString());
        Assert.Contains("\"chat_id\":1001", handler.Body);
        Assert.Contains("https://lugat.example", handler.Body);
    }

    [Fact]
    public async Task A_document_is_sent_as_multipart_with_chat_caption_and_file()
    {
        var (bot, handler) = Create();

        var result = await bot.SendDocumentAsync(1001, "lugat-20261004-040000.dump", [1, 2, 3], "zaxira nusxa", CancellationToken.None);

        Assert.Equal(SendOutcome.Sent, result.Outcome);
        Assert.Equal($"https://api.telegram.org/bot{Token}/sendDocument", handler.Request!.RequestUri!.ToString());
        Assert.Equal("multipart/form-data", handler.Request.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("name=chat_id", handler.Body);
        Assert.Contains("1001", handler.Body);
        Assert.Contains("zaxira nusxa", handler.Body);
        Assert.Contains("filename=lugat-20261004-040000.dump", handler.Body);
    }

    [Fact]
    public async Task A_user_who_never_pressed_start_is_reported_as_blocked()
    {
        var (bot, _) = Create(HttpStatusCode.BadRequest, """{"ok":false,"description":"Bad Request: chat not found"}""");

        var result = await bot.SendDocumentAsync(1001, "lugat.dump", [1], "x", CancellationToken.None);

        Assert.Equal(SendOutcome.Blocked, result.Outcome);
    }
}
