using System.Net;
using System.Text.Json;
using Lugat.Api.Tests.Catalog;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Domain.Catalog;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Seo;

/// <summary>The sign-in-free taster /sinab (L-58).</summary>
public class SinabPageTests
{
    private static string DataOf(string html)
    {
        const string open = "<script type=\"application/json\" id=\"taster-data\">";
        var at = html.IndexOf(open, StringComparison.Ordinal);
        Assert.True(at >= 0, "no taster data");
        var from = at + open.Length;
        return html[from..html.IndexOf("</script>", from, StringComparison.Ordinal)];
    }

    [Fact]
    public async Task The_published_A1_introduction_gives_two_words_whose_saved_audio_is_public_and_nothing_is_generated()
    {
        var narrator = new FakeNarrator();
        await using var f = IntroductionTests.Factory(narrator: narrator);
        var admin = await f.SignedInClientAsync(ApiFactory.UserA);
        await f.AddWordsAsync(ApiFactory.UserB, 1, ("가다", "bormoq"), ("하다", "qilmoq"), ("있다", "bor bo‘lmoq"));
        var anon = f.CreateClient();

        var row = await IntroductionTests.Approve(admin, await IntroductionTests.Fill(admin, await IntroductionTests.Create(admin)));
        row = await IntroductionTests.Audio(admin, row);
        // Recorded and approved, not yet published: nothing on the public page.
        var page = await anon.GetAsync("/sinab");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.DoesNotContain("taster-data", await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/sinab/audio/0/-1")).StatusCode);

        await IntroductionTests.Publish(admin, row);
        var generated = narrator.Texts.Count;
        var html = await anon.GetStringAsync("/sinab");
        using var data = JsonDocument.Parse(DataOf(html));
        var words = data.RootElement.GetProperty("words");
        Assert.Equal(2, words.GetArrayLength());
        Assert.Equal("가다", words[0].GetProperty("word").GetString());
        Assert.StartsWith("/api/sinab/audio/0/-1", words[0].GetProperty("koreanAudioUrl").GetString());
        Assert.DoesNotContain("/api/introductions/", html); // the learner's own routes are not exposed
        Assert.Contains("<script type=\"module\" src=\"/js/sinab.js\"></script>", html);

        var audio = await anon.GetAsync("/api/sinab/audio/0/-1");
        Assert.Equal(HttpStatusCode.OK, audio.StatusCode);
        Assert.Equal("audio/mpeg", audio.Content.Headers.ContentType!.MediaType);
        Assert.Contains("public", audio.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/api/sinab/audio/1/0/0")).StatusCode);
        // The third word, a missing step or part, and a negative index are not served.
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/sinab/audio/2/-1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/sinab/audio/0/9")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/sinab/audio/0/0/9")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/sinab/audio/-1/0")).StatusCode);
        Assert.Equal(generated, narrator.Texts.Count); // reading the taster never makes new audio
    }

    [Fact]
    public async Task Only_checked_complete_trap_scenes_of_unit_A1_become_questions_and_their_text_is_encoded()
    {
        await using var f = new ApiFactory();
        await using (var scope = f.Services.CreateAsyncScope())
        {
            await ApiFactory.PublishUnitAsync(scope.ServiceProvider, 1, [("말", "gap"), ("눈", "ko‘z"), ("밥", "ovqat"), ("손", "qo‘l"), ("물", "suv")]);
            await ApiFactory.PublishUnitAsync(scope.ServiceProvider, 2, [("집", "uy")]);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var words = await db.CatalogWords.OrderBy(w => w.Level).ThenBy(w => w.FreqRank).ToListAsync();
            void Trap(CatalogWord w, string right)
            {
                w.TrapUz = "O‘zbekcha <b>\"shunday\"</b> & boshqacha";
                w.TrapCue = "Do‘stingizga aytasiz";
                w.TrapWrong = "틀린 " + right;
                w.TrapRight = right;
            }

            Trap(words[0], "맞는 말");                    // shown
            Trap(words[1], "보류 눈"); words[1].TrapScenePending = true; // waits for review
            Trap(words[2], "빈 밥"); words[2].TrapRight = null;          // incomplete
            Trap(words[3], "초안 손"); words[3].Status = CatalogWordStatus.Draft;
            Trap(words[4], "맞는 물");                    // shown
            Trap(words[5], "다른 집");                    // another unit
            await db.SaveChangesAsync();
        }

        var html = await f.CreateClient().GetStringAsync("/sinab?src=ig");
        Assert.Contains("맞는 말", html);
        Assert.Contains("맞는 물", html);
        Assert.DoesNotContain("보류 눈", html);
        Assert.DoesNotContain("빈 밥", html);
        Assert.DoesNotContain("초안 손", html);
        Assert.DoesNotContain("다른 집", html);
        Assert.Contains("Qaysi biri to‘g‘ri?", html);
        Assert.Contains("data-right=\"1\">맞는 말</button>", html);
        Assert.Contains("data-right=\"0\">틀린 맞는 말</button>", html);
        Assert.Contains("O‘zbekcha &lt;b&gt;&quot;shunday&quot;&lt;/b&gt; &amp; boshqacha", html);
        Assert.DoesNotContain("<b>\"shunday\"</b>", html);
        Assert.Contains("data-total=\"2\"", html);
        // The source the app keeps: sinab-<src>, and the start link carries it.
        Assert.Contains("data-source=\"sinab-ig\" href=\"/?src=sinab-ig\"", html);
        Assert.Contains("data-source=\"sinab\" href=\"/?src=sinab\"", await f.CreateClient().GetStringAsync("/sinab?src=%3Cx%3E"));
        // The share card's link (L-59) is counted as its own channel.
        Assert.Contains("data-source=\"sinab-ulash\" href=\"/?src=sinab-ulash\"", await f.CreateClient().GetStringAsync("/sinab?src=ulash"));
        // The home page's static landing (L-60) and its sign-in screen link here as "bosh".
        Assert.Contains("data-source=\"sinab-bosh\" href=\"/?src=sinab-bosh\"", await f.CreateClient().GetStringAsync("/sinab?src=bosh"));
    }

    [Fact]
    public async Task Without_an_introduction_or_traps_the_page_is_a_short_text_and_the_start_link()
    {
        await using var f = new ApiFactory();
        var client = f.CreateClient();
        var response = await client.GetAsync("/sinab");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("public", response.Headers.CacheControl!.ToString());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("1-bo‘limni bepul boshlang", html);
        Assert.Contains("href=\"/?src=sinab\"", html);
        Assert.DoesNotContain("taster-data", html);
        Assert.DoesNotContain("id=\"traps\"", html);
        Assert.Contains("<link rel=\"canonical\" href=\"https://koreyscha.com/sinab\">", html);

        Assert.Contains("<loc>https://koreyscha.com/sinab</loc>", await client.GetStringAsync("/sitemap.xml"));
    }
}
