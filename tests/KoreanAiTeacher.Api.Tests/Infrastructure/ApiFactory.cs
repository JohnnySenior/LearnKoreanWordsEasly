using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KoreanAiTeacher.Api.Auth;
using KoreanAiTeacher.Api.Tests.Auth;
using KoreanAiTeacher.Application.Abstractions;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace KoreanAiTeacher.Api.Tests.Infrastructure;

/// <summary>
/// The real app against a real, throw-away PostgreSQL database (spec AG02: ownership and idempotency are tested
/// on a real database). Server: KAT_TEST_POSTGRES, default local "kat/kat".
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const long UserA = 1001;
    public const long UserB = 1002;

    private static readonly string ServerConnection =
        Environment.GetEnvironmentVariable("KAT_TEST_POSTGRES") ?? "Host=localhost;Username=kat;Password=kat";

    private readonly string _database = "kat_test_" + Guid.NewGuid().ToString("N");
    private readonly string _files = Path.Combine(Path.GetTempPath(), "kat-test-files", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Starts at a fixed moment before every date the tests set, not at the real clock: a test that moves the clock
    /// to "Monday 5 October 2026" must keep working after that day has passed (a fake clock cannot go back).
    /// </summary>
    public static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    public FakeTimeProvider Time { get; } = new(Start);

    public string EnvironmentName { get; init; } = "Production";

    public string BotToken { get; init; } = InitDataBuilder.BotToken;

    public IPdfLayoutReader? PdfReader { get; init; }

    public ITelegramMessenger? Messenger { get; init; }

    /// <summary>Replaces the real Claude/ChatGPT adapters; by default tests have no AI at all.</summary>
    public IReadOnlyList<Application.Ai.IAiProvider> AiProviders { get; init; } = [];

    /// <summary>Replaces OpenAI speech; by default tests have a synthesizer that is "not configured".</summary>
    public Application.Speech.ISpeechSynthesizer? Speech { get; init; }

    /// <summary>Replaces Azure speech recognition; by default "not configured".</summary>
    public Application.Speech.IPronunciationAssessor? Pronunciation { get; init; }

    public Dictionary<string, string?> Settings { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:Default", new NpgsqlConnectionStringBuilder(ServerConnection) { Database = _database }.ConnectionString);
        builder.UseSetting("Storage:Root", _files);
        builder.UseSetting("Telegram:BotToken", BotToken);
        builder.UseSetting("Telegram:AllowedUserIds:0", UserA.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Telegram:AllowedUserIds:1", UserB.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Auth:DevLoginUserId", UserA.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Imports:PollInterval", "00:00:00.100");
        builder.UseSetting("Grammar:PollInterval", "00:00:00.100");
        builder.UseSetting("BookTests:PollInterval", "00:00:00.100");
        builder.UseSetting("Backup:Enabled", "false"); // production turns the nightly backup on; tests run it by hand
        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
            if (PdfReader is not null)
            {
                services.RemoveAll<IPdfLayoutReader>();
                services.AddSingleton(PdfReader);
            }

            // Never talk to the real AI services from tests.
            services.RemoveAll<Application.Ai.IAiProvider>();
            foreach (var provider in AiProviders)
            {
                services.AddSingleton(provider);
            }

            services.RemoveAll<Application.Speech.ISpeechSynthesizer>();
            services.AddSingleton(Speech ?? new KoreanAiTeacher.Api.Tests.Speech.FakeSpeech { IsConfigured = false });

            services.RemoveAll<Application.Speech.IPronunciationAssessor>();
            services.AddSingleton(Pronunciation ?? new KoreanAiTeacher.Api.Tests.Speech.FakePronunciation { IsConfigured = false });

            // Never talk to the real Telegram from tests: without a Mini App URL the real messenger is "not configured".
            if (Messenger is not null)
            {
                services.RemoveAll<ITelegramMessenger>();
                services.AddSingleton(Messenger);
            }
        });
    }

    public async Task<HttpClient> SignedInClientAsync(long telegramUserId)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/telegram", new TelegramAuthRequest(InitDataBuilder.Build(telegramUserId, Time.GetUtcNow(), BotToken)));
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(ServerConnection) { Database = "postgres" }.ConnectionString);
        await connection.OpenAsync();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection);
                await drop.ExecuteNonQueryAsync();
                break;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InsufficientPrivilege && attempt < 20)
            {
                // An autovacuum worker on the fresh database cannot be terminated by a non-superuser; it finishes quickly.
                await Task.Delay(250);
            }
        }
        if (Directory.Exists(_files))
        {
            Directory.Delete(_files, recursive: true);
        }
    }
}
