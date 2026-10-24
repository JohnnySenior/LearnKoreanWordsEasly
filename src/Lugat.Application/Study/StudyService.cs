using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Learning;
using Lugat.Domain.Study;
using Lugat.Domain.Vocabulary;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Study;

/// <summary>
/// Practice sessions over the learner's words (spec §4.3, §4.6).
/// Every answer is an idempotent event applied in one transaction with the card and session (spec DATA01, API01).
/// </summary>
public sealed class StudyService(IAppDbContext db, ISpacedRepetitionScheduler scheduler, TimeProvider time, Catalog.UnitProgressService units)
{
    public const int DefaultSecondsPerCard = 12;

    /// <summary>A forgotten card comes back after this many other prompts, at most this many times per session.</summary>
    public const int ReinsertGap = 5;

    public const int MaxReinsertsPerCard = 2;

    public static readonly TimeSpan UndoWindow = TimeSpan.FromMinutes(5);

    public async Task<TodayDto> TodayAsync(Guid ownerId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var current = await units.CurrentAsync(ownerId, ct);
        var currentIds = await units.CurrentUnitIdsAsync(ownerId, ct);
        var cards = ActiveCards(ownerId);
        var dueNow = await cards.CountAsync(c => c.Card.Phase != LearningPhase.New && c.Card.DueAt <= now, ct);
        var newAvailable = await cards.CountAsync(c => c.Card.Phase == LearningPhase.New && c.Sense.UnitId != null && currentIds.Contains(c.Sense.UnitId.Value), ct);
        var active = await db.StudySessions.Where(s => s.OwnerId == ownerId && s.Status == StudySessionStatus.Active)
            .OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync(ct);

        return new TodayDto(
            dueNow,
            newAvailable,
            active is null ? null : await ToDtoAsync(active, ct),
            await SecondsPerCardAsync(ownerId, ct),
            current);
    }

    /// <summary>Words with at least one card due now, counted as on the home page (the reminder's "N ta so‘z", spec §8).</summary>
    public async Task<int> DueWordsAsync(Guid ownerId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return await ActiveCards(ownerId).Where(c => c.Card.Phase != LearningPhase.New && c.Card.DueAt <= now)
            .Select(c => c.Card.LearningItemId).Distinct().CountAsync(ct);
    }

    public async Task<SessionDto?> CurrentAsync(Guid ownerId, CancellationToken ct)
    {
        var active = await db.StudySessions.Where(s => s.OwnerId == ownerId && s.Status == StudySessionStatus.Active)
            .OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync(ct);
        return active is null ? null : await ShowAsync(active, ct);
    }

    public async Task<SessionDto> GetAsync(Guid ownerId, Guid sessionId, CancellationToken ct) =>
        await ShowAsync(await LoadSessionAsync(ownerId, sessionId, ct), ct);

    /// <summary>Resumes an unfinished session unless <see cref="StartSessionRequest.Restart"/> is set (spec PLAN07 "Davom etish").</summary>
    public const int MaxMistakeWords = 60;

