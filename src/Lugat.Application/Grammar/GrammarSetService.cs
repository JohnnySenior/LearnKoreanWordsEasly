using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Grammar;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Grammar;

/// <summary>
/// Grammar sets ("to‘plam", D-28): the points the teacher gave to learn together, with an optional deadline. Progress
/// comes from the points' mastery (D-27); the daily target spreads the not-yet-started points over the days left.
/// </summary>
public sealed class GrammarSetService(IAppDbContext db, GrammarStudyService study, TimeProvider time)
{
    public const int MaxItems = 60;

    public async Task<IReadOnlyList<GrammarSetDto>> ListAsync(Guid ownerId, CancellationToken ct)
    {
        var ctx = await LoadAsync(ownerId, ct);
        return ctx.Sets.OrderByDescending(s => s.Number).Select(ctx.ToDto).ToList();
    }

    public async Task<GrammarSetDetailDto> GetAsync(Guid ownerId, Guid setId, CancellationToken ct)
    {
        var ctx = await LoadAsync(ownerId, ct);
        var set = ctx.Sets.SingleOrDefault(s => s.Id == setId) ?? throw new NotFoundException("To‘plam topilmadi.");
        return ctx.Detail(set);
    }

    public async Task<GrammarTodayDto> TodayAsync(Guid ownerId, CancellationToken ct)
    {
        var ctx = await LoadAsync(ownerId, ct);
        var dtos = ctx.Sets.OrderByDescending(s => s.Number).Select(ctx.ToDto).ToList();
        var current = dtos.FirstOrDefault(s => !s.Overdue && s.Independent < s.Total);

        // Overdue sets: what is not yet "Mustaqil" keeps coming back until it is.
        var review = ctx.Sets
            .Where(s => ctx.IsOverdue(s))
            .OrderBy(s => s.Number)
            .SelectMany(s => s.Items.OrderBy(i => i.Position))
            .Select(i => ctx.Items.GetValueOrDefault(i.GrammarItemId))
            .Where(i => i is not null && i.Mastery != GrammarMastery.Independent)
            .ToList();
        var free = ctx.Items.Values.Count(i => i.Status == GrammarItemStatus.Approved && i.SetNumber is null);
        return new GrammarTodayDto(current, review.Count, review.FirstOrDefault()?.Id, free);
    }

    public async Task<GrammarSetDetailDto> CreateAsync(Guid ownerId, CreateGrammarSetRequest request, CancellationToken ct)
    {
        var ids = (request.ItemIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new ValidationException("To‘plamga kamida bitta band tanlang.");
        }

        if (ids.Count > MaxItems)
        {
            throw new ValidationException($"Bir to‘plamda ko‘pi bilan {MaxItems} ta band bo‘ladi.");
        }

        var today = await TodayOfAsync(ownerId, ct);
        if (request.DueDate is { } due && due < today)
        {
            throw new ValidationException("Muddat o‘tib ketgan sana bo‘lmasin.");
        }

        await CheckFreeAsync(ownerId, ids, ct);
        var number = (await db.GrammarSets.Where(s => s.OwnerId == ownerId).MaxAsync(s => (int?)s.Number, ct) ?? 0) + 1;
        var set = new GrammarSet
        {
            Id = Guid.CreateVersion7(time.GetUtcNow()),
            OwnerId = ownerId,
            Number = number,
            DueDate = request.DueDate,
            CreatedAt = time.GetUtcNow(),
        };
        set.Items = ids.Select((id, i) => new GrammarSetItem { GrammarSetId = set.Id, GrammarItemId = id, OwnerId = ownerId, Position = i }).ToList();
        db.GrammarSets.Add(set);
        await SaveAsync(ct);
        return await GetAsync(ownerId, set.Id, ct);
    }

    public async Task<GrammarSetDetailDto> UpdateAsync(Guid ownerId, Guid setId, UpdateGrammarSetRequest request, CancellationToken ct)
    {
        var set = await db.GrammarSets.Include(s => s.Items).SingleOrDefaultAsync(s => s.Id == setId && s.OwnerId == ownerId, ct)
            ?? throw new NotFoundException("To‘plam topilmadi.");
        if (set.Version != request.Version)
        {
            throw new ConflictException("To‘plam boshqa oynada o‘zgartirilgan. Sahifani yangilang.");
        }

        if (request.ClearDueDate)
        {
            set.DueDate = null;
        }
        else if (request.DueDate is { } due)
        {
            set.DueDate = due;
        }

        if (request.RemoveItemIds is { Count: > 0 } remove)
        {
            set.Items.RemoveAll(i => remove.Contains(i.GrammarItemId));
        }

        if (request.AddItemIds is { Count: > 0 } add)
        {
            var ids = add.Distinct().Where(id => set.Items.All(i => i.GrammarItemId != id)).ToList();
            await CheckFreeAsync(ownerId, ids, ct);
            var position = set.Items.Count == 0 ? 0 : set.Items.Max(i => i.Position) + 1;
            set.Items.AddRange(ids.Select((id, i) => new GrammarSetItem { GrammarSetId = set.Id, GrammarItemId = id, OwnerId = ownerId, Position = position + i }));
        }

        if (set.Items.Count == 0)
        {
            throw new ValidationException("To‘plamda kamida bitta band qolsin. Kerak bo‘lmasa, to‘plamni o‘chiring.");
        }

        if (set.Items.Count > MaxItems)
        {
            throw new ValidationException($"Bir to‘plamda ko‘pi bilan {MaxItems} ta band bo‘ladi.");
        }

        await SaveAsync(ct);
        return await GetAsync(ownerId, set.Id, ct);
    }

