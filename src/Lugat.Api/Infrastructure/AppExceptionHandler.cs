using Lugat.Application;
using Microsoft.AspNetCore.Diagnostics;

namespace Lugat.Api.Infrastructure;

/// <summary>Maps expected application failures to ProblemDetails with their Uzbek message (spec API03).</summary>
public sealed class AppExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public const string PremiumRequiredType = "premium-required";

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not AppException app)
        {
            return false;
        }

        context.Response.StatusCode = app switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            PremiumRequiredException or ForbiddenException => StatusCodes.Status403Forbidden,
            ConflictException => StatusCodes.Status409Conflict,
            PayloadTooLargeException => StatusCodes.Status413PayloadTooLarge,
            ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
            TooManyRequestsException => StatusCodes.Status429TooManyRequests,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status422UnprocessableEntity,
        };

        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            // The client opens the Premium page on this type (spec §11: 403, not 402).
            ProblemDetails = { Status = context.Response.StatusCode, Detail = app.UserMessage, Type = app is PremiumRequiredException ? PremiumRequiredType : null },
        });
    }
}
