namespace Lugat.Domain.Users;

/// <summary>
/// A day (Korea time) the user opened the app while signed in. Recorded at most about once an hour per user; it is
/// what "faol" means in the teacher panel and the admin metrics (daily active, D1/D7 retention).
/// </summary>
public sealed class UserActivityDay
{
    public Guid UserId { get; set; }

    public DateOnly Day { get; set; }
}
