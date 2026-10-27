using Lugat.Application.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Domain.Ai;
using Lugat.Domain.Learning;
using Lugat.Domain.Study;
using Lugat.Domain.Reminders;
using Lugat.Domain.Users;
using Lugat.Domain.Vocabulary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Lugat.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public DbSet<User> Users => Set<User>();

    public DbSet<UserLogin> UserLogins => Set<UserLogin>();

    public DbSet<Domain.Premium.Subscription> Subscriptions => Set<Domain.Premium.Subscription>();

    public DbSet<Domain.Premium.Payment> Payments => Set<Domain.Premium.Payment>();

    public DbSet<Domain.Premium.PromoCode> PromoCodes => Set<Domain.Premium.PromoCode>();

    public DbSet<UserActivityDay> ActivityDays => Set<UserActivityDay>();

    public DbSet<SourceVisit> SourceVisits => Set<SourceVisit>();

    public DbSet<Domain.Teaching.Group> Groups => Set<Domain.Teaching.Group>();

    public DbSet<Domain.Teaching.GroupMember> GroupMembers => Set<Domain.Teaching.GroupMember>();

    public DbSet<Domain.Teaching.CommissionPayout> CommissionPayouts => Set<Domain.Teaching.CommissionPayout>();

    public DbSet<Domain.Operations.AdminAuditEntry> AdminAudit => Set<Domain.Operations.AdminAuditEntry>();

    public DbSet<Domain.Catalog.CatalogWord> CatalogWords => Set<Domain.Catalog.CatalogWord>();

    public DbSet<Domain.Catalog.CatalogUsage> CatalogUsages => Set<Domain.Catalog.CatalogUsage>();

    public DbSet<Domain.Catalog.Unit> Units => Set<Domain.Catalog.Unit>();

    public DbSet<LearningItem> LearningItems => Set<LearningItem>();

    public DbSet<Domain.Catalog.UserUnitProgress> UnitProgress => Set<Domain.Catalog.UserUnitProgress>();

    public DbSet<WordUsage> WordUsages => Set<WordUsage>();

    public DbSet<Domain.Ladder.SentenceLadder> SentenceLadders => Set<Domain.Ladder.SentenceLadder>();

    public DbSet<Domain.Ladder.TrapNote> TrapNotes => Set<Domain.Ladder.TrapNote>();

    public DbSet<Domain.Listening.ListeningSet> ListeningSets => Set<Domain.Listening.ListeningSet>();

    public DbSet<Card> Cards => Set<Card>();

    public DbSet<ReviewEvent> ReviewEvents => Set<ReviewEvent>();

    public DbSet<StudySession> StudySessions => Set<StudySession>();

    public DbSet<ReminderDelivery> ReminderDeliveries => Set<ReminderDelivery>();

    public DbSet<AiUsage> AiUsages => Set<AiUsage>();

    public DbSet<SentenceCheck> SentenceChecks => Set<SentenceCheck>();

    public DbSet<Domain.Testing.TestRun> TestRuns => Set<Domain.Testing.TestRun>();

    public DbSet<Domain.Testing.QuestionReport> QuestionReports => Set<Domain.Testing.QuestionReport>();

    public DbSet<Domain.Writing.WritingAttempt> WritingAttempts => Set<Domain.Writing.WritingAttempt>();

    public DbSet<Domain.Operations.BackupRun> BackupRuns => Set<Domain.Operations.BackupRun>();

    public void DiscardChanges() => ChangeTracker.Clear();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken ct) =>
        Database.BeginTransactionAsync(ct);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as text so the database stays readable and reordering an enum is safe.
        configurationBuilder.Properties<LoginProvider>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ThemeChoice>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<Domain.Catalog.CatalogLevel>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<Domain.Catalog.CatalogWordStatus>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<Domain.Catalog.UnitStatus>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<Domain.Catalog.UnitProgressStatus>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<Domain.Catalog.UnitPassedBy>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<Domain.Premium.SubscriptionPlan>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<Domain.Premium.SubscriptionSource>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<Domain.Premium.PaymentMethod>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<Domain.Premium.PaymentStatus>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<Domain.Premium.Currency>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<LearningItemKind>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ReviewDirection>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<LearningPhase>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<ReviewAnswer>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<StudySessionStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<ReminderSlot>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<DeliveryStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<AiProviderKind>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<AiCheckStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Domain.Writing.WritingVerdict>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Domain.Testing.TestRunStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Domain.Testing.TestPurpose>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<Domain.Testing.TestQuestionKind>().HaveConversion<string>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.DisplayName).HasMaxLength(100);
            e.Property(u => u.TimeZone).HasMaxLength(64);
            e.Property(u => u.Roles).HasConversion<int>(); // flags
            e.Property(u => u.PaymentCode).HasMaxLength(8);
            e.HasIndex(u => u.PaymentCode).IsUnique();
            e.Property(u => u.Version).IsRowVersion();
            e.HasOne<Domain.Premium.PromoCode>().WithMany().HasForeignKey(u => u.PromoCodeId).OnDelete(DeleteBehavior.Restrict);
            e.Property(u => u.SignupSource).HasMaxLength(Application.Users.SourceService.MaxLength);
        });

        b.Entity<SourceVisit>(e =>
        {
            e.ToTable("source_visits");
            e.HasKey(x => new { x.Source, x.Day });
            e.Property(x => x.Source).HasMaxLength(Application.Users.SourceService.MaxLength);
        });

        b.Entity<UserLogin>(e =>
        {
            e.ToTable("user_logins");
            e.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique(); // one account, one user (spec DATA01)
            e.HasIndex(x => x.UserId);
            e.Property(x => x.ProviderUserId).HasMaxLength(128);
            e.Property(x => x.Display).HasMaxLength(256);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Premium (spec §5.1, §7). Payments stay for bookkeeping when an account is deleted (the user row stays).
        b.Entity<Domain.Premium.Subscription>(e =>
        {
            e.ToTable("subscriptions");
            e.HasIndex(x => new { x.UserId, x.EndsAt });
            e.HasIndex(x => x.PaymentId).IsUnique();
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Domain.Premium.Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Premium.Payment>(e =>
        {
            e.ToTable("payments");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.UserId, x.PaidAt });
            e.HasIndex(x => x.PromoCodeId);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.RefundNote).HasMaxLength(500);
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Domain.Premium.PromoCode>().WithMany().HasForeignKey(x => x.PromoCodeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Premium.PromoCode>(e =>
        {
            e.ToTable("promo_codes");
            e.HasIndex(x => x.Code).IsUnique(); // stored in upper case: unique regardless of case (spec DATA01)
            e.Property(x => x.Code).HasMaxLength(32);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.TeacherUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<UserActivityDay>(e =>
        {
            e.ToTable("user_activity_days");
            e.HasKey(x => new { x.UserId, x.Day });
            e.HasIndex(x => x.Day);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Teachers (spec §9).
        b.Entity<Domain.Teaching.Group>(e =>
        {
            e.ToTable("groups");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.InviteCode).HasMaxLength(16);
            e.HasIndex(x => x.InviteCode).IsUnique();
            e.HasIndex(x => x.TeacherUserId);
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.TeacherUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Teaching.GroupMember>(e =>
        {
            e.ToTable("group_members");
            e.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne<Domain.Teaching.Group>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Teaching.CommissionPayout>(e =>
        {
            e.ToTable("commission_payouts");
            e.HasIndex(x => new { x.TeacherUserId, x.Month, x.Currency }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.TeacherUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Operations.AdminAuditEntry>(e =>
        {
            e.ToTable("admin_audit");
            e.Property(x => x.Action).HasMaxLength(32);
            e.Property(x => x.Detail).HasMaxLength(500);
            e.HasIndex(x => new { x.TargetUserId, x.At });
        });

        // The shared catalog (spec §2, §5.1): one copy for everyone, no owner.
        b.Entity<Domain.Catalog.Unit>(e =>
        {
            e.ToTable("units");
            e.HasIndex(x => new { x.Level, x.Number }).IsUnique(); // spec DATA01
            e.Property(x => x.Version).IsRowVersion();
        });

        b.Entity<Domain.Catalog.CatalogWord>(e =>
        {
            e.ToTable("catalog_words");
            e.HasIndex(x => x.SourceKey).IsUnique(); // re-import never duplicates (spec §2.4)
            e.HasIndex(x => new { x.Level, x.FreqRank });
            e.HasIndex(x => new { x.UnitId, x.OrderInUnit });
            e.Property(x => x.SourceKey).HasMaxLength(200);
            e.Property(x => x.Word).HasMaxLength(100);
            e.Property(x => x.Homonym).HasMaxLength(8);
            e.Property(x => x.Pos).HasMaxLength(32);
            e.Property(x => x.Gloss).HasMaxLength(200);
            e.Property(x => x.TranslationUz).HasMaxLength(300);
            e.Property(x => x.Romanization).HasMaxLength(100);
            e.Property(x => x.ExampleKo).HasMaxLength(200);
            e.Property(x => x.ExampleUz).HasMaxLength(300);
            e.Property(x => x.TrapUz).HasMaxLength(1000);
            e.Property(x => x.Theme).HasMaxLength(40);
            e.Property(x => x.Source).HasMaxLength(200);
            e.Property(x => x.AiProvider).HasMaxLength(64);
            e.Property(x => x.AiModel).HasMaxLength(128);
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<Domain.Catalog.Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Catalog.CatalogUsage>(e =>
        {
            e.ToTable("catalog_usages");
            e.HasIndex(x => x.CatalogWordId).IsUnique();
            e.Property(x => x.Questions).HasColumnType("jsonb").HasConversion(JsonConverter<List<UsageQuestion>>(), ListComparer<UsageQuestion>());
            e.HasOne<Domain.Catalog.CatalogWord>().WithMany().HasForeignKey(x => x.CatalogWordId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WordUsage>(e =>
        {
            e.ToTable("word_usages");
            e.HasIndex(x => new { x.OwnerId, x.LearningItemId }).IsUnique();
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.Phrases).HasColumnType("jsonb").HasConversion(JsonConverter<List<UsagePair>>(), ListComparer<UsagePair>());
            e.Property(x => x.Example).HasColumnType("jsonb").HasConversion(JsonConverter<UsagePair?>());
            e.Property(x => x.Questions).HasColumnType("jsonb").HasConversion(JsonConverter<List<UsageQuestion>>(), ListComparer<UsageQuestion>());
            e.Property(x => x.TrapUz).HasMaxLength(1000);
            e.Property(x => x.Provider).HasMaxLength(64);
            e.Property(x => x.Model).HasMaxLength(128);
            e.Property(x => x.Warning).HasMaxLength(500);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LearningItem>().WithMany().HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Cascade);
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

        b.Entity<LearningItem>(e =>
        {
            e.ToTable("learning_items");
            e.HasIndex(x => x.CatalogWordId).IsUnique(); // one item per catalog word (spec §5.2)
            e.HasIndex(x => new { x.UnitId, x.Order });
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.Korean).HasMaxLength(200);
            e.Property(x => x.KoreanNote).HasMaxLength(200);
            e.Property(x => x.Uzbek).HasMaxLength(300);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Domain.Catalog.CatalogWord>().WithMany().HasForeignKey(x => x.CatalogWordId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Domain.Catalog.Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            // Catalog items have a word and no owner; personal ones the other way round (spec §5.2).
            e.ToTable(t => t.HasCheckConstraint("CK_learning_items_kind",
                "(\"Kind\" = 'Catalog' AND \"CatalogWordId\" IS NOT NULL AND \"OwnerId\" IS NULL) OR (\"Kind\" = 'Personal' AND \"CatalogWordId\" IS NULL AND \"OwnerId\" IS NOT NULL)"));
        });

        b.Entity<Domain.Catalog.UserUnitProgress>(e =>
        {
            e.ToTable("user_unit_progress");
            e.HasIndex(x => new { x.UserId, x.UnitId }).IsUnique(); // spec DATA01
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Domain.Catalog.Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Card>(e =>
        {
            e.ToTable("cards");
            e.HasIndex(x => new { x.OwnerId, x.LearningItemId, x.Direction }).IsUnique(); // spec DATA01
            e.HasIndex(x => new { x.OwnerId, x.DueAt });
            e.Property(x => x.AlgorithmVersion).HasMaxLength(32);
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<LearningItem>().WithMany().HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Restrict);
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

        b.Entity<Domain.Testing.TestRun>(e =>
        {
            e.ToTable("test_runs");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            e.HasIndex(x => new { x.OwnerId, x.UnitId, x.Purpose, x.CreatedAt }); // "Bilaman" attempts per day (spec §4.5)
            e.Property(x => x.Version).IsRowVersion();
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Warning).HasMaxLength(500);
            e.Property(x => x.Provider).HasMaxLength(64);
            e.HasOne<Domain.Catalog.Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Questions).HasColumnType("jsonb").HasConversion(
                JsonConverter<List<Domain.Testing.TestQuestion>>(),
                new ValueComparer<List<Domain.Testing.TestQuestion>>((a, c) => a!.SequenceEqual(c!), v => v.Count, v => v.ToList()));
            e.Property(x => x.Answers).HasColumnType("jsonb").HasConversion(
                JsonConverter<List<Domain.Testing.TestAnswer>>(),
                new ValueComparer<List<Domain.Testing.TestAnswer>>((a, c) => a!.SequenceEqual(c!), v => v.Count, v => v.ToList()));
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Domain.Testing.QuestionReport>(e =>
        {
            e.ToTable("question_reports");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.ReporterId, x.TestRunId, x.QuestionIndex }).IsUnique();
            e.HasIndex(x => x.ResolvedAt);
            e.Property(x => x.Prompt).HasMaxLength(500);
            e.Property(x => x.Sentence).HasMaxLength(500);
            e.Property(x => x.Translation).HasMaxLength(500);
            e.Property(x => x.Correct).HasMaxLength(500);
            e.Property(x => x.Given).HasMaxLength(500);
            e.Property(x => x.Comment).HasMaxLength(500);
            e.Property(x => x.Options).HasColumnType("jsonb").HasConversion(
                JsonConverter<List<string>>(),
                new ValueComparer<List<string>>((a, c) => a!.SequenceEqual(c!), v => v.Count, v => v.ToList()));
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
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
            e.HasOne<LearningItem>().WithMany().HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Restrict);
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
            e.HasOne<LearningItem>().WithMany().HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Restrict);
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

