using System.Diagnostics;
using System.Security.Claims;
using KoreanAiTeacher.Api.Auth;
using KoreanAiTeacher.Application;
using KoreanAiTeacher.Application.Ai;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Domain.Ai;

namespace KoreanAiTeacher.Api.Ai;

public static class AiEndpoints
{
    private const string PingSchema = """
        { "type": "object", "additionalProperties": false, "required": ["ok"], "properties": { "ok": { "type": "boolean" } } }
        """;

    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var ai = app.MapGroup("/api/ai").RequireAuthorization();

        ai.MapGet("/budget", (AiUsageService usage, CancellationToken ct) => usage.BudgetAsync(ct));

        ai.MapGet("/status", (ClaimsPrincipal user, AiGateway gateway, CancellationToken ct) =>
            gateway.StatusAsync(user.GetUserId(), ct));

        ai.MapPut("/provider", (ChooseAiProviderRequest body, ClaimsPrincipal user, AiGateway gateway, CancellationToken ct) =>
            gateway.ChooseAsync(user.GetUserId(), body.Provider, ct));

        // "Ulanishni tekshirish": one tiny call to exactly this service, so a wrong key or model name shows up at once.
        ai.MapPost("/ping/{provider}", async (AiProviderKind provider, ClaimsPrincipal user, AiGateway gateway, CancellationToken ct) =>
        {
            var watch = Stopwatch.StartNew();
            try
            {
                var result = await gateway.CompleteJsonAsync(
                    user.GetUserId(),
                    new AiJsonRequest("ping", "Answer with the JSON object {\"ok\": true}.", "ping", "ping", PingSchema, 50),
                    ct,
                    only: provider);
                return new AiPingResult(true, provider, result.Response.Model, (int)watch.ElapsedMilliseconds, "Ulanish ishlayapti.");
            }
            catch (ServiceUnavailableException ex)
            {
                return new AiPingResult(false, provider, null, null, ex.UserMessage);
            }
        });

        var practice = app.MapGroup("/api/practice").RequireAuthorization();
        practice.MapGet("/target", (Guid? wordSenseId, ClaimsPrincipal user, SentenceCheckService s, CancellationToken ct) =>
            s.TargetAsync(user.GetUserId(), wordSenseId, ct));
        practice.MapPost("/check", (SentenceCheckRequest body, ClaimsPrincipal user, SentenceCheckService s, CancellationToken ct) =>
            s.CheckAsync(user.GetUserId(), body, ct));

        return app;
    }
}

