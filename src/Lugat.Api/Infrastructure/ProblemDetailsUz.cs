using Microsoft.AspNetCore.Http.Features;

namespace Lugat.Api.Infrastructure;

/// <summary>
/// Uzbek, user-readable ProblemDetails for status codes the framework produces on its own (spec API03).
/// Endpoints that set their own <c>detail</c> keep it. Stack traces are never included.
/// </summary>
public static class ProblemDetailsUz
{
    private static readonly Dictionary<int, string> Details = new()
    {
        [StatusCodes.Status400BadRequest] = "So‘rov noto‘g‘ri tuzilgan.",
        [StatusCodes.Status401Unauthorized] = "Kirish talab qilinadi. Ilovani Telegram ichidan qayta oching.",
        [StatusCodes.Status403Forbidden] = "Bu amal uchun ruxsat yo‘q.",
        [StatusCodes.Status404NotFound] = "Topilmadi.",
        [StatusCodes.Status409Conflict] = "Ma’lumot boshqa joyda o‘zgargan. Sahifani yangilab, qayta urinib ko‘ring.",
        [StatusCodes.Status422UnprocessableEntity] = "Kiritilgan ma’lumotni tekshiring.",
        [StatusCodes.Status429TooManyRequests] = "Juda ko‘p so‘rov. Birozdan keyin qayta urinib ko‘ring.",
        [StatusCodes.Status500InternalServerError] = "Kutilmagan xato yuz berdi. Qayta urinib ko‘ring.",
        [StatusCodes.Status503ServiceUnavailable] = "Xizmat vaqtincha ishlamayapti. Keyinroq urinib ko‘ring.",
    };

    public static void Configure(ProblemDetailsOptions options)
    {
        options.CustomizeProblemDetails = ctx =>
        {
            var status = ctx.ProblemDetails.Status ?? ctx.HttpContext.Response.StatusCode;
            if (string.IsNullOrEmpty(ctx.ProblemDetails.Detail) && Details.TryGetValue(status, out var detail))
            {
                ctx.ProblemDetails.Detail = detail;
            }

            ctx.ProblemDetails.Extensions.Remove("exception");
            ctx.ProblemDetails.Extensions["requestId"] =
                ctx.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier;
        };
    }
}
