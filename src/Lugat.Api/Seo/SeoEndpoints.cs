using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Xml;
using Lugat.Application.Seo;
using Lugat.Domain.Catalog;

namespace Lugat.Api.Seo;

/// <summary>
/// Public, server-rendered pages for search engines and link previews (L-56): one page per public word, a list of
/// them, the sitemap and robots.txt. Plain HTML without Blazor or scripts; every text is HTML-encoded. The way in from
/// Google; the app itself stays the Blazor client.
/// </summary>
public static class SeoEndpoints
{
    private const string CacheControl = "public, max-age=21600";

    private static readonly JsonSerializerOptions JsonLd = new()
    {
        // Hangul stays readable; <, >, & and quotes are still escaped, so nothing can close the script tag.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        var baseUrl = (app.ServiceProvider.GetRequiredService<IConfiguration>()["Seo:BaseUrl"] ?? "https://koreyscha.com").TrimEnd('/');

        app.MapGet("/soz/{slug}", async (string slug, HttpContext http, SeoService seo, CancellationToken ct) =>
        {
            var word = await seo.WordAsync(slug, ct);
            if (word is null)
            {
                var lower = slug.ToLowerInvariant();
                if (lower != slug && await seo.WordAsync(lower, ct) is not null)
                {
                    return Results.Redirect($"/soz/{Uri.EscapeDataString(lower)}", permanent: true);
                }

                return NotFound(http);
            }

            http.Response.Headers.CacheControl = CacheControl;
            return Results.Content(WordPage(word, baseUrl), "text/html; charset=utf-8");
        }).AllowAnonymous();

        app.MapGet("/soz", async (int? sahifa, HttpContext http, SeoService seo, CancellationToken ct) =>
        {
            var list = await seo.ListAsync(sahifa ?? 1, ct);
            if (sahifa is { } asked && asked != list.Page)
            {
                return NotFound(http);
            }

            http.Response.Headers.CacheControl = CacheControl;
            return Results.Content(ListPage(list, baseUrl), "text/html; charset=utf-8");
        }).AllowAnonymous();

        app.MapGet("/sitemap.xml", async (HttpContext http, SeoService seo, CancellationToken ct) =>
        {
            var words = await seo.SitemapAsync(ct);
            var list = await seo.ListAsync(1, ct);
            http.Response.Headers.CacheControl = CacheControl;
            return Results.Content(Sitemap(words, list.Pages, baseUrl), "application/xml; charset=utf-8");
        }).AllowAnonymous();

        app.MapGet("/robots.txt", (HttpContext http) =>
        {
            http.Response.Headers.CacheControl = CacheControl;
            return Results.Text($"User-agent: *\nAllow: /\nDisallow: /admin\nDisallow: /api\n\nSitemap: {baseUrl}/sitemap.xml\n", "text/plain; charset=utf-8");
        }).AllowAnonymous();

        return app;
    }

    private static string Origin(string url) => new Uri(url).GetLeftPart(UriPartial.Authority);

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");

    private static string Level(CatalogLevel level) => level switch
    {
        CatalogLevel.A => "A 초급 · boshlang‘ich",
        CatalogLevel.B => "B 중급 · o‘rta",
        _ => "C 고급 · yuqori",
    };

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";

    /// <summary>Ends the text with one full stop: "odam" → "odam.", "선생님이에요." stays as it is.</summary>
    private static string Sentence(string text)
    {
        var trimmed = text.TrimEnd();
        return trimmed.Length > 0 && trimmed[^1] is '.' or '?' or '!' or '…' or '。' ? trimmed : trimmed + ".";
    }