    public async Task<StartSessionResponse> StartAsync(Guid ownerId, StartSessionRequest request, CancellationToken ct)
    {
        if (request.Minutes is { } m && (m <= 0 || m > 240))
        {
            throw new ValidationException("Mashg‘ulot vaqti 1 daqiqadan 240 daqiqagacha bo‘lishi mumkin.");
        }

        var active = await db.StudySessions.Where(s => s.OwnerId == ownerId && s.Status == StudySessionStatus.Active).ToListAsync(ct);
        if (active.Count > 0 && !request.Restart)
        {
            var resumed = await ShowAsync(active.OrderByDescending(s => s.CreatedAt).First(), ct);
            if (resumed.Status == StudySessionStatus.Active)
            {
                return new StartSessionResponse(resumed, null);
            }

            // Everything left in it was removed from the vocabulary: plan a fresh one.
            active = await db.StudySessions.Where(s => s.OwnerId == ownerId && s.Status == StudySessionStatus.Active).ToListAsync(ct);
        }

        foreach (var s in active)
        {
            s.Status = StudySessionStatus.Abandoned;
        }

        var now = time.GetUtcNow();
        var rows = await ActiveCards(ownerId).ToListAsync(ct);
        Func<LearningItem, bool>? focus = null;
        if (request.LearningItemIds is { Count: > 0 } senseIds)
        {
            // The mistakes notebook (D-41): these words only, every card, like a table drill.
            if (senseIds.Count > MaxMistakeWords)
            {
                throw new ValidationException($"Bir martada ko‘pi bilan {MaxMistakeWords} ta so‘z.");
            }

            var ids = senseIds.ToHashSet();
            focus = s => ids.Contains(s.Id);
        }
        else if (request.UnitId is { } unitId)
        {
            // The unit's cards after studying its table (spec §4.3 step 2, D-36): every word, also those not yet due.
            // A locked unit answers 403, an unknown one 404; an open one gets its cards here if it has none yet.
            await units.OpenAsync(ownerId, unitId, ct);
            rows = await ActiveCards(ownerId).ToListAsync(ct);
            focus = s => s.UnitId == unitId;
        }

        var drill = focus is not null;
        if (drill)
        {
            rows = rows.Where(r => focus!(r.Sense)).ToList();
        }
        else
        {
            // Daily review (spec §4.6): due cards of every opened unit; new words only from the current unit.
            var current = await units.CurrentUnitIdsAsync(ownerId, ct);
            focus = s => s.UnitId is { } u && current.Contains(u);
        }

        var candidates = rows
            // In a daily review only new words are tied to the current unit; due cards of every unit count the same.
            .Select(r => new PlanCandidate(r.Card.Id, r.Sense.Id, r.Card.Direction, r.Card.ToState(),
                drill || (r.Card.Phase == LearningPhase.New && focus!(r.Sense)), r.Sense.Order))
            .ToList();

        int? capacity = request.Minutes is { } minutes && !drill ? Math.Max(1, minutes * 60 / await SecondsPerCardAsync(ownerId, ct)) : null;
        var items = SessionPlanner.Plan(candidates, new PlanRequest(now, capacity, NewOnlyFromFocus: true, Drill: drill));
        if (items.Count == 0)
        {
            await db.SaveChangesAsync(ct);
            var message = rows.Count == 0
                ? request.UnitId is not null ? "Bu bo‘limda so‘z yo‘q." : "Hali o‘rganiladigan so‘z yo‘q. Bo‘limlar sahifasida birinchi bo‘limni oching."
                : "Hozircha takrorlanadigan so‘z yo‘q. Keyingi takrorlash vaqti kelganda qayta kiring.";
            return new StartSessionResponse(null, message);
        }

        var session = new StudySession
        {
            Id = Guid.CreateVersion7(now),
            OwnerId = ownerId,
            Minutes = drill ? null : request.Minutes,
            Items = items,
            Status = StudySessionStatus.Active,
            CreatedAt = now,
        };
        db.StudySessions.Add(session);
        await db.SaveChangesAsync(ct);
        return new StartSessionResponse(await ToDtoAsync(session, ct), null);
    }

