using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Premium;
using Lugat.Domain.Teaching;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Teaching;

/// <summary>
/// Groups from the learner's side (spec §9.1): see who invites, agree and join, leave from the profile. Joining
/// brings the teacher's promo code to the Premium page.
/// </summary>
public sealed class GroupService(IAppDbContext db, TimeProvider time)
{
    /// <summary>What the join page shows before the learner agrees; an unknown or closed link is 404.</summary>
    public async Task<GroupInviteDto> InviteAsync(Guid userId, string inviteCode, CancellationToken ct)
    {
        var group = await OpenGroupAsync(inviteCode, ct);
        var teacher = await db.Users.Where(u => u.Id == group.TeacherUserId).Select(u => u.DisplayName).SingleOrDefaultAsync(ct);
        var member = await db.GroupMembers.AnyAsync(m => m.GroupId == group.Id && m.UserId == userId && m.LeftAt == null, ct);
        return new GroupInviteDto(group.Name, teacher, member);
    }

    /// <summary>
    /// The learner agreed (spec §9.1): from now the teacher sees their unit results and most-missed words. Joining
    /// again changes nothing; joining after leaving starts a new membership.
    /// </summary>
    public async Task<MyGroupDto> JoinAsync(Guid userId, string inviteCode, CancellationToken ct)
    {
        var group = await OpenGroupAsync(inviteCode, ct);
        if (group.TeacherUserId == userId)
        {
            throw new ValidationException("Bu sizning guruhingiz: o‘z guruhingizga o‘quvchi bo‘lib qo‘shila olmaysiz.");
        }

        var now = time.GetUtcNow();
        var member = await db.GroupMembers.SingleOrDefaultAsync(m => m.GroupId == group.Id && m.UserId == userId, ct);
        if (member is null)
        {
            db.GroupMembers.Add(new GroupMember { Id = Guid.CreateVersion7(now), GroupId = group.Id, UserId = userId, JoinedAt = now });
        }
        else if (member.LeftAt is not null)
        {
            (member.JoinedAt, member.LeftAt) = (now, null);
        }

        await AttachPromoAsync(userId, await TeacherPromoAsync(group.TeacherUserId, ct), ct);
        // The teacher's published decks come with the group (L-67).
        await GroupDeckService.GivePublishedDecksAsync(db, group.Id, userId, now, ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.DiscardChanges(); // a double tap: the other request joined
        }

        return (await MyGroupsAsync(userId, ct)).Single(g => g.Id == group.Id);
    }

    public async Task<IReadOnlyList<MyGroupDto>> MyGroupsAsync(Guid userId, CancellationToken ct) =>
        await (from m in db.GroupMembers
               join g in db.Groups on m.GroupId equals g.Id
               where m.UserId == userId && m.LeftAt == null && g.ClosedAt == null
               orderby m.JoinedAt
               select new MyGroupDto(g.Id, g.Name, db.Users.Where(u => u.Id == g.TeacherUserId).Select(u => u.DisplayName).FirstOrDefault(), m.JoinedAt))
            .ToListAsync(ct);

    /// <summary>Leaving takes effect at once: the teacher no longer sees the learner (spec §13.8).</summary>
    public async Task LeaveAsync(Guid userId, Guid groupId, CancellationToken ct)
    {
        var member = await db.GroupMembers.SingleOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, ct)
            ?? throw new NotFoundException("Guruh topilmadi.");
        var now = time.GetUtcNow();
        member.LeftAt ??= now;
        await GroupDeckService.WithdrawGroupAsync(db, groupId, userId, now, ct); // the teacher's decks go with the group (L-67)
        await db.SaveChangesAsync(ct);
    }

    /// <summary>A teacher's code from <c>/?ref=CODE</c>; an unknown or non-teacher code is ignored, not an error.</summary>
    public async Task<string?> ReferAsync(Guid userId, string code, CancellationToken ct)
    {
        var normalized = PromoCode.Normalize(code ?? "");
        var now = time.GetUtcNow();
        var promo = await db.PromoCodes.SingleOrDefaultAsync(p => p.Code == normalized && p.TeacherUserId != null && p.TeacherUserId != userId
            && p.IsActive && (p.ValidUntil == null || p.ValidUntil > now), ct);
        if (await AttachPromoAsync(userId, promo, ct))
        {
            await db.SaveChangesAsync(ct);
        }

        return await AttachedPromoAsync(db, userId, now, ct);
    }

    /// <summary>The learner's teacher code, if it still works: the Premium page fills it in.</summary>
    public static async Task<string?> AttachedPromoAsync(IAppDbContext db, Guid userId, DateTimeOffset now, CancellationToken ct) =>
        await (from u in db.Users
                      join p in db.PromoCodes on u.PromoCodeId equals p.Id
                      where u.Id == userId && p.IsActive && (p.ValidUntil == null || p.ValidUntil > now)
                      select p.Code).SingleOrDefaultAsync(ct);

    /// <summary>The first teacher who brought the learner keeps the code (and the commission); a later link does not replace it.</summary>
    private async Task<bool> AttachPromoAsync(Guid userId, PromoCode? promo, CancellationToken ct)
    {
        if (promo is null)
        {
            return false;
        }

        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        if (user.PromoCodeId is not null)
        {
            return false;
        }

        user.PromoCodeId = promo.Id;
        return true;
    }

    private async Task<PromoCode?> TeacherPromoAsync(Guid teacherId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return await db.PromoCodes.Where(p => p.TeacherUserId == teacherId && p.IsActive && (p.ValidUntil == null || p.ValidUntil > now))
            .OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(ct);
    }

    private async Task<Group> OpenGroupAsync(string inviteCode, CancellationToken ct)
    {
        var code = (inviteCode ?? "").Trim().ToUpperInvariant();
        return await db.Groups.SingleOrDefaultAsync(g => g.InviteCode == code && g.ClosedAt == null, ct)
            ?? throw new NotFoundException("Taklif havolasi topilmadi yoki endi ishlamaydi.");
    }
}
