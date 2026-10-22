using KoreanAiTeacher.Application.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using KoreanAiTeacher.Domain.Ai;
using KoreanAiTeacher.Domain.Imports;
using KoreanAiTeacher.Domain.Learning;
using KoreanAiTeacher.Domain.Lessons;
using KoreanAiTeacher.Domain.Reminders;
using KoreanAiTeacher.Domain.Users;
using KoreanAiTeacher.Domain.Vocabulary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace KoreanAiTeacher.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public DbSet<User> Users => Set<User>();

    public DbSet<SourceDocument> SourceDocuments => Set<SourceDocument>();

    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

    public DbSet<SourceEntry> SourceEntries => Set<SourceEntry>();

    public DbSet<WordSense> WordSenses => Set<WordSense>();

    public DbSet<WordUsage> WordUsages => Set<WordUsage>();

    public DbSet<Domain.Ladder.SentenceLadder> SentenceLadders => Set<Domain.Ladder.SentenceLadder>();

    public DbSet<Domain.Ladder.TrapNote> TrapNotes => Set<Domain.Ladder.TrapNote>();

    public DbSet<Domain.Essays.EssayAttempt> EssayAttempts => Set<Domain.Essays.EssayAttempt>();

    public DbSet<Domain.Listening.ListeningSet> ListeningSets => Set<Domain.Listening.ListeningSet>();

    public DbSet<Card> Cards => Set<Card>();

    public DbSet<ReviewEvent> ReviewEvents => Set<ReviewEvent>();

    public DbSet<LessonAssignment> LessonAssignments => Set<LessonAssignment>();

    public DbSet<StudySession> StudySessions => Set<StudySession>();

    public DbSet<ReminderDelivery> ReminderDeliveries => Set<ReminderDelivery>();

    public DbSet<AiUsage> AiUsages => Set<AiUsage>();

    public DbSet<SentenceCheck> SentenceChecks => Set<SentenceCheck>();

    public DbSet<Domain.Grammar.GrammarImport> GrammarImports => Set<Domain.Grammar.GrammarImport>();

    public DbSet<Domain.Grammar.GrammarItem> GrammarItems => Set<Domain.Grammar.GrammarItem>();

    public DbSet<Domain.Grammar.GrammarNote> GrammarNotes => Set<Domain.Grammar.GrammarNote>();

    public DbSet<Domain.Grammar.GrammarSet> GrammarSets => Set<Domain.Grammar.GrammarSet>();

    public DbSet<Domain.Grammar.GrammarSetItem> GrammarSetItems => Set<Domain.Grammar.GrammarSetItem>();

    public DbSet<Domain.Testing.TestRun> TestRuns => Set<Domain.Testing.TestRun>();

    public DbSet<Domain.Testing.BookTest> BookTests => Set<Domain.Testing.BookTest>();

    public DbSet<Domain.Testing.BookQuestion> BookQuestions => Set<Domain.Testing.BookQuestion>();

    public DbSet<Domain.Writing.WritingAttempt> WritingAttempts => Set<Domain.Writing.WritingAttempt>();

    public DbSet<Domain.Operations.BackupRun> BackupRuns => Set<Domain.Operations.BackupRun>();

    public void DiscardChanges() => ChangeTracker.Clear();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken ct) =>
        Database.BeginTransactionAsync(ct);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as text so the database stays readable and reordering an enum is safe.
        configurationBuilder.Properties<ImportStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<EntryStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<SourceDocumentKind>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<ReviewDirection>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<LearningPhase>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<ReviewAnswer>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<AssignmentStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<StudySessionStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<ReminderSlot>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<DeliveryStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<AiProviderKind>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<AiCheckStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Domain.Writing.WritingVerdict>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Domain.Grammar.GrammarImportStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Domain.Grammar.GrammarItemStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Domain.Grammar.GrammarQuestionKind>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Domain.Testing.TestRunStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Domain.Testing.BookTestStatus>().HaveConversion<string>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasIndex(u => u.TelegramUserId).IsUnique();
            e.Property(u => u.FirstName).HasMaxLength(256);
            e.Property(u => u.Username).HasMaxLength(256);
            e.Property(u => u.TimeZone).HasMaxLength(64);
        });

        b.Entity<SourceDocument>(e =>
        {
            e.ToTable("source_documents");
            e.HasIndex(d => new { d.OwnerId, d.Kind, d.ContentSha256 }).IsUnique();
            e.Property(d => d.FileName).HasMaxLength(256);
            e.Property(d => d.ContentSha256).HasMaxLength(64);
            e.Property(d => d.StorageKey).HasMaxLength(512);
            e.HasOne<User>().WithMany().HasForeignKey(d => d.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ImportJob>(e =>
        {
            e.ToTable("import_jobs");
            e.HasIndex(j => j.Status);
            e.HasIndex(j => new { j.OwnerId, j.CreatedAt });
            e.Property(j => j.Error).HasMaxLength(1000);
            e.Property(j => j.ParserVersion).HasMaxLength(64);
            e.HasOne(j => j.SourceDocument).WithMany().HasForeignKey(j => j.SourceDocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(j => j.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(j => j.Entries).WithOne().HasForeignKey(x => x.ImportJobId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SourceEntry>(e =>
        {
            e.ToTable("source_entries");
            e.HasIndex(x => new { x.ImportJobId, x.TableNumber, x.OrderInTable }).IsUnique();
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.Warnings).HasConversion<int>();
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<SourceDocument>().WithMany().HasForeignKey(x => x.SourceDocumentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<WordUsage>(e =>
        {
            e.ToTable("word_usages");
            e.HasIndex(x => x.WordSenseId).IsUnique();
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.Phrases).HasColumnType("jsonb").HasConversion(JsonConverter<List<UsagePair>>(), ListComparer<UsagePair>());
            e.Property(x => x.Example).HasColumnType("jsonb").HasConversion(JsonConverter<UsagePair?>());
            e.Property(x => x.Questions).HasColumnType("jsonb").HasConversion(JsonConverter<List<UsageQuestion>>(), ListComparer<UsageQuestion>());
            e.Property(x => x.TrapUz).HasMaxLength(1000);
            e.Property(x => x.Provider).HasMaxLength(64);
            e.Property(x => x.Model).HasMaxLength(128);
            e.Property(x => x.Warning).HasMaxLength(500);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<WordSense>().WithMany().HasForeignKey(x => x.WordSenseId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Domain.Ladder.SentenceLadder>(e =>
        {
            e.ToTable("sentence_ladders");
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Provider).HasMaxLength(64);
            e.Property(x => x.Model).HasMaxLength(128);
            e.Property(x => x.Items).HasColumnType("jsonb").HasConversion(JsonConverter<List<Domain.Ladder.LadderItem>>(), ListComparer<Domain.Ladder.LadderItem>());
            e.Property(x => x.Answers).HasColumnType("jsonb").HasConversion(JsonConverter<List<Domain.Ladder.LadderAnswer>>(), ListComparer<Domain.Ladder.LadderAnswer>());
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Essays.EssayAttempt>(e =>
        {
            e.ToTable("essay_attempts");
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            e.Property(x => x.Task).HasConversion<string>().HasMaxLength(8);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(8);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Prompt).HasColumnType("jsonb").HasConversion(JsonConverter<Domain.Essays.EssayPrompt>());
            e.Property(x => x.Answers).HasColumnType("jsonb").HasConversion(JsonConverter<List<string>>(), ListComparer<string>());
            e.Property(x => x.Feedback).HasColumnType("jsonb").HasConversion(JsonConverter<Domain.Essays.EssayFeedback?>());
            e.Property(x => x.Provider).HasMaxLength(64);
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Listening.ListeningSet>(e =>
        {
            e.ToTable("listening_sets");
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Items).HasColumnType("jsonb").HasConversion(JsonConverter<List<Domain.Listening.ListeningItem>>(), ListComparer<Domain.Listening.ListeningItem>());
            e.Property(x => x.Answers).HasColumnType("jsonb").HasConversion(JsonConverter<List<Domain.Listening.ListeningAnswer>>(), ListComparer<Domain.Listening.ListeningAnswer>());
            e.Property(x => x.Provider).HasMaxLength(64);
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Ladder.TrapNote>(e =>
        {
            e.ToTable("trap_notes");
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            e.Property(x => x.Uzbek).HasMaxLength(500);
            e.Property(x => x.Wrong).HasMaxLength(200);
            e.Property(x => x.Right).HasMaxLength(200);
            e.Property(x => x.ExplanationUz).HasMaxLength(1000);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<WordSense>(e =>
        {
            e.ToTable("word_senses");
            e.HasIndex(x => x.SourceEntryId).IsUnique();
            e.HasIndex(x => new { x.OwnerId, x.SourceDocumentId, x.TableNumber });
            e.Property(x => x.Korean).HasMaxLength(200);
            e.Property(x => x.KoreanNote).HasMaxLength(200);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SourceEntry>().WithMany().HasForeignKey(x => x.SourceEntryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Card>(e =>
        {
            e.ToTable("cards");
            e.HasIndex(x => new { x.WordSenseId, x.Direction }).IsUnique(); // user + unit + direction (spec DATA01)
            e.HasIndex(x => new { x.OwnerId, x.DueAt });
            e.Property(x => x.AlgorithmVersion).HasMaxLength(32);
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<WordSense>().WithMany().HasForeignKey(x => x.WordSenseId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ReviewEvent>(e =>
        {
            e.ToTable("review_events");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.OwnerId, x.OccurredAt });
            e.HasIndex(x => x.SessionId);
            e.Property(x => x.AlgorithmVersion).HasMaxLength(32);
            e.Property(x => x.PreviousState).HasColumnType("jsonb").HasConversion(JsonConverter<LearningState>());
            e.Property(x => x.NextState).HasColumnType("jsonb").HasConversion(JsonConverter<LearningState>());
            e.HasOne<Card>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<LessonAssignment>(e =>
        {
            e.ToTable("lesson_assignments");
            e.HasIndex(x => new { x.OwnerId, x.Status, x.DueAt });
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SourceDocument>().WithMany().HasForeignKey(x => x.SourceDocumentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StudySession>(e =>
        {
            e.ToTable("study_sessions");
            e.HasIndex(x => new { x.OwnerId, x.Status });
            e.Property(x => x.Version).IsRowVersion();
            e.Property(x => x.Items).HasColumnType("jsonb").HasConversion(
                JsonConverter<List<SessionItem>>(),
                new ValueComparer<List<SessionItem>>(
                    (a, c) => a!.SequenceEqual(c!),
                    v => v.Aggregate(0, (h, i) => HashCode.Combine(h, i.GetHashCode())),
                    v => v.ToList()));
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ReminderDelivery>(e =>
        {
            e.ToTable("reminder_deliveries");
            e.HasIndex(x => x.OccurrenceKey).IsUnique(); // at most once per occurrence (spec DATA01, AC10)
            e.HasIndex(x => new { x.UserId, x.ScheduledAt });
            e.Property(x => x.OccurrenceKey).HasMaxLength(100);
            e.Property(x => x.Error).HasMaxLength(500);
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AiUsage>(e =>
        {
            e.ToTable("ai_usage");
            e.Property(x => x.Unit).HasMaxLength(16);
            e.HasIndex(x => new { x.OwnerId, x.OccurredAt });
            e.Property(x => x.Feature).HasMaxLength(64);
            e.Property(x => x.Model).HasMaxLength(128);
            e.Property(x => x.Error).HasMaxLength(500);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Grammar.GrammarImport>(e =>
        {
            e.ToTable("grammar_imports");
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.Property(x => x.Error).HasMaxLength(1000);
            e.HasOne(x => x.SourceDocument).WithMany().HasForeignKey(x => x.SourceDocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Grammar.GrammarItem>(e =>
        {
            e.ToTable("grammar_items");
            e.HasIndex(x => new { x.GrammarImportId, x.PdfPage });
            e.HasIndex(x => new { x.OwnerId, x.Status });
            e.Property(x => x.Version).IsRowVersion();
            e.Property(x => x.Section).HasMaxLength(100);
            e.Property(x => x.Form).HasMaxLength(100);
            e.Property(x => x.Similar).HasMaxLength(300);
            e.Property(x => x.MeaningKo).HasMaxLength(200);
            e.Property(x => x.MeaningUz).HasMaxLength(1000);
            e.Property(x => x.ConnectionUz).HasMaxLength(500);
            e.Property(x => x.RestrictionUz).HasMaxLength(500);
            e.Property(x => x.Warning).HasMaxLength(500);
            e.Property(x => x.Examples).HasColumnType("jsonb").HasConversion(
                JsonConverter<List<Domain.Grammar.GrammarExample>>(),
                new ValueComparer<List<Domain.Grammar.GrammarExample>>(
                    (a, c) => a!.SequenceEqual(c!),
                    v => v.Aggregate(0, (h, i) => HashCode.Combine(h, i.GetHashCode())),
                    v => v.ToList()));
            e.Property(x => x.Guide).HasColumnType("jsonb").HasConversion(JsonConverter<Domain.Grammar.GrammarGuide?>());
            e.HasOne<Domain.Grammar.GrammarImport>().WithMany().HasForeignKey(x => x.GrammarImportId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Grammar.GrammarSet>(e =>
        {
            e.ToTable("grammar_sets");
            e.HasIndex(x => new { x.OwnerId, x.Number }).IsUnique();
            e.Property(x => x.Version).IsRowVersion();
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.GrammarSetId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Grammar.GrammarSetItem>(e =>
        {
            e.ToTable("grammar_set_items");
            e.HasKey(x => new { x.GrammarSetId, x.GrammarItemId });
            e.HasIndex(x => new { x.OwnerId, x.GrammarItemId }).IsUnique(); // one set per grammar point
            e.HasOne<Domain.Grammar.GrammarItem>().WithMany().HasForeignKey(x => x.GrammarItemId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Testing.TestRun>(e =>
        {
            e.ToTable("test_runs");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            e.Property(x => x.Version).IsRowVersion();
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Warning).HasMaxLength(500);
            e.Property(x => x.Provider).HasMaxLength(64);
            e.Property(x => x.Questions).HasColumnType("jsonb").HasConversion(
                JsonConverter<List<Domain.Testing.TestQuestion>>(),
                new ValueComparer<List<Domain.Testing.TestQuestion>>((a, c) => a!.SequenceEqual(c!), v => v.Count, v => v.ToList()));
            e.Property(x => x.Answers).HasColumnType("jsonb").HasConversion(
                JsonConverter<List<Domain.Testing.TestAnswer>>(),
                new ValueComparer<List<Domain.Testing.TestAnswer>>((a, c) => a!.SequenceEqual(c!), v => v.Count, v => v.ToList()));
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Testing.BookTest>(e =>
        {
            e.ToTable("book_tests");
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            e.HasIndex(x => x.Status);
            e.Property(x => x.Version).IsRowVersion();
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Error).HasMaxLength(500);
            e.HasOne(x => x.SourceDocument).WithMany().HasForeignKey(x => x.SourceDocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Testing.BookQuestion>(e =>
        {
            e.ToTable("book_questions");
            e.HasIndex(x => new { x.BookTestId, x.Number });
            e.Property(x => x.Version).IsRowVersion();
            e.Property(x => x.Instruction).HasMaxLength(1000);
            e.Property(x => x.Passage).HasMaxLength(4000);
            e.Property(x => x.Stem).HasMaxLength(1000);
            e.Property(x => x.Figure).HasMaxLength(2000);
            e.Property(x => x.HintUz).HasMaxLength(600);
            e.Property(x => x.ExplanationUz).HasMaxLength(1500);
            e.Property(x => x.Warning).HasMaxLength(500);
            e.Property(x => x.Options).HasColumnType("jsonb").HasConversion(
                JsonConverter<List<string>>(),
                new ValueComparer<List<string>>((a, c) => a!.SequenceEqual(c!), v => v.Aggregate(0, (h, i) => HashCode.Combine(h, i.GetHashCode())), v => v.ToList()));
            e.Ignore(x => x.Answer);
            e.HasOne<Domain.Testing.BookTest>().WithMany().HasForeignKey(x => x.BookTestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Grammar.GrammarNote>(e =>
        {
            e.ToTable("grammar_notes");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.GrammarItemId, x.CreatedAt });
            e.Property(x => x.Question).HasMaxLength(1000);
            e.Property(x => x.AnswerUz).HasMaxLength(4000);
            e.Property(x => x.Provider).HasMaxLength(64);
            e.Property(x => x.Model).HasMaxLength(128);
            e.Property(x => x.PromptVersion).HasMaxLength(64);
            e.Property(x => x.Examples).HasColumnType("jsonb").HasConversion(
                JsonConverter<List<Domain.Grammar.GrammarExample>>(),
                new ValueComparer<List<Domain.Grammar.GrammarExample>>(
                    (a, c) => a!.SequenceEqual(c!),
                    v => v.Aggregate(0, (h, i) => HashCode.Combine(h, i.GetHashCode())),
                    v => v.ToList()));
            e.HasOne<Domain.Grammar.GrammarItem>().WithMany().HasForeignKey(x => x.GrammarItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Writing.WritingAttempt>(e =>
        {
            e.ToTable("writing_attempts");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            e.Property(x => x.Answer).HasMaxLength(200);
            e.Property(x => x.Expected).HasMaxLength(500);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<WordSense>().WithMany().HasForeignKey(x => x.WordSenseId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Operations.BackupRun>(e =>
        {
            e.ToTable("backup_runs");
            e.HasIndex(x => x.StartedAt);
            e.Property(x => x.FileName).HasMaxLength(128);
            e.Property(x => x.RestoredCounts).HasColumnType("jsonb");
            e.Property(x => x.Error).HasMaxLength(1000);
            e.Property(x => x.TelegramError).HasMaxLength(500);
        });

        b.Entity<SentenceCheck>(e =>
        {
            e.ToTable("sentence_checks");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            e.Property(x => x.Target).HasMaxLength(200);
            e.Property(x => x.TargetMeaning).HasMaxLength(500);
            e.Property(x => x.Answer).HasMaxLength(1000);
            e.Property(x => x.ResultJson).HasColumnType("jsonb");
            e.Property(x => x.Model).HasMaxLength(128);
            e.Property(x => x.PromptVersion).HasMaxLength(64);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<WordSense>().WithMany().HasForeignKey(x => x.WordSenseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Domain.Grammar.GrammarItem>().WithMany().HasForeignKey(x => x.GrammarItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>Compares JSON lists by their serialized text, so nested lists inside records are compared too.</summary>
    private static ValueComparer<List<T>> ListComparer<T>() => new(
        (a, c) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(c, Json),
        v => JsonSerializer.Serialize(v, Json).GetHashCode(),
        v => JsonSerializer.Deserialize<List<T>>(JsonSerializer.Serialize(v, Json), Json)!);

    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T, string> JsonConverter<T>() =>
        new(v => JsonSerializer.Serialize(v, Json), v => JsonSerializer.Deserialize<T>(v, Json)!);
}

