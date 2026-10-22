using System.Text.RegularExpressions;

namespace KoreanAiTeacher.Domain.Testing;

/// <summary>TOPIK II reading question types, told apart by the standard instruction line (D-35).</summary>
public enum BookCategory
{
    /// <summary>(    )에 들어갈 가장 알맞은 것: grammar or a phrase in a gap.</summary>
    Blank,

    /// <summary>의미가 비슷한 것: a paraphrase of the underlined part.</summary>
    Similar,

    /// <summary>무엇에 대한 글: what an ad or notice is about.</summary>
    Topic,

    /// <summary>내용과 같은 것 / 알 수 있는 것: matching the content, including graphs.</summary>
    Content,

    /// <summary>순서대로 맞게 배열: putting sentences in order.</summary>
    Order,

    /// <summary>주제 / 중심 생각: the main idea.</summary>
    MainIdea,

    /// <summary>문장이 들어가기에 가장 알맞은 곳: where a sentence fits.</summary>
    Insert,

    /// <summary>심정 / 태도: the writer's or a character's feeling or attitude.</summary>
    Attitude,

    /// <summary>신문 기사의 제목: explaining a newspaper headline.</summary>
    Headline,

    /// <summary>글을 쓴 목적: why the text was written.</summary>
    Purpose,

    Other,
}

public static partial class BookCategories
{
    /// <summary>More specific phrases first: "빈칸에 들어갈" also contains "들어갈", which the insert type uses.</summary>
    private static readonly (string Phrase, BookCategory Category)[] Rules =
    [
        ("제목", BookCategory.Headline),
        ("심정", BookCategory.Attitude),
        ("태도", BookCategory.Attitude),
        ("목적", BookCategory.Purpose),
        ("순서", BookCategory.Order),
        ("배열", BookCategory.Order),
        ("들어가기에", BookCategory.Insert),
        ("들어갈 곳", BookCategory.Insert),
        ("비슷한", BookCategory.Similar),
        ("무엇에 대한", BookCategory.Topic),
        ("중심 생각", BookCategory.MainIdea),
        ("주제", BookCategory.MainIdea),
        ("들어갈", BookCategory.Blank),
        ("내용과 같은", BookCategory.Content),
        ("일치", BookCategory.Content),
        ("알 수 있는", BookCategory.Content),
    ];

    public static BookCategory Classify(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return BookCategory.Other;
        }

        foreach (var (phrase, category) in Rules)
        {
            if (instruction.Contains(phrase, StringComparison.Ordinal))
            {
                return category;
            }
        }

        return BookCategory.Other;
    }

    /// <summary>
    /// Types for one book test's questions. From question 19 on, TOPIK prints a general instruction
    /// ("다음을 읽고 물음에 답하십시오") and the type is in the question itself, so a stem that asks something
    /// ("…고르십시오", "…것은?") is read first. Otherwise the instruction decides; a question without one takes the
    /// type of the last instruction whose "[n~m]" range covers it (or simply the last one, when no range is printed).
    /// </summary>
    public static Dictionary<int, BookCategory> ForTest(IEnumerable<(int Number, string? Instruction, string? Stem)> questions)
    {
        var result = new Dictionary<int, BookCategory>();
        (int From, int To, BookCategory Category)? current = null;
        foreach (var (number, instruction, stem) in questions.OrderBy(q => q.Number))
        {
            if (!string.IsNullOrWhiteSpace(instruction))
            {
                var range = Range(instruction);
                current = (range?.From ?? number, range?.To ?? int.MaxValue, Classify(instruction));
            }

            var asked = IsQuestion(stem) ? Classify(stem) : BookCategory.Other;
            result[number] = asked != BookCategory.Other
                ? asked
                : current is { } c && number >= c.From && number <= c.To ? c.Category : BookCategory.Other;
        }

        return result;
    }

    /// <summary>A stem that asks for something, not a passage sentence that may happen to contain "주제" or "목적".</summary>
    private static bool IsQuestion(string? stem) =>
        stem is not null && (stem.Contains("고르십시오", StringComparison.Ordinal) || stem.Contains("것은", StringComparison.Ordinal));

    private static (int From, int To)? Range(string instruction)
    {
        var m = RangePattern().Match(instruction);
        return m.Success ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) : null;
    }

    [GeneratedRegex(@"(\d{1,3})\s*[~～\-–]\s*(\d{1,3})")]
    private static partial Regex RangePattern();

    public static string Label(BookCategory category) => category switch
    {
        BookCategory.Blank => "Bo‘sh joyni to‘ldirish",
        BookCategory.Similar => "Ma’nosi yaqin ifoda",
        BookCategory.Topic => "Nima haqida",
        BookCategory.Content => "Mazmunga mos gap",
        BookCategory.Order => "Gaplar tartibi",
        BookCategory.MainIdea => "Asosiy fikr",
        BookCategory.Insert => "Gapni joyiga qo‘yish",
        BookCategory.Attitude => "Kayfiyat va munosabat",
        BookCategory.Headline => "Gazeta sarlavhasi",
        BookCategory.Purpose => "Yozilish maqsadi",
        _ => "Boshqa",
    };
}
