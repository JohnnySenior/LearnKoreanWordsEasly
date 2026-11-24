using System.Text;
using System.Text.Json;
using Lugat.Application.Seo;

namespace Lugat.Api.Seo;

/// <summary>
/// L-93: the public page /alifbo — the Korean alphabet in Uzbek, with sound. Server-rendered like /soz and /sinab, so it
/// is read whole without scripts; alifbo.js only plays the recorded sounds and checks the "O‘qib ko‘ring" answers.
/// </summary>
public static partial class SeoEndpoints
{
    /// <summary>Recordings change only when the admin records them anew.</summary>
    private const string AlphabetAudioCacheControl = "public, max-age=2592000";

    private static void MapAlphabet(IEndpointRouteBuilder app, string baseUrl)
    {
        app.MapGet("/alifbo", async (HttpContext http, SeoService seo, AlphabetAudioService audio, CancellationToken ct) =>
        {
            var ready = await audio.ReadyAsync(ct);
            var questions = await seo.ReadingQuestionsAsync(ct);
            http.Response.Headers.CacheControl = TasterCacheControl;
            return Results.Content(AlphabetPage(ready, questions, baseUrl), "text/html; charset=utf-8");
        }).AllowAnonymous();

        app.MapGet("/alifbo/ovoz/{key}.mp3", async (string key, HttpContext http, AlphabetAudioService audio, CancellationToken ct) =>
        {
            var stream = await audio.OpenAsync(key, ct);
            http.Response.Headers.CacheControl = AlphabetAudioCacheControl;
            return Results.File(stream, "audio/mpeg", enableRangeProcessing: true);
        }).AllowAnonymous().RequireRateLimiting(TasterRateLimitPolicy);
    }

