using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application.Speech;

namespace Lugat.Api.Speech;

public static class SpeechEndpoints
{
    public static IEndpointRouteBuilder MapSpeechEndpoints(this IEndpointRouteBuilder app)
    {
        // Korean pronunciation as MP3 (spec AU01). The client fetches it with its token and plays it from memory.
        app.MapGet("/api/tts", async (string? text, ClaimsPrincipal user, SpeechService s, HttpContext http, CancellationToken ct) =>
        {
            var audio = await s.GetAsync(user.GetUserId(), text, ct);
            http.Response.Headers.CacheControl = "private, max-age=86400";
            return Results.File(audio, "audio/mpeg");
        }).RequireAuthorization();

        // Speaking practice (spec AU02–AU04, D-32): a short WAV and, for pronunciation, the text that should be said.
        app.MapPost("/api/speech/assess", async (HttpRequest request, ClaimsPrincipal user, SpeakingService s, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.Problem(detail: "Yozuv yuborilmadi.", statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var form = await request.ReadFormAsync(ct);
            if (form.Files["audio"] is not { Length: > 0 and <= SpeakingService.MaxBytes } file)
            {
                return Results.Problem(detail: "Yozuv yo‘q yoki juda uzun (ko‘pi bilan 30 soniya).", statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            return Results.Ok(await s.AssessAsync(user.GetUserId(), ms.ToArray(), form["reference"].FirstOrDefault(), ct));
        }).RequireAuthorization().DisableAntiforgery();

        return app;
    }
}
