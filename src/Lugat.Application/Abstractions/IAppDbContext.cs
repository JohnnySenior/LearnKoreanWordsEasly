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

    DbSet<WordSense> WordSenses { get; }

    DbSet<WordUsage> WordUsages { get; }

    DbSet<Domain.Ladder.SentenceLadder> SentenceLadders { get; }

    DbSet<Domain.Ladder.TrapNote> TrapNotes { get; }

    DbSet<Domain.Listening.ListeningSet> ListeningSets { get; }

    DbSet<Card> Cards { get; }

    DbSet<ReviewEvent> ReviewEvents { get; }

    DbSet<StudySession> StudySessions { get; }

    DbSet<Domain.Reminders.ReminderDelivery> ReminderDeliveries { get; }

    DbSet<Domain.Ai.AiUsage> AiUsages { get; }

    DbSet<Domain.Ai.SentenceCheck> SentenceChecks { get; }

    DbSet<Domain.Writing.WritingAttempt> WritingAttempts { get; }

    DbSet<Domain.Testing.TestRun> TestRuns { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Forgets tracked changes after a failed save, so follow-up reads see the database.</summary>
    void DiscardChanges();

    /// <summary>For changes that span several saves and must succeed or fail together.</summary>
    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
}
