using Lugat.Domain.Learning;
using Lugat.Domain.Study;
using Lugat.Domain.Users;
using Lugat.Domain.Vocabulary;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<UserLogin> UserLogins { get; }

    DbSet<UserActivityDay> ActivityDays { get; }

    DbSet<SourceVisit> SourceVisits { get; }

    DbSet<Domain.Premium.Subscription> Subscriptions { get; }

    DbSet<Domain.Premium.PremiumView> PremiumViews { get; }

    DbSet<Domain.Premium.Payment> Payments { get; }

    DbSet<Domain.Premium.PromoCode> PromoCodes { get; }

    DbSet<Domain.Teaching.Group> Groups { get; }

    DbSet<Domain.Teaching.GroupMember> GroupMembers { get; }

    DbSet<Domain.Teaching.CommissionPayout> CommissionPayouts { get; }

    DbSet<Domain.Operations.AdminAuditEntry> AdminAudit { get; }

    DbSet<Domain.Catalog.CatalogWord> CatalogWords { get; }

    DbSet<Domain.Catalog.WordSlug> WordSlugs { get; }

    DbSet<Domain.Catalog.CatalogUsage> CatalogUsages { get; }

    DbSet<Domain.Catalog.Unit> Units { get; }

    DbSet<Domain.Catalog.UnitIntroduction> UnitIntroductions { get; }

    DbSet<LearningItem> LearningItems { get; }

    DbSet<WordDeck> WordDecks { get; }

    DbSet<PersonalWord> PersonalWords { get; }

    DbSet<Domain.Catalog.UserUnitProgress> UnitProgress { get; }

    DbSet<WordUsage> WordUsages { get; }

    DbSet<Domain.Ladder.SentenceLadder> SentenceLadders { get; }

    DbSet<Domain.Ladder.TrapNote> TrapNotes { get; }

    DbSet<Domain.Listening.ListeningSet> ListeningSets { get; }

    DbSet<Domain.Games.Crossword> Crosswords { get; }

    DbSet<Card> Cards { get; }

    DbSet<ReviewEvent> ReviewEvents { get; }

    DbSet<StudySession> StudySessions { get; }

    DbSet<Domain.Reminders.ReminderDelivery> ReminderDeliveries { get; }

    DbSet<Domain.Reminders.PushSubscription> PushSubscriptions { get; }

    DbSet<Domain.Ai.AiUsage> AiUsages { get; }

    DbSet<Domain.Ai.SentenceCheck> SentenceChecks { get; }

    DbSet<Domain.Writing.WritingAttempt> WritingAttempts { get; }

    DbSet<Domain.Testing.TestRun> TestRuns { get; }

    DbSet<Domain.Testing.QuestionReport> QuestionReports { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Forgets tracked changes after a failed save, so follow-up reads see the database.</summary>
    void DiscardChanges();

    /// <summary>For changes that span several saves and must succeed or fail together.</summary>
    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
}