    /// <summary>The points go back to the pool; their sentences and progress stay.</summary>
    public async Task DeleteAsync(Guid ownerId, Guid setId, CancellationToken ct)
    {
        var set = await db.GrammarSets.Include(s => s.Items).SingleOrDefaultAsync(s => s.Id == setId && s.OwnerId == ownerId, ct)
            ?? throw new NotFoundException("To‘plam topilmadi.");
        db.GrammarSets.Remove(set);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Only approved points (spec AC03) that are in no other set.</summary>
    private async Task CheckFreeAsync(Guid ownerId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var found = await db.GrammarItems
            .Where(i => i.OwnerId == ownerId && ids.Contains(i.Id) && i.Status != GrammarItemStatus.Excluded)
            .Select(i => new { i.Id, i.Form, i.Status })
            .ToListAsync(ct);
        if (found.Count != ids.Count)
        {
            throw new NotFoundException("Tanlangan bandlardan biri topilmadi.");
        }

        if (found.FirstOrDefault(i => i.Status != GrammarItemStatus.Approved) is { } unchecked_)
        {
            throw new ValidationException($"{unchecked_.Form} hali tasdiqlanmagan. Avval uni tasdiqlang.");
        }

        var taken = await db.GrammarSetItems
            .Where(x => x.OwnerId == ownerId && ids.Contains(x.GrammarItemId))
            .Join(db.GrammarSets, x => x.GrammarSetId, s => s.Id, (x, s) => new { x.GrammarItemId, s.Number })
            .FirstOrDefaultAsync(ct);
        if (taken is not null)
        {
            var form = found.Single(i => i.Id == taken.GrammarItemId).Form;
            throw new ConflictException($"{form} allaqachon {taken.Number}-to‘plamda.");
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("To‘plam boshqa oynada o‘zgartirilgan. Sahifani yangilang.");
        }
        catch (DbUpdateException)
        {
            // Unique indexes: a set number or a point taken in parallel (double tap).
            throw new ConflictException("Bu bandlar yoki to‘plam hozirgina boshqa oynada o‘zgardi. Sahifani yangilang.");
        }
    }

    private async Task<DateOnly> TodayOfAsync(Guid ownerId, CancellationToken ct)
    {
        var zone = (await db.Users.SingleAsync(u => u.Id == ownerId, ct)).Zone();
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
    }

    private async Task<Context> LoadAsync(Guid ownerId, CancellationToken ct)
    {
        var sets = await db.GrammarSets.Include(s => s.Items).Where(s => s.OwnerId == ownerId).AsNoTracking().ToListAsync(ct);
        var items = (await study.ListAsync(ownerId, ct)).ToDictionary(i => i.Id);
        var attempts = await study.AttemptsAsync(ownerId, null, ct);
        return new Context(sets, items, attempts, await TodayOfAsync(ownerId, ct));
    }

    private sealed record Context(
        List<GrammarSet> Sets,
        Dictionary<Guid, GrammarListItemDto> Items,
        Dictionary<Guid, List<GrammarAttempt>> Attempts,
        DateOnly Today)
    {
        public bool IsOverdue(GrammarSet s) => s.DueDate is { } d && d < Today;

        private IEnumerable<GrammarListItemDto> ItemsOf(GrammarSet s) =>
            s.Items.OrderBy(i => i.Position).Select(i => Items.GetValueOrDefault(i.GrammarItemId)).OfType<GrammarListItemDto>();

        public GrammarSetDto ToDto(GrammarSet s)
        {
            var items = ItemsOf(s).ToList();
            DateOnly? FirstDay(Guid id) => Attempts.GetValueOrDefault(id) is { Count: > 0 } a ? a[0].Day : null;
            var startedToday = items.Count(i => FirstDay(i.Id) == Today);
            var notStartedAtDayStart = items.Count(i => FirstDay(i.Id) is not { } d || d >= Today);
            var next = items.FirstOrDefault(i => i.Sentences == 0) ?? items.FirstOrDefault(i => i.Mastery != GrammarMastery.Independent);
            return new GrammarSetDto(
                s.Id,
                s.Number,
                s.DueDate,
                items.Count,
                items.Count(i => i.Sentences > 0),
                items.Count(i => i.Mastery == GrammarMastery.Independent),
                GrammarPace.TodayTarget(notStartedAtDayStart, Today, s.DueDate),
                startedToday,
                IsOverdue(s),
                next?.Id,
                s.CreatedAt,
                s.Version);
        }

        public GrammarSetDetailDto Detail(GrammarSet s) => new(ToDto(s), ItemsOf(s).ToList());
    }
}
