using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Testing;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Testing;

/// <summary>"Bu savolda xato bor" (L-37): learners report a wrong question, the admin looks at it and resolves it.</summary>
public sealed class QuestionReportService(IAppDbContext db, TimeProvider time)
{
    public const int MaxComment = 500;

    /// <summary>Once per learner and question: a second report of the same question changes nothing.</summary>
    public async Task ReportAsync(Guid userId, Guid runId, int index, QuestionReportRequest request, CancellationToken ct)
    {
        var run = await db.TestRuns.AsNoTracking().SingleOrDefaultAsync(r => r.Id == runId && r.OwnerId == userId, ct)
            ?? throw new NotFoundException("Test topilmadi.");
        var q = run.Questions.SingleOrDefault(x => x.Index == index) ?? throw new NotFoundException("Savol topilmadi.");
        var given = run.Answers.SingleOrDefault(a => a.Index == index)?.Given;
        if (run.Status != TestRunStatus.Finished && given is null)
        {
            throw new ValidationException("Avval savolga javob bering.");
        }

        if (await db.QuestionReports.AnyAsync(r => r.ReporterId == userId && r.TestRunId == runId && r.QuestionIndex == index, ct))
        {
            return;
        }

        var comment = request.Comment?.Trim();
        db.QuestionReports.Add(new QuestionReport
        {
            Id = Guid.NewGuid(),
            ReporterId = userId,
            TestRunId = runId,
            QuestionIndex = index,
            Kind = q.Kind,
            Prompt = q.Prompt,
            Sentence = q.Sentence,
            Translation = q.Translation,
            Options = q.Options.ToList(),
            Correct = q.Correct,
            Given = given,
            LearningItemId = q.LearningItemId,
            Comment = string.IsNullOrEmpty(comment) ? null : comment.Length > MaxComment ? comment[..MaxComment] : comment,
            CreatedAt = time.GetUtcNow(),
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same report sent twice at once: the first one is kept.
            db.DiscardChanges();
        }
    }

    public async Task<IReadOnlyList<QuestionReportDto>> OpenAsync(CancellationToken ct) =>
        await (from r in db.QuestionReports
               where r.ResolvedAt == null
               join u in db.Users on r.ReporterId equals u.Id
               orderby r.CreatedAt descending
               select new QuestionReportDto(r.Id, r.Kind, r.Prompt, r.Sentence, r.Translation, r.Options, r.Correct, r.Given, r.Comment,
                   u.DisplayName ?? "Foydalanuvchi", r.CreatedAt))
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task ResolveAsync(Guid adminId, Guid id, CancellationToken ct)
    {
        var report = await db.QuestionReports.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Xabar topilmadi.");
        if (report.ResolvedAt is null)
        {
            report.ResolvedAt = time.GetUtcNow();
            report.ResolvedById = adminId;
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Sentences of open reports: such a stored question is not asked again until the admin resolves it.</summary>
    public static async Task<HashSet<string>> ReportedSentencesAsync(IAppDbContext db, CancellationToken ct) =>
        (await db.QuestionReports.Where(r => r.ResolvedAt == null && r.Sentence != null).Select(r => r.Sentence!).Distinct().ToListAsync(ct))
            .ToHashSet();
}
