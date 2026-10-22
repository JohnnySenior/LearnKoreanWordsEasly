using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Grammar;
using Lugat.Contracts;

namespace Lugat.Api.Grammar;

public static class GrammarEndpoints
{
    public static IEndpointRouteBuilder MapGrammarEndpoints(this IEndpointRouteBuilder app)
    {
        var grammar = app.MapGroup("/api/grammar").RequireAuthorization();

        grammar.MapPost("/books/{documentId:guid}/imports", async (Guid documentId, CreateGrammarImportRequest body, ClaimsPrincipal user, GrammarService s, CancellationToken ct) =>
        {
            var created = await s.CreateImportAsync(user.GetUserId(), documentId, body, ct);
            return TypedResults.Created($"/api/grammar/imports/{created.Id}", created);
        });
        grammar.MapGet("/imports", (ClaimsPrincipal user, GrammarService s, CancellationToken ct) =>
            s.ListImportsAsync(user.GetUserId(), ct));
        grammar.MapGet("/imports/{id:guid}", (Guid id, ClaimsPrincipal user, GrammarService s, CancellationToken ct) =>
            s.GetImportAsync(user.GetUserId(), id, ct));
        grammar.MapPost("/imports/{id:guid}/retry", (Guid id, ClaimsPrincipal user, GrammarService s, CancellationToken ct) =>
            s.RetryAsync(user.GetUserId(), id, ct));
        grammar.MapGet("/imports/{id:guid}/items", (Guid id, int? page, ClaimsPrincipal user, GrammarService s, CancellationToken ct) =>
            s.ListItemsAsync(user.GetUserId(), id, page, ct));
        grammar.MapPost("/imports/{id:guid}/pages/{page:int}/approve", async (Guid id, int page, ClaimsPrincipal user, GrammarService s, CancellationToken ct) =>
            new { approved = await s.ApprovePageAsync(user.GetUserId(), id, page, ct) });
        grammar.MapPatch("/items/{id:guid}", (Guid id, UpdateGrammarItemRequest body, ClaimsPrincipal user, GrammarService s, CancellationToken ct) =>
            s.UpdateItemAsync(user.GetUserId(), id, body, ct));


        // The grammar section as a place to study (D-26).
        grammar.MapGet("/items", (ClaimsPrincipal user, GrammarStudyService s, CancellationToken ct) =>
            s.ListAsync(user.GetUserId(), ct));
        grammar.MapGet("/items/{id:guid}", (Guid id, Guid? set, ClaimsPrincipal user, GrammarStudyService s, CancellationToken ct) =>
            s.GetAsync(user.GetUserId(), id, ct, set));
        grammar.MapPost("/items/{id:guid}/guide", (Guid id, bool? again, ClaimsPrincipal user, GrammarStudyService s, CancellationToken ct) =>
            s.GuideAsync(user.GetUserId(), id, again == true, ct));
        grammar.MapPost("/items/{id:guid}/notes", (Guid id, AskGrammarRequest body, ClaimsPrincipal user, GrammarStudyService s, CancellationToken ct) =>
            s.AskAsync(user.GetUserId(), id, body, ct));

        // Grammar sets: what the teacher gave to learn together, with a deadline (D-28).
        grammar.MapGet("/today", (ClaimsPrincipal user, GrammarSetService s, CancellationToken ct) =>
            s.TodayAsync(user.GetUserId(), ct));
        grammar.MapGet("/sets", (ClaimsPrincipal user, GrammarSetService s, CancellationToken ct) =>
            s.ListAsync(user.GetUserId(), ct));
        grammar.MapGet("/sets/{id:guid}", (Guid id, ClaimsPrincipal user, GrammarSetService s, CancellationToken ct) =>
            s.GetAsync(user.GetUserId(), id, ct));
        grammar.MapPost("/sets", async (CreateGrammarSetRequest body, ClaimsPrincipal user, GrammarSetService s, CancellationToken ct) =>
        {
            var created = await s.CreateAsync(user.GetUserId(), body, ct);
            return TypedResults.Created($"/api/grammar/sets/{created.Set.Id}", created);
        });
        grammar.MapPatch("/sets/{id:guid}", (Guid id, UpdateGrammarSetRequest body, ClaimsPrincipal user, GrammarSetService s, CancellationToken ct) =>
            s.UpdateAsync(user.GetUserId(), id, body, ct));
        grammar.MapDelete("/sets/{id:guid}", async (Guid id, ClaimsPrincipal user, GrammarSetService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(user.GetUserId(), id, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}

/// <summary>Reads queued grammar pages in the background (D-24). One page at a time; AI calls are slow.</summary>
public sealed class GrammarWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<GrammarWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = config.GetValue("Grammar:PollInterval", TimeSpan.FromSeconds(10));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<GrammarService>().ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Grammar worker iteration failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