    public async Task<AnswerResultDto> AnswerAsync(Guid ownerId, Guid sessionId, AnswerRequest request, CancellationToken ct)
    {
        var existing = await db.ReviewEvents.AsNoTracking().SingleOrDefaultAsync(e => e.Id == request.EventId, ct);
        if (existing is not null)
        {
            return await ReplayAsync(ownerId, existing, ct);
        }

        var session = await LoadSessionAsync(ownerId, sessionId, ct);
        if (session.Status != StudySessionStatus.Active)
        {
            throw new ConflictException("Bu mashg‘ulot yakunlangan.");
        }

        if (await SkipInactiveAsync(session, ct))
        {
            await db.SaveChangesAsync(ct);
            throw new ConflictException("Bu so‘z lug‘atdan chiqarilgan. Mashg‘ulot keyingi so‘zdan davom etadi.");
        }

        if (request.Position != session.Position || session.Position >= session.Items.Count)
        {
            throw new ConflictException("Bu savolga boshqa oynada javob berilgan. Mashg‘ulot yangilanadi.");
        }

        var item = session.Items[session.Position];
        var card = await db.Cards.SingleAsync(c => c.Id == item.CardId && c.OwnerId == ownerId, ct);
        var now = time.GetUtcNow();
        var before = card.ToState();
        var result = scheduler.Apply(before, request.Answer, request.Assisted, now, await LocalDayAsync(ownerId, now, ct));
        card.Apply(result.State);

        var reviewEvent = new ReviewEvent
        {
            Id = request.EventId,
            OwnerId = ownerId,
            SessionId = session.Id,
            SessionPosition = session.Position,
            CardId = card.Id,
            LearningItemId = card.LearningItemId,
            Direction = card.Direction,
            Answer = request.Answer,
            Assisted = request.Assisted,
            WasDue = result.WasDue,
            CountedAsIndependentSuccess = result.CountsAsIndependentSuccess,
            ResponseMs = request.ResponseMs is > 0 and < 600_000 ? request.ResponseMs : null,
            OccurredAt = now,
            AlgorithmVersion = scheduler.AlgorithmVersion,
            PreviousState = before,
            NextState = result.State,
        };

        var items = session.Items.ToList();
        if (request.Answer == ReviewAnswer.Forgot && items.Count(i => i.CardId == card.Id) <= MaxReinsertsPerCard)
        {
            var at = Math.Min(session.Position + 1 + ReinsertGap, items.Count);
            while (at < items.Count && (items[at - 1].LearningItemId == item.LearningItemId || items[at].LearningItemId == item.LearningItemId))
            {
                at++; // never right next to the same word's other direction (spec SRS03)
            }

            items.Insert(at, item with { Kind = SessionItemKind.Relearning });
            reviewEvent.ReinsertedAt = at;
        }

        session.Items = items;
        session.Position++;
        if (session.Position >= session.Items.Count)
        {
            session.Status = StudySessionStatus.Completed;
            session.CompletedAt = now;
        }

        await SkipInactiveAsync(session, ct);
        db.ReviewEvents.Add(reviewEvent);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.DiscardChanges();
            throw new ConflictException("Bu so‘z boshqa qurilmada hozirgina baholandi. Mashg‘ulot yangilanadi.");
        }
        catch (DbUpdateException)
        {
            // The same answer arrived twice at the same moment: the first one won, answer like a retry.
            db.DiscardChanges();
            var first = await db.ReviewEvents.AsNoTracking().SingleOrDefaultAsync(e => e.Id == request.EventId, ct);
            if (first is null)
            {
                throw;
            }

            return await ReplayAsync(ownerId, first, ct);
        }

