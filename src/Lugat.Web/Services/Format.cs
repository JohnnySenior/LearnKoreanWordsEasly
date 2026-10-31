using Lugat.Domain.Catalog;
using Lugat.Domain.Premium;
using Lugat.Domain.Learning;
using Lugat.Domain.Study;

namespace Lugat.Web.Services;

/// <summary>Uzbek formatting for dates, intervals and study labels. Times are shown in Korea time.</summary>
public static class Format
{
    private static readonly string[] Days = ["Yakshanba", "Dushanba", "Seshanba", "Chorshanba", "Payshanba", "Juma", "Shanba"];

    private static readonly string[] Months =
        ["yanvar", "fevral", "mart", "aprel", "may", "iyun", "iyul", "avgust", "sentabr", "oktabr", "noyabr", "dekabr"];

    /// <summary>Korea has no daylight saving time; a fixed offset avoids depending on browser time zone data.</summary>
    public static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9);

    public static DateTimeOffset ToKorea(DateTimeOffset value) => value.ToOffset(KoreaOffset);

    /// <summary>The Korean name of a level, as learners meet it in Korea (초급 = beginner).</summary>
    public static string LevelKorean(CatalogLevel level) => level switch { CatalogLevel.A => "초급", CatalogLevel.B => "중급", _ => "고급" };

    /// <summary>"Seshanba, 6-oktabr, 23:00".</summary>
    public static string DayTime(DateTimeOffset value)
    {
        var k = ToKorea(value);
        return $"{Days[(int)k.DayOfWeek]}, {k.Day}-{Months[k.Month - 1]}, {k:HH\\:mm}";
    }

    /// <summary>"Seshanba, 29-sentabr" (Korea date).</summary>
    public static string DayMonth(DateTimeOffset value)
    {
        var k = ToKorea(value);
        return $"{Days[(int)k.DayOfWeek]}, {k.Day}-{Months[k.Month - 1]}";
    }

    /// <summary>"sentabr 2026".</summary>
    public static string Month(DateOnly month) => $"{Months[month.Month - 1]} {month.Year}";

    /// <summary>"29-sentabr".</summary>
    public static string Day(DateOnly day) => $"{day.Day}-{Months[day.Month - 1]}";

    /// <summary>"bugun, 18:40", "ertaga, 09:15", or "Seshanba, 6-oktabr, 23:00" (Korea time).</summary>
    public static string Soon(DateTimeOffset value, DateTimeOffset now)
    {
        var k = ToKorea(value);
        var days = DateOnly.FromDateTime(k.DateTime).DayNumber - DateOnly.FromDateTime(ToKorea(now).DateTime).DayNumber;
        return days switch
        {
            <= 0 => $"bugun, {k:HH\\:mm}",
            1 => $"ertaga, {k:HH\\:mm}",
            _ => DayTime(value),
        };
    }

    /// <summary>"bugun", "kecha", "3 kun oldin", or the date.</summary>
    public static string Ago(DateTimeOffset value, DateTimeOffset now)
    {
        var days = (DateOnly.FromDateTime(ToKorea(now).DateTime).DayNumber - DateOnly.FromDateTime(ToKorea(value).DateTime).DayNumber);
        return days switch
        {
            <= 0 => "bugun",
            1 => "kecha",
            < 30 => $"{days} kun oldin",
            _ => DayMonth(value),
        };
    }

    public static string Won(int amount) => Money(amount, Currency.Krw);

    /// <summary>"₩5,000" or "29 000 so‘m" (L-48).</summary>
    public static string Money(int amount, Currency currency) => Lugat.Domain.Premium.Money.Format(amount, currency);

    /// <summary>"15 daq", "5 soat", "1 kun", "2 oy".</summary>
    public static string Interval(int seconds) => seconds switch
    {
        < 3600 => $"{Math.Max(1, seconds / 60)} daq",
        < 86400 => $"{seconds / 3600} soat",
        < 60 * 86400 => $"{seconds / 86400} kun",
        _ => $"{seconds / (30 * 86400)} oy",
    };

    public static string Rating(ReviewAnswer answer) => answer switch
    {
        ReviewAnswer.Forgot => "Eslay olmadim",
        ReviewAnswer.Hard => "Qiynalib esladim",
        ReviewAnswer.Easy => "Oson esladim",
        _ => answer.ToString(),
    };

    public static string Kind(SessionItemKind kind) => kind switch
    {
        SessionItemKind.New => "Yangi so‘z",
        SessionItemKind.Relearning => "Qayta",
        SessionItemKind.FocusReview => "Jadval takrorlash",
        SessionItemKind.Overdue => "Eski so‘z",
        _ => "Takrorlash",
    };

    public static string Direction(ReviewDirection direction) =>
        direction == ReviewDirection.KoreanToUzbek ? "Ma’nosini eslang" : "Koreyschasini eslang";
}
