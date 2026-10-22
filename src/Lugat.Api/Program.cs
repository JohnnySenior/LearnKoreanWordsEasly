using Lugat.Api.Ai;
using Lugat.Api.Auth;
using Lugat.Api.Backups;
using Lugat.Api.Grammar;
using Lugat.Api.Imports;
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
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Enums travel as names ("NeedsReview"), which keeps the API readable and safe to reorder.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(ProblemDetailsUz.Configure);
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.TryAddSingleton(TimeProvider.System);

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddSingleton<TelegramInitDataValidator>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ImportWorker>();
builder.Services.AddHostedService<ReminderWorker>();
builder.Services.AddHostedService<BackupWorker>();
builder.Services.AddHostedService<GrammarWorker>();
builder.Services.AddHostedService<BookTestWorker>();

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services
    .AddAuthentication(BearerTokenDefaults.AuthenticationScheme)
    .AddBearerToken(o =>
    {
        o.BearerTokenExpiration = authOptions.AccessTokenLifetime;
        o.RefreshTokenExpiration = authOptions.RefreshTokenLifetime;
    });
builder.Services.AddAuthorization();

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

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

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
app.MapImportEndpoints();
app.MapStudyEndpoints();
app.MapMeEndpoints();
app.MapAiEndpoints();
app.MapBackupEndpoints();
app.MapGrammarEndpoints();
app.MapSpeechEndpoints();
app.MapTestEndpoints();
app.MapLadderEndpoints();

// Unknown API routes must be a JSON 404, not the SPA shell.
app.Map("/api/{**rest}", () => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound));

// Blazor WebAssembly client (src/Lugat.Web) is served from the same origin.
app.MapStaticAssets();
app.MapFallbackToFile("index.html");

await app.RunAsync();

public partial class Program;

