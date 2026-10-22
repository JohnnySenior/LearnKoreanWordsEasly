using Lugat.Api.Ai;
using Lugat.Api.Auth;
using Lugat.Api.Backups;
using Lugat.Api.Catalog;
using Lugat.Api.Ladder;
using Lugat.Api.Infrastructure;
using Lugat.Api.Me;
using Lugat.Api.Speech;
using Lugat.Api.Study;
using Lugat.Api.Testing;
using Lugat.Infrastructure;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Lugat.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Enums travel as names ("NeedsReview"), which keeps the API readable and safe to reorder.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(ProblemDetailsUz.Configure);
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.TryAddSingleton(TimeProvider.System);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<TelegramLoginOptions>(builder.Configuration.GetSection(TelegramLoginOptions.SectionName));
builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection(GoogleOptions.SectionName));
builder.Services.AddSingleton<TelegramLoginValidator>();
builder.Services.AddSingleton<GoogleIdTokenValidator>();
builder.Services.AddHttpClient<IGoogleSigningKeys, GoogleJwksSigningKeys>(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ReminderWorker>();
builder.Services.AddHostedService<BackupWorker>();

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services
    .AddAuthentication(BearerTokenDefaults.AuthenticationScheme)
    .AddBearerToken(o =>
    {
        o.BearerTokenExpiration = authOptions.AccessTokenLifetime;
        o.RefreshTokenExpiration = authOptions.RefreshTokenLifetime;
    });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Policies.Admin, p => p.RequireRole(nameof(UserRoles.Admin)));
    o.AddPolicy(Policies.Editor, p => p.RequireRole(nameof(UserRoles.Admin), nameof(UserRoles.Editor)));
});

// Open sign-up (spec §6.3) needs limits (spec §6.4): sign-in per IP, starting tests per user.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        var problems = ctx.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx.HttpContext,
            ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Detail = "Juda ko‘p urinish. Birozdan keyin qayta urinib ko‘ring." },
        });
    };
    o.AddPolicy(AuthEndpoints.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = http.RequestServices.GetRequiredService<IOptionsMonitor<AuthOptions>>().CurrentValue.SignInPerMinute,
            Window = TimeSpan.FromMinutes(1),
        }));
    o.AddPolicy(TestEndpoints.StartRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        "user:" + (http.User.FindFirstValue(AuthEndpoints.UserIdClaim) ?? "anonymous"),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = http.RequestServices.GetRequiredService<IOptionsMonitor<AuthOptions>>().CurrentValue.TestStartsPerHour,
            Window = TimeSpan.FromHours(1),
        }));
});

// Session tokens are encrypted with Data Protection keys. In production they must live on the persistent
// volume, or every restart signs everyone out (docs/DECISIONS.md D-03).
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    builder.Services.AddDataProtection()
        .SetApplicationName("Lugat")
        .PersistKeysToFileSystem(Directory.CreateDirectory(keysPath));
}

var app = builder.Build();

// Single small instance: applying migrations at startup keeps deploys one step (see docs/DECISIONS.md D-08).
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

await app.SyncLearningItemsAsync();
await app.SeedSampleAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseSessionGuard();
app.UseAuthorization();
app.UseRateLimiter();

// Liveness is independent of dependencies; readiness checks the database for Railway's /health probe.
app.MapGet("/health/live", () => TypedResults.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeout.CancelAfter(TimeSpan.FromSeconds(3));
    try
    {
        return await db.Database.CanConnectAsync(timeout.Token)
            ? Results.Ok(new { status = "ready" })
            : Results.Problem(statusCode: 503, detail: "Ma’lumotlar bazasi hozir tayyor emas.");
    }
    catch (Exception) when (!ct.IsCancellationRequested)
    {
        return Results.Problem(statusCode: 503, detail: "Ma’lumotlar bazasi hozir tayyor emas.");
    }
}).AllowAnonymous();
app.MapAuthEndpoints(app.Environment);
app.MapStudyEndpoints();
app.MapMeEndpoints();
app.MapCatalogEndpoints();
app.MapAiEndpoints();
app.MapBackupEndpoints();
app.MapSpeechEndpoints();
app.MapTestEndpoints();
app.MapLadderEndpoints(app.Configuration);

// Unknown API routes must be a JSON 404, not the SPA shell.
app.Map("/api/{**rest}", () => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound));

// Blazor WebAssembly client (src/Lugat.Web) is served from the same origin.
app.MapStaticAssets();
app.MapFallbackToFile("index.html");

await app.RunAsync();

public partial class Program;

