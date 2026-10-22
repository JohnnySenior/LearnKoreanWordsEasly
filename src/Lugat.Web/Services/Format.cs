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
