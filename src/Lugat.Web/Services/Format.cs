using Lugat.Domain.Learning;
using Lugat.Domain.Lessons;

namespace Lugat.Web.Services;

/// <summary>Uzbek formatting for dates, intervals and study labels. Lesson times are shown in Korea time (spec PLAN02).</summary>
public static class Format
{
    public static string Mastery(Lugat.Domain.Grammar.GrammarMastery m) => m switch
    {
        Lugat.Domain.Grammar.GrammarMastery.Seen => "Ko‘rib chiqildi",
        Lugat.Domain.Grammar.GrammarMastery.Practising => "Mashq qilinyapti",
        Lugat.Domain.Grammar.GrammarMastery.Independent => "Mustaqil qo‘llayapti",
        _ => "Yangi",
    };

    public static string QuestionKind(Lugat.Domain.Grammar.GrammarQuestionKind k) => k switch
    {
        Lugat.Domain.Grammar.GrammarQuestionKind.Simpler => "Soddaroq tushuntir",
        Lugat.Domain.Grammar.GrammarQuestionKind.MoreExamples => "Yana misol",
        Lugat.Domain.Grammar.GrammarQuestionKind.Difference => "O‘xshashidan farqi",
        _ => "Savol",
    };

    private static readonly string[] Days = ["Yakshanba", "Dushanba", "Seshanba", "Chorshanba", "Payshanba", "Juma", "Shanba"];

    private static readonly string[] Months =
        ["yanvar", "fevral", "mart", "aprel", "may", "iyun", "iyul", "avgust", "sentabr", "oktabr", "noyabr", "dekabr"];

    /// <summary>Korea has no daylight saving time; a fixed offset avoids depending on browser time zone data.</summary>
    public static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9);

    public static DateTimeOffset ToKorea(DateTimeOffset value) => value.ToOffset(KoreaOffset);

    /// <summary>"Seshanba, 6-oktabr, 23:00".</summary>
    public static string LessonDate(DateTimeOffset value)
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

    /// <summary>"Juma, 3-oktabr" for a calendar date (a grammar set's deadline).</summary>
    public static string Date(DateOnly d) => $"{Days[(int)d.DayOfWeek]}, {d.Day}-{Months[d.Month - 1]}";

    /// <summary>"bugun", "ertaga", "3 kun qoldi", "2 kun o‘tdi" relative to the Korea date today.</summary>
    public static string DueIn(DateOnly due)
    {
        var today = DateOnly.FromDateTime(ToKorea(DateTimeOffset.UtcNow).DateTime);
        var days = due.DayNumber - today.DayNumber;
        return days switch
        {
            0 => "muddat bugun",
            1 => "muddat ertaga",
            > 1 => $"{days} kun qoldi",
            _ => $"muddat {-days} kun oldin o‘tdi",
        };
    }

    /// <summary>Two-letter weekday for the week strip: "Du", "Se", ...</summary>
    public static string ShortDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Du",
        DayOfWeek.Tuesday => "Se",
        DayOfWeek.Wednesday => "Ch",
        DayOfWeek.Thursday => "Pa",
        DayOfWeek.Friday => "Ju",
        DayOfWeek.Saturday => "Sh",
        _ => "Ya",
    };

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
        SessionItemKind.LessonReview => "Dars uchun takrorlash",
        SessionItemKind.Overdue => "Eski so‘z",
        _ => "Takrorlash",
    };

    public static string Direction(ReviewDirection direction) =>
        direction == ReviewDirection.KoreanToUzbek ? "Ma’nosini eslang" : "Koreyschasini eslang";
}
