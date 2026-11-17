namespace Lugat.Api.Seo;

/// <summary>
/// L-86 (UI audit 12): the first path segments the Blazor app owns. These, and "/", get the app's index.html; any other
/// unknown address gets a real 404 page instead of the home page with 200 (a "soft 404" for search engines).
/// <c>AppRoutesTests</c> keeps this list in step with the <c>@page</c> directives of <c>src/Lugat.Web</c>.
/// </summary>
public static class AppRoutes
{
    public static readonly IReadOnlyList<string> FirstSegments =
    [
        "admin", "bolimlar", "diagnostika", "gap", "join", "kontent", "mashq", "maxfiylik", "natijalar", "not-found",
        "oqituvchi", "ornatish", "premium", "profil", "sessiya", "sinov", "sozlamalar", "sozlarim", "takrorlash",
        "tinglash", "tuzoqlar", "xatolar", "zaif-joylar",
    ];

    /// <summary>The route pattern of the app's own pages: one of <see cref="FirstSegments"/>, then anything but a file.</summary>
    public static string Pattern => $"{{page:regex(^(?:{string.Join('|', FirstSegments)})$)}}/{{**rest:nonfile}}";
}
