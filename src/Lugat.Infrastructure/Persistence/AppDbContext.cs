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

    public DbSet<WordSense> WordSenses => Set<WordSense>();

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
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.DisplayName).HasMaxLength(100);
            e.Property(u => u.TimeZone).HasMaxLength(64);
            e.Property(u => u.Roles).HasConversion<int>(); // flags
            e.Property(u => u.Version).IsRowVersion();
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
            e.HasIndex(x => new { x.OwnerId, x.TableNumber, x.Order }).IsUnique(); // one word per place in the list
            e.Property(x => x.Korean).HasMaxLength(200);
            e.Property(x => x.KoreanNote).HasMaxLength(200);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
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

