using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lugat.Api.Auth;
using Lugat.Application.Abstractions;
using Lugat.Application.Users;
using Lugat.Application.Vocabulary;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Lugat.Api.Tests.Infrastructure;

/// <summary>
/// The real app against a real, throw-away PostgreSQL database (spec AG02: ownership and idempotency are tested
/// on a real database). Server: LUGAT_TEST_POSTGRES, default local "lugat/lugat".
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const long UserA = 1001;
    public const long UserB = 1002;

    private static readonly string ServerConnection =
        Environment.GetEnvironmentVariable("LUGAT_TEST_POSTGRES") ?? "Host=localhost;Username=lugat;Password=lugat";

    private readonly string _database = "lugat_test_" + Guid.NewGuid().ToString("N");
    private readonly string _files = Path.Combine(Path.GetTempPath(), "lugat-test-files", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Starts at a fixed moment before every date the tests set, not at the real clock: a test that moves the clock
    /// to "Monday 5 October 2026" must keep working after that day has passed (a fake clock cannot go back).
    /// </summary>
    public static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    public FakeTimeProvider Time { get; } = new(Start);

    /// <summary>The API's JSON shape: web defaults with enums as names.</summary>
    public static readonly System.Text.Json.JsonSerializerOptions Json =
        new(System.Text.Json.JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    /// <summary>Test-only sign-in endpoint (see <see cref="TestEndpoints"/>); query: tg = Telegram user id, name = first name.</summary>
    public const string TestSignInPath = "/__test/sign-in";

    /// <summary>Test-only: adds <see cref="TableWords"/> as table 1 (or ?table=n) of the signed-in learner.</summary>
    public const string TestWordsPath = "/__test/words";

    /// <summary>The words a test's learner gets through <see cref="TestWordsPath"/>.</summary>
    public IReadOnlyList<(string Korean, string Uzbek)> TableWords { get; init; } = [];

    public string EnvironmentName { get; init; } = "Production";

    public string BotToken { get; init; } = "123456:TEST-TOKEN";

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
        builder.UseSetting("Auth:DevLoginUserId", UserA.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Backup:Enabled", "false"); // production turns the nightly backup on; tests run it by hand
        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<IStartupFilter>(new TestEndpoints(this));

            // Never talk to the real AI services from tests.
            services.RemoveAll<Application.Ai.IAiProvider>();
            foreach (var provider in AiProviders)
            {
                services.AddSingleton(provider);
            }

            services.RemoveAll<Application.Speech.ISpeechSynthesizer>();
            services.AddSingleton(Speech ?? new Lugat.Api.Tests.Speech.FakeSpeech { IsConfigured = false });

            services.RemoveAll<Application.Speech.IPronunciationAssessor>();
            services.AddSingleton(Pronunciation ?? new Lugat.Api.Tests.Speech.FakePronunciation { IsConfigured = false });

            // Never talk to the real Telegram from tests: without an app URL the real messenger is "not configured".
            if (Messenger is not null)
            {
                services.RemoveAll<ITelegramMessenger>();
                services.AddSingleton(Messenger);
            }
        });
    }

    /// <summary>
    /// A client signed in as this Telegram user. Real sign-in providers come in stage 1 (spec §6.3); until then the
    /// tests sign in through <see cref="TestEndpoints"/>, which exist only in this factory.
    /// </summary>
    public async Task<HttpClient> SignedInClientAsync(long telegramUserId, string? firstName = "Test")
    {
        var client = CreateClient();
        var response = await client.PostAsync(
            $"{TestSignInPath}?tg={telegramUserId.ToString(CultureInfo.InvariantCulture)}&name={Uri.EscapeDataString(firstName ?? "")}", null);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    /// <summary>
    /// Adds words to the learner's table <paramref name="table"/> as the vocabulary service does, with both cards.
    /// Interim until the shared catalog (spec §5); the order continues across tables, as in a frequency list.
    /// </summary>
    public async Task AddWordsAsync(long telegramUserId, int table, params (string Korean, string Uzbek)[] rows)
    {
        await using var scope = Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserService>().EnsureAsync(telegramUserId, null, null, CancellationToken.None);
        var words = rows.Select((r, i) => new NewWord(table, ((table - 1) * 100) + i + 1, r.Korean, null, r.Uzbek)).ToList();
        await scope.ServiceProvider.GetRequiredService<VocabularyService>().AddAsync(user.Id, words, CancellationToken.None);
    }

    /// <summary>Generated words "가가", "가나", … with meanings "ma’no 1", "ma’no 2", ….</summary>
    public static (string, string)[] Words(string prefix, int count) =>
        Enumerable.Range(0, count).Select(i => ($"{prefix}{(char)('가' + i)}", $"ma’no {i + 1}")).ToArray();

    /// <summary>
    /// Test-only endpoints: a sign-in that issues the same bearer tokens as the real ones for a given Telegram user id,
    /// and a way to give the signed-in learner words (the shared catalog comes in stages 2–3).
    /// </summary>
    private sealed class TestEndpoints(ApiFactory factory) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Map(TestWordsPath, branch => branch.Run(async http =>
            {
                var auth = await http.AuthenticateAsync(BearerTokenDefaults.AuthenticationScheme);
                if (auth.Principal is not { } principal)
                {
                    http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                var table = int.TryParse(http.Request.Query["table"], CultureInfo.InvariantCulture, out var t) ? t : 1;
                var words = factory.TableWords.Select((w, i) => new NewWord(table, ((table - 1) * 100) + i + 1, w.Korean, null, w.Uzbek)).ToList();
                await http.RequestServices.GetRequiredService<VocabularyService>().AddAsync(principal.GetUserId(), words, http.RequestAborted);
            }));
            app.Map(TestSignInPath, branch => branch.Run(async http =>
            {
                var telegramId = long.Parse(http.Request.Query["tg"]!, CultureInfo.InvariantCulture);
                var name = http.Request.Query["name"].ToString() is { Length: > 0 } n ? n : null;
                var user = await http.RequestServices.GetRequiredService<UserService>().EnsureAsync(telegramId, name, null, http.RequestAborted);
                await http.SignInAsync(BearerTokenDefaults.AuthenticationScheme, AuthEndpoints.CreatePrincipal(user.Id, telegramId, name, null));
            }));
            next(app);
        };
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
