using System.Net;
using System.Xml.Linq;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Domain.Catalog;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Seo;

public class SeoPageTests
{
    /// <summary>A-1 published with these words, then the given changes to the catalog.</summary>
    private static async Task SetupAsync(ApiFactory factory, Action<AppDbContext, List<CatalogWord>> change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await ApiFactory.PublishUnitAsync(scope.ServiceProvider, 1, [("사랑", "sevgi"), ("가다", "bormoq"), ("갔다", "bordi"), ("물", "suv"), ("학교", "maktab")]);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var words = await db.CatalogWords.OrderBy(w => w.OrderInUnit).ToListAsync();
        words[0].Romanization = "sarang";
        words[0].ExampleKo = "사랑해요 <b>\"꼭\"</b>";
        words[0].ExampleUz = "Sevaman & yaxshi ko‘raman";
        words[1].Romanization = "gada";
        words[2].Romanization = "Gada!"; // the same slug as 가다: it must get another one
        words[3].Romanization = "mul";
        words[4].Romanization = "hakgyo";
        change(db, words);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_reviewed_word_of_a_published_unit_has_a_full_page_with_title_canonical_and_json_ld()
    {
        await using var factory = new ApiFactory();
        await SetupAsync(factory, (_, _) => { });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/soz/sarang");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("public", response.Headers.CacheControl!.ToString());
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>사랑 (sarang) — o‘zbekcha tarjimasi va misol | Koreyscha</title>", html);
        Assert.Contains("<link rel=\"canonical\" href=\"https://koreyscha.com/soz/sarang\">", html);
        Assert.Contains("<meta property=\"og:url\" content=\"https://koreyscha.com/soz/sarang\">", html);
        Assert.Contains("\"@type\":\"DefinedTerm\"", html);
        Assert.Contains("\"name\":\"Koreyscha lug‘ati\"", html);
        Assert.Contains($"href=\"/bolimlar/{ApiFactory.Unit(1)}?src=soz\"", html);
        // The app reads ?src= (MainLayout), so visits from the word pages reach Admin → Manbalar.
        Assert.DoesNotContain("from=soz", html);
        Assert.Contains("href=\"/?src=soz\"", html);
        Assert.Contains("href=\"/sinab?src=soz\"", html); // the sign-in-free taster (L-58)
        // Neighbours in the unit are linked.
        Assert.Contains("href=\"/soz/mul\"", html);
        Assert.Contains("국립국어원", html);
        Assert.DoesNotContain("<script src", html);
    }

    [Fact]
    public async Task The_description_never_doubles_a_full_stop_the_example_already_has()
    {
        await using var factory = new ApiFactory();
        await SetupAsync(factory, (_, words) =>
        {
            words[3].ExampleKo = "물을 마셔요.";
            words[3].TranslationUz = "suv.";
        });
        var html = await factory.CreateClient().GetStringAsync("/soz/mul");

        Assert.Contains("<meta name=\"description\" content=\"물 (mul) — suv. Koreyscha so‘zning o‘zbekcha tarjimasi va misol: 물을 마셔요. Koreyscha", html);
        Assert.DoesNotContain("..", html[html.IndexOf("<meta name=\"description\"", StringComparison.Ordinal)..][..300]);
        // Without an example the sentence still ends once.
        var plain = await factory.CreateClient().GetStringAsync("/soz/hakgyo");
        Assert.Contains("o‘zbekcha tarjimasi. Koreyscha ilovasida", plain);
    }

    [Fact]
    public async Task Hangul_and_special_characters_are_encoded()
    {
        await using var factory = new ApiFactory();
        await SetupAsync(factory, (_, _) => { });
        var html = await factory.CreateClient().GetStringAsync("/soz/sarang");

        Assert.Contains("사랑해요 &lt;b&gt;&quot;꼭&quot;&lt;/b&gt;", html);
        Assert.Contains("Sevaman &amp; yaxshi ko‘raman", html);
        Assert.DoesNotContain("<b>\"꼭\"</b>", html);
        // In the JSON-LD block nothing can close the script tag.
        var script = html[html.IndexOf("application/ld+json", StringComparison.Ordinal)..];
        script = script[..script.IndexOf("</script>", StringComparison.Ordinal)];
        Assert.DoesNotContain("<", script[(script.IndexOf('>') + 1)..]);
    }

    [Fact]
    public async Task A_draft_word_or_a_word_of_a_draft_unit_is_not_found()
    {
        await using var factory = new ApiFactory();
        await SetupAsync(factory, (db, words) =>
        {
            words[3].Status = CatalogWordStatus.Draft; // 물
            var draft = new Unit { Id = Guid.NewGuid(), Level = CatalogLevel.A, Number = 2, Status = UnitStatus.Draft, WordCount = 1, CreatedAt = DateTimeOffset.UtcNow };
            db.Units.Add(draft);
            db.CatalogWords.Add(new CatalogWord
            {
                Id = Guid.NewGuid(), SourceKey = "A:책:draft", Word = "책", Pos = "명", Level = CatalogLevel.A, UnitId = draft.Id, OrderInUnit = 1,
                TranslationUz = "kitob", Romanization = "chaek", Status = CatalogWordStatus.Reviewed, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            });
        });
        var client = factory.CreateClient();

        var missing = await client.GetAsync("/soz/mul");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("noindex", await missing.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/soz/chaek")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/soz/yoq-soz")).StatusCode);
        var list = await client.GetStringAsync("/soz");
        Assert.DoesNotContain("/soz/mul\"", list);
        Assert.DoesNotContain("/soz/chaek", list);
    }

    [Fact]
    public async Task Words_with_the_same_romanization_get_different_lasting_addresses()
    {
        await using var factory = new ApiFactory();
        await SetupAsync(factory, (_, _) => { });
        var client = factory.CreateClient();

        var list = await client.GetStringAsync("/soz");
        Assert.Contains("href=\"/soz/gada\"", list);  // the more frequent one keeps the plain romanization
        Assert.Contains("href=\"/soz/gada-ot\"", list); // the other: its part of speech
        Assert.Contains("<h1 lang=\"ko\">갔다</h1>", await client.GetStringAsync("/soz/gada-ot"));

        // The romanization is edited later: the address stays.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var word = await db.CatalogWords.SingleAsync(w => w.Word == "가다");
            word.Romanization = "kada";
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/soz/gada")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/soz/kada")).StatusCode);
    }

    [Fact]
    public async Task The_sitemap_is_valid_xml_with_only_public_words_and_robots_points_to_it()
    {
        await using var factory = new ApiFactory();
        await SetupAsync(factory, (_, words) => words[4].Status = CatalogWordStatus.Draft); // 학교
        var client = factory.CreateClient();

        var response = await client.GetAsync("/sitemap.xml");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType!.MediaType);
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locs = xml.Root!.Elements(ns + "url").Select(u => u.Element(ns + "loc")!.Value).ToList();
        Assert.Contains("https://koreyscha.com/", locs);
        Assert.Contains("https://koreyscha.com/soz", locs);
        Assert.Contains("https://koreyscha.com/soz/sarang", locs);
        Assert.DoesNotContain("https://koreyscha.com/soz/hakgyo", locs);
        Assert.All(xml.Root.Elements(ns + "url").Where(u => u.Element(ns + "loc")!.Value.Contains("/soz/")),
            u => Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", u.Element(ns + "lastmod")!.Value));

        var robots = await client.GetStringAsync("/robots.txt");
        Assert.Contains("Sitemap: https://koreyscha.com/sitemap.xml", robots);
        Assert.Contains("Disallow: /api", robots);
        Assert.Contains("Disallow: /admin", robots);
    }

    private const string Note = "Bu yerda o‘zbekcha «suv ichmoq» deb so‘zma-so‘z aytiladi. Koreyschada 마시다 bilan aytiladi.";

    /// <summary>물 with a confirmed trap scene, 학교 with only the note and an unconfirmed scene, 사랑 with none (L-65).</summary>
    private static Task SetupTrapsAsync(ApiFactory factory) => SetupAsync(factory, (_, words) =>
    {
        (words[3].TrapUz, words[3].TrapCue, words[3].TrapWrong, words[3].TrapRight) = (Note, "Do‘stingizga aytasiz:", "물을 먹어요 <틀림>", "물을 마셔요");
        (words[4].TrapUz, words[4].TrapWrong, words[4].TrapRight, words[4].TrapScenePending) = ("Maktab haqida izoh.", "학교 A", "학교 B", true);
    });

    [Fact]
    public async Task A_word_with_a_confirmed_trap_shows_the_question_and_the_note_and_loads_the_trap_script()
    {
        await using var factory = new ApiFactory();
        await SetupTrapsAsync(factory);
        var html = await factory.CreateClient().GetStringAsync("/soz/mul");

        Assert.Contains("<section class=\"trap-note\"><h2>O‘zbeklar shu yerda adashadi</h2>", html);
        Assert.Contains($"<p class=\"trap-text\">{System.Net.WebUtility.HtmlEncode(Note)}</p>", html);
        Assert.Contains("<p class=\"ask\">Qaysi biri to‘g‘ri?</p>", html);
        Assert.Contains("data-right=\"0\">물을 먹어요 &lt;틀림&gt;</button>", html);
        Assert.Contains("data-right=\"1\">물을 마셔요</button>", html);
        Assert.Contains("<p class=\"cue\">Do‘stingizga aytasiz:</p>", html);
        Assert.Contains("<script type=\"module\" src=\"/js/sinab.js\"></script>", html);
        // The block sits after the example and before the call to action; the note is not repeated in the answer.
        Assert.True(html.IndexOf("trap-note", StringComparison.Ordinal) < html.IndexOf("class=\"cta\"", StringComparison.Ordinal));
        Assert.Equal(1, CountOf(html[html.IndexOf("<main>", StringComparison.Ordinal)..], System.Net.WebUtility.HtmlEncode("«suv ichmoq»"))); // the head has it in the description
        // No #start and no taster data: sinab.js wires the question only and counts no visit.
        Assert.DoesNotContain("id=\"start\"", html);
        Assert.DoesNotContain("taster-data", html);
    }

    [Fact]
    public async Task An_unconfirmed_scene_shows_only_the_note_and_a_word_without_a_trap_shows_nothing()
    {
        await using var factory = new ApiFactory();
        await SetupTrapsAsync(factory);
        var client = factory.CreateClient();

        var pending = await client.GetStringAsync("/soz/hakgyo");
        Assert.Contains("<p class=\"trap-text\">Maktab haqida izoh.</p>", pending); // checked with the word, as on the card
        Assert.DoesNotContain("class=\"trap\"", pending);
        Assert.DoesNotContain("학교 A", pending);
        Assert.DoesNotContain("sinab.js", pending);

        var none = await client.GetStringAsync("/soz/sarang");
        Assert.DoesNotContain("class=\"trap-note\"", none);
        Assert.DoesNotContain("adashadi", none);
        Assert.DoesNotContain("sinab.js", none);
    }

    [Fact]
    public async Task The_src_of_the_link_reaches_every_way_into_the_app_but_not_the_canonical()
    {
        await using var factory = new ApiFactory();
        await SetupTrapsAsync(factory);
        var client = factory.CreateClient();

        var ig = await client.GetStringAsync("/soz/mul?src=IG");
        Assert.Contains($"href=\"/bolimlar/{ApiFactory.Unit(1)}?src=soz-ig\"", ig);
        Assert.Contains("href=\"/sinab?src=soz-ig\"", ig);
        Assert.Contains("href=\"/?src=soz-ig\"", ig);
        Assert.Contains("<link rel=\"canonical\" href=\"https://koreyscha.com/soz/mul\">", ig);
        Assert.Contains("<meta property=\"og:url\" content=\"https://koreyscha.com/soz/mul\">", ig);

        foreach (var bad in new[] { "<script>", new string('a', 40), "  " })
        {
            var html = await client.GetStringAsync("/soz/mul?src=" + Uri.EscapeDataString(bad));
            Assert.Contains($"href=\"/bolimlar/{ApiFactory.Unit(1)}?src=soz\"", html);
            Assert.Contains("href=\"/?src=soz\"", html);
            Assert.DoesNotContain("<script>", html);
        }

        // An upper-case address keeps the src on its way to the lasting one.
        var redirect = await factory.CreateClient(new() { AllowAutoRedirect = false }).GetAsync("/soz/MUL?src=ig");
        Assert.Equal(HttpStatusCode.MovedPermanently, redirect.StatusCode);
        Assert.Equal("/soz/mul?src=ig", redirect.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task The_description_carries_the_first_sentence_of_the_trap_without_a_double_full_stop()
    {
        await using var factory = new ApiFactory();
        await SetupTrapsAsync(factory);
        var html = await factory.CreateClient().GetStringAsync("/soz/mul");

        var meta = html[html.IndexOf("<meta name=\"description\"", StringComparison.Ordinal)..];
        meta = meta[..meta.IndexOf('>')];
        Assert.Contains("물 (mul) — suv. Bu yerda o‘zbekcha «suv ichmoq» deb so‘zma-so‘z aytiladi. Koreyscha ilovasida bepul yodlang.", System.Net.WebUtility.HtmlDecode(meta));
        Assert.DoesNotContain("마시다", meta); // only the first sentence
        Assert.DoesNotContain("..", meta);
    }

    [Fact]
    public async Task One_json_ld_block_holds_the_term_and_its_breadcrumb_with_absolute_urls()
    {
        await using var factory = new ApiFactory();
        await SetupAsync(factory, (_, words) => words[3].Romanization = null); // 물: no romanization, no alternateName
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Lugat.Application.Seo.SeoService>().EnsureSlugsAsync(CancellationToken.None);
        }

        var html = await factory.CreateClient().GetStringAsync("/soz/sarang");
        Assert.Equal(1, CountOf(html, "application/ld+json"));
        var start = html.IndexOf("application/ld+json", StringComparison.Ordinal);
        var json = html[(html.IndexOf('>', start) + 1)..html.IndexOf("</script>", start, StringComparison.Ordinal)];
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var graph = doc.RootElement.GetProperty("@graph");
        Assert.Equal("https://schema.org", doc.RootElement.GetProperty("@context").GetString());

        var term = graph.EnumerateArray().Single(e => e.GetProperty("@type").GetString() == "DefinedTerm");
        Assert.Equal("사랑", term.GetProperty("name").GetString());
        Assert.Equal("sarang", term.GetProperty("alternateName").GetString());
        Assert.Equal("sevgi", term.GetProperty("description").GetString());
        Assert.Equal("https://koreyscha.com/soz/sarang", term.GetProperty("url").GetString());
        Assert.Equal("https://koreyscha.com/soz", term.GetProperty("inDefinedTermSet").GetProperty("url").GetString());

        var crumbs = graph.EnumerateArray().Single(e => e.GetProperty("@type").GetString() == "BreadcrumbList")
            .GetProperty("itemListElement").EnumerateArray()
            .Select(i => (i.GetProperty("position").GetInt32(), i.GetProperty("name").GetString(), i.GetProperty("item").GetString())).ToList();
        Assert.Equal([(1, "Koreyscha", "https://koreyscha.com/"), (2, "So‘zlar", "https://koreyscha.com/soz"), (3, "사랑", "https://koreyscha.com/soz/sarang")], crumbs);

        // The JSON-LD stays the same whatever src the link carried (L-65).
        var withSource = await factory.CreateClient().GetStringAsync("/soz/sarang?src=ig");
        Assert.DoesNotContain("src=", withSource[withSource.IndexOf("application/ld+json", StringComparison.Ordinal)..withSource.IndexOf("</script>", withSource.IndexOf("application/ld+json", StringComparison.Ordinal), StringComparison.Ordinal)]);

        // A word without a romanization has no alternateName at all, not a null.
        var list = await factory.CreateClient().GetStringAsync("/soz");
        var slug = System.Text.RegularExpressions.Regex.Match(list, "href=\"/soz/([^\"]+)\"><span lang=\"ko\">물</span>").Groups[1].Value;
        var plain = await factory.CreateClient().GetStringAsync("/soz/" + slug);
        Assert.DoesNotContain("alternateName", plain);
        Assert.DoesNotContain("null", plain[plain.IndexOf("application/ld+json", StringComparison.Ordinal)..]);
    }

    private static int CountOf(string text, string part)
    {
        var n = 0;
        for (var i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal))
        {
            n++;
        }

        return n;
    }

    [Fact]
    public void Slugs_are_lower_case_latin_with_single_hyphens()
    {
        Assert.Equal("sa-rang", Lugat.Application.Seo.SeoService.Slugify(" Sa--Rang! "));
        Assert.Equal("fel", Lugat.Application.Seo.SeoService.Slugify("fe’l"));
        Assert.Equal("", Lugat.Application.Seo.SeoService.Slugify("사랑"));
        Assert.Equal("yordamchi-fel", Lugat.Application.Seo.SeoService.Slugify("yordamchi fe’l"));
    }
}