    private static string WordPage(SeoWord w, string baseUrl)
    {
        var url = $"{baseUrl}/soz/{w.Slug}";
        var reading = string.IsNullOrWhiteSpace(w.Romanization) ? "" : $" ({w.Romanization})";
        var title = $"{w.Word}{reading} — o‘zbekcha tarjimasi va misol | Koreyscha";
        var description = Clip($"{w.Word}{reading} — {Sentence(w.Translation)} Koreyscha so‘zning o‘zbekcha tarjimasi"
            + (w.ExampleKo is null ? "." : $" va misol: {Sentence(w.ExampleKo)}") + " Koreyscha ilovasida bepul yodlang.", 160);
        var jsonLd = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "DefinedTerm",
            ["name"] = w.Word,
            ["alternateName"] = w.Romanization,
            ["description"] = w.Translation,
            ["inLanguage"] = "ko",
            ["url"] = url,
            ["inDefinedTermSet"] = new Dictionary<string, object?>
            {
                ["@type"] = "DefinedTermSet",
                ["name"] = "Koreyscha lug‘ati",
                ["url"] = $"{baseUrl}/soz",
            },
        }, JsonLd);

        var body = new StringBuilder();
        body.Append($"<nav class=\"crumbs\"><a href=\"/soz\">So‘zlar</a> › {E(Level(w.Level))}</nav>");
        body.Append("<article class=\"word\">");
        body.Append($"<h1 lang=\"ko\">{E(w.Word)}</h1>");
        if (!string.IsNullOrWhiteSpace(w.Romanization))
        {
            body.Append($"<p class=\"reading\">{E(w.Romanization)}</p>");
        }

        body.Append($"<p class=\"meta\">{E(w.PartOfSpeech)} · {E(Level(w.Level))}</p>");
        body.Append($"<p class=\"label\">O‘zbekcha</p><p class=\"translation\">{E(w.Translation)}</p>");
        if (!string.IsNullOrWhiteSpace(w.ExampleKo))
        {
            body.Append("<section class=\"example\"><p class=\"label\">Misol</p>");
            body.Append($"<p class=\"ko\" lang=\"ko\">{E(w.ExampleKo)}</p>");
            if (!string.IsNullOrWhiteSpace(w.ExampleUz))
            {
                body.Append($"<p>{E(w.ExampleUz)}</p>");
            }

            body.Append("</section>");
        }

        body.Append($"<a class=\"cta\" href=\"/bolimlar/{w.UnitId}?src=soz\">Ilovada yodlashni boshlash →</a>");
        body.Append($"<p class=\"note\">{E(w.Level.ToString())} darajaning {w.UnitNumber}-bo‘limi: 25 ta so‘z kartochka, mashq va test bilan. Telegram yoki Google orqali bepul kirasiz.</p>");
        body.Append("</article>");
        if (w.Related.Count > 0)
        {
            body.Append($"<section class=\"related\"><h2>Shu bo‘limdagi boshqa so‘zlar</h2><ul>");
            foreach (var r in w.Related)
            {
                body.Append($"<li><a href=\"/soz/{E(r.Slug)}\"><span lang=\"ko\">{E(r.Word)}</span> — {E(r.Translation)}</a></li>");
            }

            body.Append("</ul></section>");
        }

        return Page(title, description, url, body.ToString(), jsonLd);
    }

    private static string ListPage(SeoListPage list, string baseUrl)
    {
        var url = list.Page == 1 ? $"{baseUrl}/soz" : $"{baseUrl}/soz?sahifa={list.Page}";
        var title = list.Page == 1
            ? "Koreyscha so‘zlar o‘zbekcha tarjimasi bilan | Koreyscha"
            : $"Koreyscha so‘zlar o‘zbekcha tarjimasi bilan — {list.Page}-sahifa | Koreyscha";
        var description = $"Eng ko‘p ishlatiladigan {list.Total} ta koreyscha so‘z o‘zbekcha tarjimasi, o‘qilishi va misoli bilan. Darajalar bo‘yicha: A 초급, B 중급, C 고급.";
        var body = new StringBuilder();
        body.Append("<h1>Koreyscha so‘zlar</h1>");
        body.Append("<p class=\"intro\">Koreys tilining eng ko‘p ishlatiladigan so‘zlari o‘zbekcha tarjimasi bilan. So‘zni bosing — o‘qilishi, misol gap va shu bo‘limdagi boshqa so‘zlar. Yodlash uchun <a href=\"/\">Koreyscha ilovasini</a> oching.</p>");
        foreach (var group in list.Groups)
        {
            body.Append($"<section class=\"level\"><h2>{E(Level(group.Level))}</h2><ul class=\"words\">");
            foreach (var w in group.Words)
            {
                body.Append($"<li><a href=\"/soz/{E(w.Slug)}\"><span lang=\"ko\">{E(w.Word)}</span> <span class=\"tr\">{E(w.Translation)}</span></a></li>");
            }

            body.Append("</ul></section>");
        }

        if (list.Pages > 1)
        {
            body.Append("<nav class=\"pages\" aria-label=\"Sahifalar\">");
            for (var p = 1; p <= list.Pages; p++)
            {
                body.Append(p == list.Page
                    ? $"<span aria-current=\"page\">{p}</span>"
                    : $"<a href=\"{(p == 1 ? "/soz" : $"/soz?sahifa={p}")}\">{p}</a>");
            }

            body.Append("</nav>");
        }

        return Page(title, description, url, body.ToString(), null);
    }

    private static IResult NotFound(HttpContext http)
    {
        http.Response.Headers.CacheControl = "public, max-age=300";
        var body = "<h1>So‘z topilmadi</h1><p class=\"intro\">Bu manzilda so‘z yo‘q yoki u hali nashr qilinmagan.</p><a class=\"cta\" href=\"/soz\">Barcha so‘zlar →</a>";
        return Results.Content(Page("So‘z topilmadi | Koreyscha", "So‘z topilmadi.", null, body, null, noIndex: true),
            "text/html; charset=utf-8", statusCode: StatusCodes.Status404NotFound);
    }

    private static string Page(string title, string description, string? canonical, string body, string? jsonLd, bool noIndex = false)
    {
        var head = new StringBuilder();
        head.Append($"<title>{E(title)}</title>");
        head.Append($"<meta name=\"description\" content=\"{E(description)}\">");
        if (noIndex)
        {
            head.Append("<meta name=\"robots\" content=\"noindex\">");
        }

        if (canonical is not null)
        {
            head.Append($"<link rel=\"canonical\" href=\"{E(canonical)}\">");
            head.Append("<meta property=\"og:type\" content=\"article\"><meta property=\"og:site_name\" content=\"Koreyscha\"><meta property=\"og:locale\" content=\"uz_UZ\">");
            head.Append($"<meta property=\"og:title\" content=\"{E(title)}\"><meta property=\"og:description\" content=\"{E(description)}\"><meta property=\"og:url\" content=\"{E(canonical)}\">");
            head.Append($"<meta property=\"og:image\" content=\"{E(Origin(canonical))}/icons/icon-512.png\"><meta name=\"twitter:card\" content=\"summary\">");
        }

        if (jsonLd is not null)
        {
            head.Append($"<script type=\"application/ld+json\">{jsonLd}</script>");
        }

        return $$"""
            <!doctype html>
            <html lang="uz">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="theme-color" content="#f6f2ed" media="(prefers-color-scheme: light)"><meta name="theme-color" content="#0f1117" media="(prefers-color-scheme: dark)">
            <link rel="icon" href="/favicon.ico?v=2" sizes="any"><link rel="icon" href="/icons/favicon.svg?v=2" type="image/svg+xml">
            {{head}}
            <style>{{Css}}</style>
            </head>
            <body>
            <header class="top"><a class="brand" href="/?src=soz"><span class="mark" lang="ko" aria-hidden="true">ㅋ</span>Koreyscha</a><a href="/soz">Barcha so‘zlar</a></header>
            <main>{{body}}</main>
            <footer>So‘zlar ro‘yxati: <span lang="ko">국립국어원 「한국어 학습용 어휘 목록」</span> (KOGL 1-tur). Tarjima va misollar muharrir tomonidan tekshirilgan.</footer>
            </body>
            </html>
            """;
    }

    // The app's look (L-57): "Sokin" warm paper by day, "Tungi" deep ink by night, soft cards and the red accent.
    // System fonts, so the page opens at once from a search result.
    private const string Css = """
        :root{--bg:#f6f2ed;--card:#fffdfa;--surface:#efe8df;--ink:#2b2420;--muted:#776a5f;--rule:rgba(43,36,32,.09);--accent:#e5533d;--accent-ink:#b8402c;--accent-soft:#fbeee8;--on-accent:#fff;--fill:#e5533d;--shadow:0 1px 2px rgba(80,50,30,.05),0 10px 26px -14px rgba(80,50,30,.2);--glow:0 8px 18px -8px rgba(229,83,61,.55);--page:none;color-scheme:light}
        @media (prefers-color-scheme:dark){:root{--bg:#0f1117;--card:#161922;--surface:#1b1e27;--ink:#eef0f6;--muted:#8f94a7;--rule:rgba(255,255,255,.07);--accent:#ff5a3c;--accent-ink:#ff8a70;--accent-soft:#2a1b1d;--on-accent:#fff;--fill:linear-gradient(135deg,#ff6a45,#ec3013);--shadow:inset 0 0 0 1px rgba(255,255,255,.06);--glow:0 10px 28px -10px rgba(255,90,60,.7);--page:radial-gradient(120% 50% at 100% 0%,rgba(255,90,60,.13),transparent 60%);color-scheme:dark}}
        *{box-sizing:border-box}html{-webkit-text-size-adjust:100%}
        body{margin:0;background:var(--page),var(--bg);background-attachment:fixed;color:var(--ink);font:16px/1.55 system-ui,-apple-system,"Segoe UI",Roboto,"Apple SD Gothic Neo","Noto Sans KR",sans-serif}
        a{color:inherit}
        .top{display:flex;justify-content:space-between;align-items:center;gap:12px;max-width:720px;margin:0 auto;padding:14px max(16px,env(safe-area-inset-right)) 6px max(16px,env(safe-area-inset-left))}
        .top a{text-decoration:none;font-weight:600}.top a:hover{color:var(--accent-ink)}
        .brand{display:inline-flex;align-items:center;gap:9px;font-size:18px;font-weight:800!important;letter-spacing:-.01em}
        .mark{display:grid;place-items:center;width:30px;height:30px;border-radius:10px;background:var(--fill);color:var(--on-accent);font-size:17px;font-weight:700}
        main{max-width:720px;margin:0 auto;padding:10px max(16px,env(safe-area-inset-right)) 40px max(16px,env(safe-area-inset-left))}
        h1{font-size:clamp(40px,12vw,64px);line-height:1.05;margin:6px 0 4px;letter-spacing:-.02em;text-wrap:balance}
        h2{font-size:18px;margin:28px 4px 10px;letter-spacing:-.01em}
        [lang=ko]{word-break:keep-all}
        .crumbs{color:var(--muted);font-size:14px;margin:4px 4px 10px}.crumbs a{text-decoration:none}.crumbs a:hover{color:var(--accent-ink)}
        .word{background:var(--card);border-radius:22px;box-shadow:var(--shadow);padding:22px 20px}
        .meta,.note,.label{color:var(--muted);font-size:14px;margin:0}
        .label{text-transform:uppercase;letter-spacing:.09em;font-size:11px;font-weight:700;margin-top:18px}
        .reading{font-size:20px;margin:0;color:var(--muted)}
        .translation{font-size:26px;font-weight:700;margin:2px 0 0;letter-spacing:-.01em}
        .example{background:var(--surface);border-radius:14px;padding:12px 14px;margin:18px 0 0}
        .example .label{margin-top:0}.example p{margin:4px 0}.example .ko{font-size:20px}
        .cta{display:flex;justify-content:space-between;align-items:center;margin:22px 0 10px;padding:15px 18px;border-radius:14px;background:var(--fill);color:var(--on-accent)!important;text-decoration:none;font-weight:700;font-size:16px;box-shadow:var(--glow);transition:filter .18s,transform .12s}
        .cta:hover{filter:brightness(1.06)}.cta:active{transform:scale(.98)}
        .cta:focus-visible,a:focus-visible{outline:2px solid var(--accent);outline-offset:3px}
        .related ul,.words{list-style:none;padding:0;margin:0;background:var(--card);border-radius:22px;box-shadow:var(--shadow);overflow:hidden}
        .related li a,.words li a{display:block;padding:12px 18px;border-bottom:1px solid var(--rule);text-decoration:none;transition:background-color .15s}
        .related li:last-child a{border-bottom:0}
        .related li a:hover,.words li a:hover{background:var(--surface)}
        .words{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr))}
        .words .tr{color:var(--muted)}
        .intro{margin:6px 4px 4px;color:var(--muted)}.intro a{color:var(--accent-ink)}
        .pages{display:flex;flex-wrap:wrap;gap:8px;margin-top:24px}
        .pages a,.pages span{min-width:44px;min-height:44px;display:inline-grid;place-items:center;padding:0 14px;border-radius:999px;background:var(--card);box-shadow:var(--shadow);text-decoration:none;font-weight:600}
        .pages span{background:var(--ink);color:var(--bg)}
        footer{max-width:720px;margin:0 auto;padding:16px max(16px,env(safe-area-inset-right)) 32px max(16px,env(safe-area-inset-left));color:var(--muted);font-size:13px}
        @media (prefers-reduced-motion:reduce){.cta,.related li a,.words li a{transition:none}}
        """;

    private static string Sitemap(IReadOnlyList<SeoSitemapEntry> words, int pages, string baseUrl)
    {
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false };
        using var stream = new MemoryStream();
        using (var xml = XmlWriter.Create(stream, settings))
        {
            const string ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var latest = words.Count == 0 ? (DateTimeOffset?)null : words.Max(w => w.LastModified);
            xml.WriteStartDocument();
            xml.WriteStartElement("urlset", ns);
            void Url(string loc, DateTimeOffset? lastmod)
            {
                xml.WriteStartElement("url", ns);
                xml.WriteElementString("loc", ns, loc);
                if (lastmod is { } at)
                {
                    xml.WriteElementString("lastmod", ns, at.UtcDateTime.ToString("yyyy-MM-dd"));
                }

                xml.WriteEndElement();
            }

            Url($"{baseUrl}/", null);
            Url($"{baseUrl}/soz", latest);
            for (var p = 2; p <= pages; p++)
            {
                Url($"{baseUrl}/soz?sahifa={p}", latest);
            }

            foreach (var w in words)
            {
                Url($"{baseUrl}/soz/{w.Slug}", w.LastModified);
            }

            xml.WriteEndElement();
            xml.WriteEndDocument();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