    private static string AlphabetJsonLd(string url, string baseUrl, string description) => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "LearningResource",
        ["name"] = "Koreys alifbosi (Hangul)",
        ["description"] = description,
        ["url"] = url,
        ["inLanguage"] = "uz",
        ["teaches"] = "Koreys alifbosi — Hangul (한글)",
        ["educationalLevel"] = "Beginner",
        ["learningResourceType"] = "Interactive resource",
        ["isAccessibleForFree"] = true,
        ["provider"] = new Dictionary<string, object?> { ["@type"] = "Organization", ["name"] = "Koreyscha", ["url"] = baseUrl },
    }, JsonLd);

    private static string AlphabetPage(IReadOnlySet<string> ready, IReadOnlyList<SeoReadingQuestion> questions, string baseUrl)
    {
        const string title = "Koreys alifbosi (Hangul) — o‘zbek tilida, ovozi bilan | Koreyscha";
        const string description = "Koreys harflari (Hangul) o‘zbekcha izoh va ovoz bilan: 14 undosh, 10 unli, qo‘sh undosh va murakkab unlilar, bo‘g‘in qanday yig‘iladi. Oxirida o‘qib ko‘ring.";
        var url = $"{baseUrl}/alifbo";
        var body = new StringBuilder();
        body.Append("<h1 class=\"taster-title\">Koreys alifbosi <span lang=\"ko\">(한글)</span></h1>");
        body.Append("<p class=\"intro\">Koreys yozuvi Hangul deyiladi. Undosh va unlilar bo‘g‘inga yig‘ilib yoziladi: harflarni bilsangiz, so‘zni o‘qiy olasiz. Har harf yonida nomi, lotincha o‘qilishi va o‘zbekcha izohi bor.</p>");
        body.Append("<nav class=\"toc\" aria-label=\"Bo‘limlar\"><a href=\"#undosh\">Undoshlar</a><a href=\"#unli\">Unlilar</a><a href=\"#qosh\">Qo‘sh undosh</a><a href=\"#murakkab\">Murakkab unlilar</a><a href=\"#bogin\">Bo‘g‘in</a><a href=\"#oqing\">O‘qib ko‘ring</a></nav>");
        Letters(body, "undosh", "14 asosiy undosh", "Undosh bo‘g‘inda yolg‘iz kelmaydi: tugmadagi bo‘g‘in harfni ㅏ bilan aytadi.", HangulAlphabet.Consonants, ready);
        Letters(body, "unli", "10 asosiy unli", "Unli bo‘g‘in boshida bo‘lsa, oldiga o‘qilmaydigan ㅇ yoziladi: ㅏ → 아.", HangulAlphabet.Vowels, ready);
        Letters(body, "qosh", "Qo‘sh undoshlar", "Ikki marta yozilgan undosh tarang, havosiz aytiladi.", HangulAlphabet.DoubleConsonants, ready);
        Letters(body, "murakkab", "Murakkab unlilar", "Ikki unli qo‘shilib bitta tovush beradi.", HangulAlphabet.ComplexVowels, ready);

        body.Append("<section id=\"bogin\"><h2>Bo‘g‘in qanday yig‘iladi</h2><div class=\"syllables\">");
        for (var i = 0; i < HangulAlphabet.Syllables.Count; i++)
        {
            var s = HangulAlphabet.Syllables[i];
            body.Append("<div class=\"syllable\">");
            body.Append($"<p class=\"sum\" lang=\"ko\">{string.Join(" + ", s.Parts.Select(E))} = <b>{E(s.Syllable)}</b></p>");
            body.Append($"<p class=\"reading\">{E(s.Reading)}</p><p>{E(s.NoteUz)}</p>");
            Play(body, ready, $"bogin-{i + 1}", s.Syllable);
            body.Append("</div>");
        }

        body.Append($"</div><p class=\"final-note\">{E(HangulAlphabet.FinalNoteUz)}</p></section>");

        body.Append("<section id=\"oqing\"><h2>O‘qib ko‘ring</h2>");
        if (questions.Count > 0)
        {
            body.Append("<p class=\"intro\">Koreyscha so‘zni o‘qing va lotincha yozilishini tanlang. Bular ilovadagi 1-bo‘limning so‘zlari.</p>");
            foreach (var q in questions)
            {
                body.Append("<div class=\"quiz-q\">");
                body.Append($"<p class=\"quiz-word\" lang=\"ko\">{E(q.Word)}</p><div class=\"options\">");
                for (var o = 0; o < q.Options.Count; o++)
                {
                    body.Append($"<button type=\"button\" class=\"opt\" data-right=\"{(o == q.Right ? "1" : "0")}\">{E(q.Options[o])}</button>");
                }

                body.Append("</div><p class=\"verdict\" aria-live=\"polite\" hidden></p></div>");
            }

            body.Append($"<p class=\"score\" id=\"score\" data-total=\"{questions.Count}\" aria-live=\"polite\" hidden></p>");
        }
        else
        {
            body.Append("<p class=\"intro\">Harflarni o‘rgangach, ilovadagi 1-bo‘lim so‘zlarida o‘qib ko‘ring.</p>");
        }

        body.Append("</section>");
        body.Append("<a class=\"cta\" href=\"/sinab?src=alifbo\"><span>Koreys so‘zlarini 1 daqiqada sinab ko‘rish</span><span aria-hidden=\"true\">→</span></a>");
        body.Append("<p class=\"note\">Ro‘yxatdan o‘tish shart emas. Ilovada har bir so‘z ovozi, lotincha o‘qilishi va o‘zbekcha izohi bilan.</p>");
        return Page(title, description, url, body.ToString(), AlphabetJsonLd(url, baseUrl, description), script: "/js/alifbo.js", brandSource: "alifbo");
    }

    private static void Letters(StringBuilder body, string id, string heading, string lead, IReadOnlyList<HangulLetter> letters, IReadOnlySet<string> ready)
    {
        body.Append($"<section id=\"{id}\"><h2>{E(heading)}</h2><p class=\"intro\">{E(lead)}</p><ul class=\"letters\">");
        foreach (var l in letters)
        {
            body.Append("<li class=\"letter\">");
            body.Append($"<p class=\"glyph\" lang=\"ko\">{E(l.Letter)}</p>");
            body.Append($"<p class=\"name\"><span lang=\"ko\">{E(l.Name)}</span> · <b>{E(l.Reading)}</b></p>");
            body.Append($"<p class=\"sound\">{E(l.NoteUz)}</p>");
            body.Append("<div class=\"plays\">");
            Play(body, ready, l.Key + "-1", l.Sample);
            if (l.Name != l.Sample)
            {
                Play(body, ready, l.Key + "-2", l.Name);
            }

            body.Append("</div></li>");
        }

        body.Append("</ul></section>");
    }

    /// <summary>A play button only for a recorded sound; hidden until alifbo.js runs, so it is never a dead button.</summary>
    private static void Play(StringBuilder body, IReadOnlySet<string> ready, string key, string text)
    {
        if (ready.Contains(key))
        {
            body.Append($"<button type=\"button\" class=\"say\" data-audio=\"/alifbo/ovoz/{E(key)}.mp3\" hidden><span aria-hidden=\"true\">▶</span> <span lang=\"ko\">{E(text)}</span><span class=\"sr\"> — tinglash</span></button>");
        }
    }
}
