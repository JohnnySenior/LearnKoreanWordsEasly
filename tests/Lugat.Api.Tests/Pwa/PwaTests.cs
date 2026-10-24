using System.Net;
using System.Text.Json;
using Lugat.Api.Tests.Infrastructure;

namespace Lugat.Api.Tests.Pwa;

/// <summary>What the browser needs to install the app and to keep it up to date (spec §6.2, §13.7).</summary>
/// <remarks>The test host serves the client's files from the build output, which needs the Development environment;
/// the published output (with the real service worker) is checked in a browser, see docs/CHECKLIST.md stage 6.</remarks>
public class PwaTests
{
    [Fact]
    public async Task The_manifest_makes_the_app_installable()
    {
        await using var factory = new ApiFactory { EnvironmentName = "Development" };
        var client = factory.CreateClient();

        var response = await client.GetAsync("/manifest.webmanifest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/manifest+json", response.Content.Headers.ContentType?.MediaType);
        using var manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = manifest.RootElement;
        Assert.Equal("Koreyscha", root.GetProperty("short_name").GetString());
        Assert.Equal("standalone", root.GetProperty("display").GetString());
        Assert.Equal("/", root.GetProperty("start_url").GetString());
        Assert.Equal("uz", root.GetProperty("lang").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("theme_color").GetString()));
        Assert.False(string.IsNullOrEmpty(root.GetProperty("background_color").GetString()));

        var icons = root.GetProperty("icons").EnumerateArray().ToList();
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() == "192x192");
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() == "512x512" && i.GetProperty("purpose").GetString() == "any");
        Assert.Contains(icons, i => i.GetProperty("purpose").GetString() == "maskable");
        foreach (var icon in icons)
        {
            var image = await client.GetAsync("/" + icon.GetProperty("src").GetString());
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task The_page_links_the_manifest_and_the_iphone_icon()
    {
        await using var factory = new ApiFactory { EnvironmentName = "Development" };
        var html = await factory.CreateClient().GetStringAsync("/bolimlar");

        Assert.Contains("rel=\"manifest\" href=\"manifest.webmanifest\"", html);
        // "?v=2": a new icon under the old name; Safari would keep the cached one when re-adding to the home screen.
        Assert.Contains("rel=\"apple-touch-icon\" href=\"icons/apple-touch-icon.png?v=2\"", html);
        Assert.Contains("rel=\"icon\" href=\"favicon.ico?v=2\"", html);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/icons/apple-touch-icon.png?v=2")).StatusCode);
        Assert.Contains("service-worker.js", html);
        Assert.DoesNotContain("telegram-web-app.js", html);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/favicon.ico")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/icons/favicon.svg")).StatusCode);
    }

    [Fact]
    public async Task The_service_worker_is_always_revalidated()
    {
        await using var factory = new ApiFactory { EnvironmentName = "Development" };

        var response = await factory.CreateClient().GetAsync("/service-worker.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache == true || response.Headers.CacheControl?.MaxAge == TimeSpan.Zero);
    }
}
