using Lugat.Api.Seo;
using Microsoft.Extensions.FileProviders;

namespace Lugat.Api.Tests.Seo;

/// <summary>
/// L-72: a public page fetches the app in the background so it opens in seconds; on a slow Android its first start
/// was 10–20 s. Only the code the app runs is listed, not the ICU data or the compressed copies.
/// </summary>
public class AppPrefetchTests
{
    [Fact]
    public void The_published_runtime_and_assemblies_are_listed_with_the_apps_own_css_and_js()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var framework = Directory.CreateDirectory(Path.Combine(root.FullName, "_framework"));
            foreach (var name in new[] { "dotnet.js", "dotnet.native.abc.wasm", "Lugat.Web.x1.wasm", "Lugat.Web.x1.wasm.br", "dotnet.js.gz", "icudt_CJK.q.dat", "blazor.webassembly.js" })
            {
                File.WriteAllText(Path.Combine(framework.FullName, name), "x");
            }

            using var provider = new PhysicalFileProvider(root.FullName);
            Assert.Equal(
                ["/_framework/Lugat.Web.x1.wasm", "/_framework/blazor.webassembly.js", "/_framework/dotnet.js", "/_framework/dotnet.native.abc.wasm", "/css/app.css", "/css/introduction.css", "/js/app.js", "/js/boot-signin.js"],
                SeoEndpoints.AppFiles(provider));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void Nothing_is_fetched_where_the_app_is_not_published()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            using var provider = new PhysicalFileProvider(root.FullName);
            Assert.Empty(SeoEndpoints.AppFiles(provider));
            Assert.Equal("", SeoEndpoints.PrefetchScript([], 1000));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void The_script_waits_for_the_page_and_adds_low_priority_prefetch_links()
    {
        var script = SeoEndpoints.PrefetchScript(["/_framework/dotnet.js", "/js/app.js"], 8000);
        Assert.StartsWith("<script>addEventListener('load',()=>setTimeout(", script);
        Assert.Contains("[\"/_framework/dotnet.js\",\"/js/app.js\"]", script);
        Assert.Contains("l.rel='prefetch'", script);
        Assert.EndsWith(",8000));</script>", script);
    }
}
