using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Api.Tests.Speech;
using Lugat.Application.Catalog;
using Lugat.Application.Seo;
using Lugat.Contracts;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Seo;

/// <summary>L-93: the public alphabet page /alifbo.</summary>
public class AlphabetPageTests
{
    [Fact]
    public async Task The_page_is_server_rendered_with_every_letter_and_is_in_the_sitemap()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/alifbo");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<title>Koreys alifbosi (Hangul) — o‘zbek tilida, ovozi bilan | Koreyscha</title>", html);
        Assert.Contains("<link rel=\"canonical\" href=\"https://koreyscha.com/alifbo\">", html);
        Assert.Contains("<meta property=\"og:image\" content=\"https://koreyscha.com/og-image.jpg\">", html);
        Assert.Contains("\"@type\":\"LearningResource\"", html);
        Assert.DoesNotContain("blazor.webassembly", html); // not the app shell
        foreach (var letter in HangulAlphabet.All)
        {
            Assert.Contains($"<p class=\"glyph\" lang=\"ko\">{letter.Letter}</p>", html);
            Assert.Contains(letter.Name, html);
        }

        Assert.Contains("ㅂ + ㅏ + ㅂ = <b>밥</b>", html);
        Assert.Contains("받침", html);
        Assert.Contains("href=\"/sinab?src=alifbo\"", html);
        Assert.DoesNotContain("class=\"say\"", html); // nothing recorded yet: no play buttons
        Assert.DoesNotContain("class=\"quiz-q\"", html); // no A-1 words: no questions

        var sitemap = await client.GetStringAsync("/sitemap.xml");
        Assert.Contains("<loc>https://koreyscha.com/alifbo</loc>", sitemap);
        Assert.Contains("<a href=\"/alifbo\">", await client.GetStringAsync("/sinab"));
    }

    [Fact]
    public async Task Five_A1_words_become_reading_questions_with_three_options_one_right()
    {
        await using var factory = new ApiFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await ApiFactory.PublishUnitAsync(scope.ServiceProvider, 1,
                [("사랑", "sevgi"), ("가다", "bormoq"), ("물", "suv"), ("학교", "maktab"), ("나", "men"), ("밥", "ovqat")]);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var words = await db.CatalogWords.OrderBy(w => w.OrderInUnit).ToListAsync();
            string[] readings = ["sarang", "gada", "mul", "hakgyo", "na", "bap"];
            for (var i = 0; i < words.Count; i++)
            {
                words[i].Romanization = readings[i];
            }

            await db.SaveChangesAsync();
        }

        var html = await factory.CreateClient().GetStringAsync("/alifbo");
        var questions = Regex.Matches(html, "<div class=\"quiz-q\"><p class=\"quiz-word\" lang=\"ko\">([^<]+)</p><div class=\"options\">(.*?)</div>");
        Assert.Equal(["사랑", "가다", "물", "학교", "나"], questions.Select(q => q.Groups[1].Value));
        var expected = new Dictionary<string, string> { ["사랑"] = "sarang", ["가다"] = "gada", ["물"] = "mul", ["학교"] = "hakgyo", ["나"] = "na" };
        foreach (Match q in questions)
        {
            var options = Regex.Matches(q.Groups[2].Value, "data-right=\"([01])\">([^<]+)</button>");
            Assert.Equal(3, options.Count);
            Assert.Equal(3, options.Select(o => o.Groups[2].Value).Distinct().Count());
            Assert.Equal(expected[q.Groups[1].Value], Assert.Single(options, o => o.Groups[1].Value == "1").Groups[2].Value);
        }

        Assert.Contains("<script type=\"module\" src=\"/js/alifbo.js\"></script>", html);
    }

    [Fact]
    public async Task The_admin_records_the_sounds_once_and_visitors_only_read_them()
    {
        var speech = new FakeSpeech();
        await using var factory = new ApiFactory { AdminTelegramIds = [ApiFactory.UserA], Speech = speech };
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);
        var learner = await factory.SignedInClientAsync(ApiFactory.UserB);
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.PostAsync("/api/admin/alifbo/audio", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/alifbo/ovoz/g-1.mp3")).StatusCode);

        var total = HangulAlphabet.Recordings.Count;
        AlphabetAudioDto? status = null;
        for (var i = 0; i < 20 && status?.Ready != total; i++)
        {
            status = await (await admin.PostAsync("/api/admin/alifbo/audio", null)).Content.ReadFromJsonAsync<AlphabetAudioDto>(ApiFactory.Json);
            Assert.True(status!.Ready <= Math.Min(total, (i + 1) * AlphabetAudioService.Batch));
        }

        Assert.Equal((total, total), (status!.Ready, status.Total));
        Assert.Empty(status.Failed);
        Assert.Contains("가", speech.Texts); // ㄱ by a syllable…
        Assert.Contains("기역", speech.Texts); // …and by its name
        Assert.DoesNotContain("ㄱ", speech.Texts); // never a lone jamo (L-62)

        // Again: nothing new is recorded.
        var recorded = speech.Texts.Count;
        await admin.PostAsync("/api/admin/alifbo/audio", null);
        Assert.Equal(recorded, speech.Texts.Count);

        var html = await anon.GetStringAsync("/alifbo");
        Assert.Contains("<button type=\"button\" class=\"say\" data-audio=\"/alifbo/ovoz/g-1.mp3\" hidden>", html);
        Assert.Contains("data-audio=\"/alifbo/ovoz/g-2.mp3\"", html);
        Assert.Contains("data-audio=\"/alifbo/ovoz/bogin-3.mp3\"", html);
        Assert.DoesNotContain("data-audio=\"/alifbo/ovoz/a-2.mp3\"", html); // a vowel's name is its syllable

        var audio = await anon.GetAsync("/alifbo/ovoz/g-1.mp3");
        Assert.Equal(HttpStatusCode.OK, audio.StatusCode);
        Assert.Equal("audio/mpeg", audio.Content.Headers.ContentType!.MediaType);
        Assert.Contains("max-age=2592000", audio.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/alifbo/ovoz/nope.mp3")).StatusCode);
        Assert.Equal(recorded, speech.Texts.Count); // the visit cost nothing
    }

    [Fact]
    public void The_alphabet_table_agrees_with_the_letter_names_the_introduction_voice_uses()
    {
        foreach (var letter in HangulAlphabet.Consonants.Concat(HangulAlphabet.DoubleConsonants))
        {
            Assert.Equal(letter.Name, HangulAlphabet.SpokenName(letter.Letter[0]));
        }

        foreach (var letter in HangulAlphabet.Vowels.Concat(HangulAlphabet.ComplexVowels))
        {
            Assert.Equal(letter.Name, HangulAlphabet.SpokenName(letter.Letter[0]));
            Assert.Equal(letter.Name, letter.Sample);
        }

        Assert.Equal("리을 밥", IntroductionAudioService.Speakable("-ㄹ 밥"));
        Assert.Equal(HangulAlphabet.Recordings.Count, HangulAlphabet.Recordings.Select(r => r.Key).Distinct().Count());
    }
}
