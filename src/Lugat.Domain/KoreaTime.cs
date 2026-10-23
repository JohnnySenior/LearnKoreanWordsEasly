namespace Lugat.Domain;

/// <summary>
/// Days and months for the business side: payments, commissions and activity metrics are counted in Korea time,
/// where the app and its learners are (spec §1.2). A learner's own study day uses <c>User.Zone()</c>.
/// </summary>
public static class KoreaTime
{
    public const string ZoneId = "Asia/Seoul";

    public static TimeZoneInfo Zone { get; } =
        TimeZoneInfo.TryFindSystemTimeZoneById(ZoneId, out var zone) ? zone : TimeZoneInfo.Utc;

    public static DateOnly Day(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, Zone).DateTime);

    public static DateOnly MonthOf(DateTimeOffset at) => Day(at) is var d ? new DateOnly(d.Year, d.Month, 1) : default;

    /// <summary>The moment a Korean day starts, in UTC (PostgreSQL stores UTC only).</summary>
    public static DateTimeOffset StartOf(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
