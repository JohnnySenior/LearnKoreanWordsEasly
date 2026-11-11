using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Vocabulary;
using Lugat.Contracts;

namespace Lugat.Api.MyWords;

/// <summary>"Mening so‘zlarim" (L-51): the learner's own words and decks.</summary>
public static class MyWordEndpoints
{
    public static IEndpointRouteBuilder MapMyWordEndpoints(this IEndpointRouteBuilder app)
    {
        var words = app.MapGroup("/api/my-words").RequireAuthorization();
        words.MapGet("/", (string? deck, string? q, MyWordState? state, ClaimsPrincipal user, PersonalWordService s, CancellationToken ct) =>
            s.ListAsync(user.GetUserId(), deck, q, state, ct));
        // A teacher's deck as the learner sees it (L-68).
        words.MapGet("/group-decks/{id:guid}", (Guid id, ClaimsPrincipal user, PersonalWordService s, CancellationToken ct) =>
            s.GroupDeckAsync(user.GetUserId(), id, ct));
        words.MapPost("/lookup", (MyWordLookupRequest body, ClaimsPrincipal user, PersonalWordService s, CancellationToken ct) =>
            s.LookupAsync(user.GetUserId(), body, ct));
        words.MapPost("/", (AddMyWordRequest body, ClaimsPrincipal user, PersonalWordService s, CancellationToken ct) =>
            s.AddAsync(user.GetUserId(), body, ct));
        words.MapPut("/{id:guid}/deck", (Guid id, MoveMyWordRequest body, ClaimsPrincipal user, PersonalWordService s, CancellationToken ct) =>
            s.MoveAsync(user.GetUserId(), id, body, ct));
        words.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, PersonalWordService s, CancellationToken ct) =>
        {
            await s.RemoveAsync(user.GetUserId(), id, ct);
            return Results.NoContent();
        });

        var decks = app.MapGroup("/api/word-decks").RequireAuthorization();
        decks.MapPost("/", (DeckNameRequest body, ClaimsPrincipal user, PersonalWordService s, CancellationToken ct) =>
            s.CreateDeckAsync(user.GetUserId(), body, ct));
        decks.MapPut("/{id:guid}", (Guid id, DeckNameRequest body, ClaimsPrincipal user, PersonalWordService s, CancellationToken ct) =>
            s.RenameDeckAsync(user.GetUserId(), id, body, ct));
        decks.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, PersonalWordService s, CancellationToken ct) =>
        {
            await s.DeleteDeckAsync(user.GetUserId(), id, ct);
            return Results.NoContent();
        });
        return app;
    }
}
