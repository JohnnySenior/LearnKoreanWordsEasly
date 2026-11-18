using System.Net;
using System.Text.RegularExpressions;
using Lugat.Api.Seo;
using Lugat.Api.Tests.Infrastructure;

namespace Lugat.Api.Tests.Seo;

/// <summary>L-86 (UI audit 12): the app's pages get the app, any other unknown address a real 404 page.</summary>
public sealed partial class AppRoutesTests
{
    [Fact]
    public void Every_page_of_the_app_is_in_the_server_list()
    {
        var web = Path.Combine(RepoRoot(), "src", "Lugat.Web");
        var segments = Directory.EnumerateFiles(web, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => PageDirective().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Where(s => s.Length > 0)
            .ToHashSet();
        Assert.NotEmpty(segments);
        Assert.Empty(segments.Except(AppRoutes.FirstSegments));
    }

    [Fact]
    public async Task An_unknown_address_is_a_404_page_with_a_way_home_and_the_app_pages_are_the_app()
    {
        await using var f = new ApiFactory { EnvironmentName = "Development" };
        var client = f.CreateClient();

        var missing = await client.GetAsync("/nope");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var html = await missing.Content.ReadAsStringAsync();
        Assert.Contains("<h1>Sahifa topilmadi</h1>", html);
        Assert.Contains("<a class=\"cta\" href=\"/\">", html);
        Assert.Contains("<meta name=\"robots\" content=\"noindex\">", html);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/bolimlarx/1")).StatusCode);

        foreach (var path in new[] { "/", "/bolimlar", "/bolimlar/00000000-0000-0000-0000-000000000001", "/profil/hisob", "/sinov/oldingi", "/?src=tg" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<div id=\"app\">", await response.Content.ReadAsStringAsync());
        }

        // Files and the API keep their own 404s.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/nope.js")).StatusCode);
        Assert.Equal("application/problem+json", (await client.GetAsync("/api/nope")).Content.Headers.ContentType?.MediaType);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Lugat.slnx")) && !Directory.Exists(Path.Combine(dir.FullName, "src", "Lugat.Web")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    [GeneratedRegex("""^@page "/([^/"{]*)""", RegexOptions.Multiline)]
    private static partial Regex PageDirective();
}
