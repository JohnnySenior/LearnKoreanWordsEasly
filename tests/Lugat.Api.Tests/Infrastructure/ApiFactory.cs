using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lugat.Api.Auth;
using Lugat.Application.Abstractions;
using Lugat.Application.Users;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>This factory's own database (each factory has one), e.g. to count its SQL commands.</summary>
    public string DatabaseName => _database;
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

    /// <summary>
    /// Test-only: publishes <see cref="TableWords"/> as catalog unit A-1 (or ?table=n) and opens it for the signed-in
    /// learner, which creates their cards as the real unit page does.
    /// </summary>
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

    /// <summary>Stands in for Google's published keys (tests sign their own ID tokens).</summary>
    public Lugat.Api.Auth.IGoogleSigningKeys? GoogleKeys { get; init; }

    /// <summary>
    /// Test learners get Premium when they sign in, so the older feature tests run as before; Premium tests turn it
    /// off and pay like a learner would (stage 5).
    /// </summary>
    public bool Premium { get; init; } = true;

    /// <summary>Telegram users who are administrators (config Auth:Admins, L-14). None by default.</summary>
    public long[] AdminTelegramIds { get; init; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:Default", new NpgsqlConnectionStringBuilder(ServerConnection) { Database = _database }.ConnectionString);
        builder.UseSetting("Storage:Root", _files);
        builder.UseSetting("Telegram:BotToken", BotToken);
        builder.UseSetting("Auth:DevLoginUserId", UserA.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Backup:Enabled", "false");
        for (var i = 0; i < AdminTelegramIds.Length; i++)
        {
            builder.UseSetting($"Auth:Admins:TelegramUserIds:{i}", AdminTelegramIds[i].ToString(CultureInfo.InvariantCulture));
        }
        // production turns the nightly backup on; tests run it by hand
        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<IStartupFilter>(new TestEndpoints(this));
            if (GoogleKeys is not null)
            {
                services.RemoveAll<Lugat.Api.Auth.IGoogleSigningKeys>();
                services.AddSingleton(GoogleKeys);
            }

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
    /// Publishes <paramref name="rows"/> as unit A-<paramref name="table"/> (if it is not there yet) and opens it for
    /// the learner with their cards. Tables of the old tests are units of level A now.
    /// </summary>
    public async Task AddWordsAsync(long telegramUserId, int table, params (string Korean, string Uzbek)[] rows)
    {
        await using var scope = Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserService>().SignInAsync(TelegramLogin(telegramUserId, null), CancellationToken.None);
        await OpenUnitAsync(scope.ServiceProvider, user.Id, table, rows);
    }

    /// <summary>The id of test unit A-<paramref name="number"/>: fixed, so tests can name it before it exists.</summary>
    public static Guid Unit(int number) => new($"00000000-0000-0000-0000-{number:D12}");

    /// <summary>The unit A-<paramref name="number"/>, published with these words when it does not exist yet.</summary>
    public static async Task<Lugat.Domain.Catalog.Unit> PublishUnitAsync(IServiceProvider services, int number, IReadOnlyList<(string Korean, string Uzbek)> rows)
    {
        var db = services.GetRequiredService<Lugat.Infrastructure.Persistence.AppDbContext>();
        var lockLease = await services.GetRequiredService<IOperationLock>().AcquireAsync(Lugat.Application.Catalog.CatalogAdminService.LockKey, CancellationToken.None);
        await using (lockLease)
        {
            var unit = await db.Units.SingleOrDefaultAsync(u => u.Level == Lugat.Domain.Catalog.CatalogLevel.A && u.Number == number);
            if (unit is not null)
            {
                return unit;
            }

            var now = DateTimeOffset.UtcNow;
            unit = new Lugat.Domain.Catalog.Unit
            {
                Id = Unit(number),
                Level = Lugat.Domain.Catalog.CatalogLevel.A,
                Number = number,
                Status = Lugat.Domain.Catalog.UnitStatus.Published,
                PublishedAt = now,
                WordCount = rows.Count,
                CreatedAt = now,
            };
            db.Units.Add(unit);
            db.CatalogWords.AddRange(rows.Select((r, i) => new Lugat.Domain.Catalog.CatalogWord
            {
                Id = Guid.CreateVersion7(),
                SourceKey = $"A:{r.Korean}:t{number}-{i}",
                Word = r.Korean,
                Pos = "명",
                Level = Lugat.Domain.Catalog.CatalogLevel.A,
                FreqRank = (number * 100) + i,
                IsStudyable = true,
                UnitId = unit.Id,
                OrderInUnit = i + 1,
                TranslationUz = r.Uzbek,
                Status = Lugat.Domain.Catalog.CatalogWordStatus.Reviewed,
                CreatedAt = now,
                UpdatedAt = now,
            }));
            await db.SaveChangesAsync();
            await Lugat.Application.Catalog.LearningItemSync.SyncUnitAsync(db, unit, now, CancellationToken.None);
            await db.SaveChangesAsync();
            return unit;
        }
    }

    /// <summary>Lets the learner into the unit as passing the one before would (stage 4) and opens it (cards).</summary>
    public static async Task OpenUnitAsync(IServiceProvider services, Guid userId, int number, IReadOnlyList<(string Korean, string Uzbek)> rows)
    {
        var unit = await PublishUnitAsync(services, number, rows);
        var db = services.GetRequiredService<Lugat.Infrastructure.Persistence.AppDbContext>();
        if (!await db.UnitProgress.AnyAsync(p => p.UserId == userId && p.UnitId == unit.Id))
        {
            db.UnitProgress.Add(new Lugat.Domain.Catalog.UserUnitProgress
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                UnitId = unit.Id,
                Status = Lugat.Domain.Catalog.UnitProgressStatus.Open,
                OpenedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await services.GetRequiredService<Lugat.Application.Catalog.UnitProgressService>().OpenAsync(userId, unit.Id, CancellationToken.None);
    }

    /// <summary>A Telegram login as the widget check would produce it.</summary>
    public static VerifiedLogin TelegramLogin(long telegramUserId, string? name) =>
        new(Lugat.Domain.Users.LoginProvider.Telegram, telegramUserId.ToString(CultureInfo.InvariantCulture), name, null);

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
                await OpenUnitAsync(http.RequestServices, principal.GetUserId(), table, factory.TableWords);
            }));
            app.Map(TestSignInPath, branch => branch.Run(async http =>
            {
                var telegramId = long.Parse(http.Request.Query["tg"]!, CultureInfo.InvariantCulture);
                var name = http.Request.Query["name"].ToString() is { Length: > 0 } n ? n : null;
                var user = await http.RequestServices.GetRequiredService<UserService>().SignInAsync(TelegramLogin(telegramId, name), http.RequestAborted);
                if (factory.Premium)
                {
                    var db = http.RequestServices.GetRequiredService<Lugat.Infrastructure.Persistence.AppDbContext>();
                    if (!await db.Subscriptions.AnyAsync(s => s.UserId == user.Id && s.Plan == Lugat.Domain.Premium.SubscriptionPlan.Gift))
                    {
                        db.Subscriptions.Add(new Lugat.Domain.Premium.Subscription
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            Plan = Lugat.Domain.Premium.SubscriptionPlan.Gift,
                            StartsAt = Start.AddYears(-1),
                            EndsAt = Start.AddYears(10),
                            Source = Lugat.Domain.Premium.SubscriptionSource.Admin,
                            CreatedAt = Start,
                        });
                        await db.SaveChangesAsync();
                    }
                }

                await http.SignInAsync(BearerTokenDefaults.AuthenticationScheme, AuthEndpoints.CreatePrincipal(user));
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
