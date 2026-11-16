using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Reminders;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Reminders;

/// <summary>A learner's devices that may get reminders by Web Push (L-63).</summary>
public sealed class PushSubscriptionService(IAppDbContext db, IWebPushSender push, TimeProvider time, IOperationLock operations)
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
        if (!PushEndpointPolicy.IsAllowed(endpoint)
            || string.IsNullOrWhiteSpace(request.P256dh) || request.P256dh.Length > 200 || string.IsNullOrWhiteSpace(request.Auth) || request.Auth.Length > 100)
        {
            throw new ValidationException("Qurilma obunasi noto‘g‘ri. Sahifani yangilab qayta urinib ko‘ring.");
        }

        // Serialize both endpoint transfers and the per-user device cap before reading tracked state.
        // Every subscription takes locks in this order, so two users transferring a phone cannot deadlock.
        await using var deviceLease = await operations.AcquireAsync("push-device:" + endpoint, ct);
        await using var userLease = await operations.AcquireAsync("push-user:" + userId, ct);
        var now = time.GetUtcNow();
        var row = await db.PushSubscriptions.SingleOrDefaultAsync(s => s.Endpoint == endpoint, ct);
        if (row is null || row.UserId != userId)
        {
            if (await db.PushSubscriptions.CountAsync(s => s.UserId == userId, ct) >= MaxDevices)
            {
                // The oldest device makes room: a learner rarely has more phones than this.
                var oldest = await db.PushSubscriptions.Where(s => s.UserId == userId).OrderBy(s => s.CreatedAt).FirstAsync(ct);
                db.PushSubscriptions.Remove(oldest);
            }

            if (row is null)
            {
                row = new PushSubscription { Id = Guid.CreateVersion7(now), Endpoint = endpoint, CreatedAt = now };
                db.PushSubscriptions.Add(row);
            }
        }

        (row.UserId, row.P256dh, row.Auth, row.FailedAt) = (userId, request.P256dh.Trim(), request.Auth.Trim(), null);
        row.UserAgent = userAgent is { Length: > 300 } ? userAgent[..300] : userAgent;
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        user.RemindersEnabled = true;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Forgets this device, only when it is the learner's own; another's endpoint changes nothing.</summary>
    public async Task UnsubscribeAsync(Guid userId, string? endpoint, CancellationToken ct) =>
        await db.PushSubscriptions.Where(s => s.UserId == userId && s.Endpoint == (endpoint ?? "").Trim()).ExecuteDeleteAsync(ct);

    public async Task<int> DevicesAsync(Guid userId, CancellationToken ct) =>
        await db.PushSubscriptions.CountAsync(s => s.UserId == userId, ct);
}
