using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Reminders;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Reminders;

/// <summary>A learner's devices that may get reminders by Web Push (L-63).</summary>
public sealed class PushSubscriptionService(IAppDbContext db, IWebPushSender push, TimeProvider time)
{
    public const int MaxDevices = 10;

    /// <summary>
    /// Saves this device. The endpoint is the device: subscribing again (or after another learner signed in on the same
    /// phone) moves it to this learner with the new keys. Turning push on also turns reminders on.
    /// </summary>
    public async Task SubscribeAsync(Guid userId, PushSubscribeRequest request, string? userAgent, CancellationToken ct)
    {
        if (!push.IsConfigured)
        {
            throw new ServiceUnavailableException("Bildirishnomalar hozircha serverda yoqilmagan.");
        }

        var endpoint = (request.Endpoint ?? "").Trim();
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || endpoint.Length > 1000
            || string.IsNullOrWhiteSpace(request.P256dh) || request.P256dh.Length > 200 || string.IsNullOrWhiteSpace(request.Auth) || request.Auth.Length > 100)
        {
            throw new ValidationException("Qurilma obunasi noto‘g‘ri. Sahifani yangilab qayta urinib ko‘ring.");
        }

        var now = time.GetUtcNow();
        var row = await db.PushSubscriptions.SingleOrDefaultAsync(s => s.Endpoint == endpoint, ct);
        if (row is null)
        {
            if (await db.PushSubscriptions.CountAsync(s => s.UserId == userId, ct) >= MaxDevices)
            {
                // The oldest device makes room: a learner rarely has more phones than this.
                var oldest = await db.PushSubscriptions.Where(s => s.UserId == userId).OrderBy(s => s.CreatedAt).FirstAsync(ct);
                db.PushSubscriptions.Remove(oldest);
            }

            row = new PushSubscription { Id = Guid.CreateVersion7(now), Endpoint = endpoint, CreatedAt = now };
            db.PushSubscriptions.Add(row);
        }

        (row.UserId, row.P256dh, row.Auth, row.FailedAt) = (userId, request.P256dh.Trim(), request.Auth.Trim(), null);
        row.UserAgent = userAgent is { Length: > 300 } ? userAgent[..300] : userAgent;
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        user.RemindersEnabled = true;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same device was saved by a second tap at the same moment; that row is this one.
            db.DiscardChanges();
        }
    }

    /// <summary>Forgets this device, only when it is the learner's own; another's endpoint changes nothing.</summary>
    public async Task UnsubscribeAsync(Guid userId, string? endpoint, CancellationToken ct) =>
        await db.PushSubscriptions.Where(s => s.UserId == userId && s.Endpoint == (endpoint ?? "").Trim()).ExecuteDeleteAsync(ct);

    public async Task<int> DevicesAsync(Guid userId, CancellationToken ct) =>
        await db.PushSubscriptions.CountAsync(s => s.UserId == userId, ct);
}