        return new AnswerResultDto(reviewEvent.Id, card.DueAt, (int)card.Interval.TotalSeconds, await ToDtoAsync(session, ct));
    }

    /// <summary>
    /// Takes back the latest answer of a session (spec UX03: short undo). Only the last answer, only shortly after,
    /// and only while nothing else has changed the card.
    /// </summary>
    public async Task<SessionDto> UndoAsync(Guid ownerId, Guid eventId, CancellationToken ct)
    {
        var ev = await db.ReviewEvents.SingleOrDefaultAsync(e => e.Id == eventId && e.OwnerId == ownerId, ct)
            ?? throw new NotFoundException("Javob topilmadi.");
        if (ev.UndoneAt is not null || ev.SessionId is null)
        {
            throw new ConflictException("Bu javobni bekor qilib bo‘lmaydi.");
        }

        var now = time.GetUtcNow();
        var session = await LoadSessionAsync(ownerId, ev.SessionId.Value, ct);
        var latest = await db.ReviewEvents.Where(e => e.SessionId == session.Id && e.UndoneAt == null)
            .OrderByDescending(e => e.SessionPosition).Select(e => e.Id).FirstAsync(ct);
        if (latest != ev.Id || now - ev.OccurredAt > UndoWindow || session.Position <= ev.SessionPosition)
        {
            throw new ConflictException("Faqat oxirgi javobni, bir necha daqiqa ichida bekor qilish mumkin.");
        }

        // Undo only restores a state nobody has built on: not when the same card was answered again elsewhere
        // (another session or device), and not for a session that has been replaced by a newer one.
        var answeredSince = await db.ReviewEvents.AnyAsync(
            e => e.CardId == ev.CardId && e.Id != ev.Id && e.UndoneAt == null && e.OccurredAt >= ev.OccurredAt, ct);
        var card = await db.Cards.SingleAsync(c => c.Id == ev.CardId, ct);
        if (answeredSince || card.LastReviewedAt != ev.NextState.LastReviewedAt || card.DueAt != ev.NextState.DueAt)
        {
            throw new ConflictException("Bu so‘z keyinroq yana baholangan, shuning uchun bu javobni bekor qilib bo‘lmaydi.");
        }

        if (session.Status == StudySessionStatus.Abandoned
            || await db.StudySessions.AnyAsync(s => s.OwnerId == ownerId && s.Id != session.Id && s.Status == StudySessionStatus.Active, ct))
        {
            throw new ConflictException("Yangi mashg‘ulot boshlangan, eski mashg‘ulotdagi javobni bekor qilib bo‘lmaydi.");
        }

        card.Apply(ev.PreviousState);

        var items = session.Items.ToList();
        if (ev.ReinsertedAt is { } at && at < items.Count && items[at].CardId == ev.CardId)
        {
            items.RemoveAt(at);
        }

        session.Items = items;
        session.Position = ev.SessionPosition!.Value;
        session.Status = StudySessionStatus.Active;
        session.CompletedAt = null;
        ev.UndoneAt = now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("So‘z boshqa joyda o‘zgargan, bekor qilib bo‘lmadi.");
        }

        return await ToDtoAsync(session, ct);
    }

    private async Task<AnswerResultDto> ReplayAsync(Guid ownerId, ReviewEvent existing, CancellationToken ct)
    {
        if (existing.OwnerId != ownerId || existing.SessionId is null)
        {
            throw new NotFoundException();
        }

        var session = await LoadSessionAsync(ownerId, existing.SessionId.Value, ct);
        return new AnswerResultDto(existing.Id, existing.NextState.DueAt, (int)existing.NextState.Interval.TotalSeconds, await ToDtoAsync(session, ct));
    }

    private async Task<StudySession> LoadSessionAsync(Guid ownerId, Guid sessionId, CancellationToken ct) =>
        await db.StudySessions.SingleOrDefaultAsync(s => s.Id == sessionId && s.OwnerId == ownerId, ct)
            ?? throw new NotFoundException("Mashg‘ulot topilmadi.");

    /// <summary>
    /// A session is planned up front, so a word taken out of the vocabulary afterwards may still be queued.
    /// Such items are stepped over: only active words are practised. Returns true if the position moved.
    /// </summary>
    private async Task<bool> SkipInactiveAsync(StudySession session, CancellationToken ct)
    {
        if (session.Status != StudySessionStatus.Active || session.Position >= session.Items.Count)
        {
            return false;
        }

        var remaining = session.Items.Skip(session.Position).Select(i => i.LearningItemId).Distinct().ToList();
        var inactive = (await db.LearningItems.Where(s => remaining.Contains(s.Id) && !s.IsActive).Select(s => s.Id).ToListAsync(ct)).ToHashSet();
        var start = session.Position;
        while (session.Position < session.Items.Count && inactive.Contains(session.Items[session.Position].LearningItemId))
        {
            session.Position++;
        }

        if (session.Position >= session.Items.Count)
        {
            session.Status = StudySessionStatus.Completed;
            session.CompletedAt = time.GetUtcNow();
        }

        return session.Position != start;
    }

    /// <summary>Loads a session for display, stepping over removed words first.</summary>
    private async Task<SessionDto> ShowAsync(StudySession session, CancellationToken ct)
    {
        if (await SkipInactiveAsync(session, ct))
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone answered at the same moment; show what the database has now.
                db.DiscardChanges();
                session = await db.StudySessions.SingleAsync(s => s.Id == session.Id, ct);
            }
        }

        return await ToDtoAsync(session, ct);
    }

    private async Task<SessionDto> ToDtoAsync(StudySession session, CancellationToken ct)
    {
        var events = await db.ReviewEvents.AsNoTracking().Where(e => e.SessionId == session.Id && e.UndoneAt == null)
            .Select(e => new { e.Id, e.Answer, e.SessionPosition, e.OccurredAt, New = e.PreviousState })
            .ToListAsync(ct);
        var summary = new SessionSummaryDto(
            events.Count,
            events.Count(e => e.Answer == ReviewAnswer.Forgot),
            events.Count(e => e.Answer == ReviewAnswer.Hard),
            events.Count(e => e.Answer == ReviewAnswer.Easy),
            events.Count(e => e.New.Phase == LearningPhase.New));

        var now = time.GetUtcNow();
        var last = events.OrderByDescending(e => e.SessionPosition).FirstOrDefault();
        Guid? undoable = last is not null && session.Status != StudySessionStatus.Abandoned
            && now - last.OccurredAt <= UndoWindow && session.Position > last.SessionPosition ? last.Id : null;

        CardPromptDto? current = null;
        if (session.Status == StudySessionStatus.Active && session.Position < session.Items.Count)
        {
            current = await PromptAsync(session.Items[session.Position], session.Position, now, ct);
        }

        return new SessionDto(session.Id, session.Status, session.Minutes, session.Position, session.Items.Count, current, summary, undoable);
    }

    private async Task<CardPromptDto> PromptAsync(SessionItem item, int position, DateTimeOffset now, CancellationToken ct)
    {
        var card = await db.Cards.AsNoTracking().SingleAsync(c => c.Id == item.CardId, ct);
        var sense = await db.LearningItems.AsNoTracking().SingleAsync(s => s.Id == item.LearningItemId, ct);
        var state = card.ToState();
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var previews = new[] { ReviewAnswer.Forgot, ReviewAnswer.Hard, ReviewAnswer.Easy }
            .Select(a =>
            {
                var r = scheduler.Apply(state, a, assisted: false, now, day);
                return new RatingPreview(a, (int)r.State.Interval.TotalSeconds, !ReferenceEquals(r.State, state) && r.State.DueAt != state.DueAt);
            })
            .ToList();

        var koreanToUzbek = item.Direction == ReviewDirection.KoreanToUzbek;
        // The catalog's checked trap (spec §2.3), else the learner's own usage note (D-46).
        var trap = (sense.CatalogWordId is { } wordId
                ? await db.CatalogWords.AsNoTracking().Where(w => w.Id == wordId).Select(w => w.TrapUz).FirstOrDefaultAsync(ct)
                : null)
            ?? await db.WordUsages.AsNoTracking()
                .Where(u => u.OwnerId == card.OwnerId && u.LearningItemId == sense.Id && u.ContentVersion == sense.ContentVersion)
                .Select(u => u.TrapUz).FirstOrDefaultAsync(ct);
        var unit = sense.UnitId is { } unitRef ? (await LearnerItems.UnitLabelsAsync(db, [unitRef], ct)).GetValueOrDefault(unitRef) : null;
        return new CardPromptDto(
            position,
            card.Id,
            sense.Id,
            item.Direction,
            item.Kind,
            koreanToUzbek ? sense.Korean : sense.Uzbek,
            koreanToUzbek ? sense.KoreanNote : null,
            koreanToUzbek ? sense.Uzbek : sense.Korean,
            koreanToUzbek ? null : sense.KoreanNote,
            unit,
            state.IsDue(now),
            previews,
            trap,
            koreanToUzbek ? null : await Vocabulary.MeaningTwins.HintAsync(db, card.OwnerId, sense, ct));
    }

    private IQueryable<ActiveCard> ActiveCards(Guid ownerId) =>
        from c in db.Cards
        join s in db.LearningItems on c.LearningItemId equals s.Id
        where c.OwnerId == ownerId && s.IsActive
        select new ActiveCard { Card = c, Sense = s }; // member-init, so EF can still filter on it

    private sealed class ActiveCard
    {
        public required Card Card { get; init; }

        public required LearningItem Sense { get; init; }
    }

    /// <summary>
    /// Estimated seconds per prompt from the learner's own recent answers (spec PLAN06): median answer time plus a few
    /// seconds for revealing and rating. Only an estimate; never a promise.
    /// </summary>
    private async Task<int> SecondsPerCardAsync(Guid ownerId, CancellationToken ct)
    {
        var recent = await db.ReviewEvents.Where(e => e.OwnerId == ownerId && e.ResponseMs != null)
            .OrderByDescending(e => e.OccurredAt).Take(100).Select(e => e.ResponseMs!.Value).ToListAsync(ct);
        if (recent.Count < 10)
        {
            return DefaultSecondsPerCard;
        }

        recent.Sort();
        var medianSeconds = recent[recent.Count / 2] / 1000.0;
        return (int)Math.Clamp(Math.Round(medianSeconds + 4), 6, 40);
    }

    private async Task<DateOnly> LocalDayAsync(Guid ownerId, DateTimeOffset now, CancellationToken ct)
    {
        var zoneId = await db.Users.Where(u => u.Id == ownerId).Select(u => u.TimeZone).SingleAsync(ct);
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var found) ? found : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }
}
