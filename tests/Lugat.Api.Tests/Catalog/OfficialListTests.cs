using System.Net.Http.Json;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Catalog;

/// <summary>Stage 2 acceptance (spec §13.3) with the real file of 국립국어원 「한국어 학습용 어휘 목록」 (content/official).</summary>
public class OfficialListTests
{
    private static byte[] OfficialFile()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "Lugat.slnx")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        return File.ReadAllBytes(Path.Combine(dir, "content", "official", "eohwi-mokrok-2003.txt"));
    }

    [Fact]
    public async Task The_official_list_imports_every_word_once_and_level_A_splits_by_frequency()
    {
        await using var factory = new ApiFactory { AdminTelegramIds = [ApiFactory.UserA] };
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);
        var file = OfficialFile();

        var first = (await (await admin.PostAsync("/api/admin/catalog/import", CatalogFiles.Upload(file, "한국어 학습용 어휘 목록.txt")))
            .Content.ReadFromJsonAsync<CatalogImportResultDto>(ApiFactory.Json))!;
        var again = (await (await admin.PostAsync("/api/admin/catalog/import", CatalogFiles.Upload(file, "list.txt")))
            .Content.ReadFromJsonAsync<CatalogImportResultDto>(ApiFactory.Json))!;
        var a = (await (await admin.PostAsync("/api/admin/catalog/levels/A/split", null)).Content.ReadFromJsonAsync<CatalogLevelAdminDto>(ApiFactory.Json))!;

        Assert.Equal((5965, 5965, 0), (first.Rows, first.Created, first.Skipped)); // 68 words have no rank: kept (L-27)
        Assert.Empty(first.Problems);
        Assert.Equal([982, 2111, 2872], first.Levels.Select(l => l.Words)); // spec §2.1
        Assert.Equal((0, 5965), (again.Created, again.Unchanged));
        Assert.Equal(0, a.Unplaced);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var placed = await db.CatalogWords.Where(w => w.UnitId != null).Join(db.Units, w => w.UnitId, u => u.Id, (w, u) => new { u.Number, w.OrderInUnit, w.FreqRank, w.Pos })
            .OrderBy(x => x.Number).ThenBy(x => x.OrderInUnit).ToListAsync();
        Assert.Equal(a.Studyable, placed.Count);
        var ranks = placed.Select(p => p.FreqRank ?? int.MaxValue).ToList();
        Assert.Equal(ranks.Order(), ranks); // most frequent first, words without a rank at the end
        Assert.Contains(placed, p => p.FreqRank is null && p.Pos == "고"); // e.g. 서울, 경주
        Assert.InRange(placed.GroupBy(p => p.Number).Count(), 30, 40); // spec §2.2: about 33 units
        Assert.DoesNotContain(placed, p => CatalogWord.StudyablePos(p.Pos) == false);
        Assert.All(placed.GroupBy(p => p.Number).SkipLast(1), g => Assert.Equal(25, g.Count()));
        // Homonyms written the same (spec §2.2: 가다01 동 and 가다01 보) are separate words; only the verb is studied.
        var gada = await db.CatalogWords.Where(w => w.Word == "가다" && w.Homonym == "01" && w.Level == CatalogLevel.A).OrderBy(w => w.Pos).ToListAsync();
        Assert.Equal([("동", true), ("보", false)], gada.Select(w => (w.Pos, w.IsStudyable)));
    }
}
