using Lugat.Domain.Vocabulary;

namespace Lugat.Domain.Catalog;

/// <summary>Levels of the official list (spec §2.1): A beginner, B intermediate, C advanced.</summary>
public enum CatalogLevel
{
    A,
    B,
    C,
}

public enum CatalogWordStatus
{
    Draft,

    /// <summary>A person checked the content (spec §2.4 step 4). Only reviewed words can be published.</summary>
    Reviewed,
}

public enum UnitStatus
{
    Draft,

    /// <summary>Visible to learners. Its words never move to another unit again (spec §2.2).</summary>
    Published,
}

/// <summary>
/// One word of the shared catalog (spec §2.3, §5.1): the official fields from 국립국어원 「한국어 학습용 어휘 목록」
/// and the content written for it. Shared by all learners; the AI never changes the official fields.
/// </summary>
public sealed class CatalogWord
{
    public const string DefaultSource = "국립국어원 한국어 학습용 어휘 목록 (KOGL 1)";

    public Guid Id { get; set; }

    /// <summary>Import key: level, word, homonym number and part of speech (spec §2.4 step 1).</summary>
    public string SourceKey { get; set; } = "";

    public string Word { get; set; } = "";

    /// <summary>Homonym number as in the list, e.g. "01" for 가다01; null when the word has none.</summary>
    public string? Homonym { get; set; }

    /// <summary>Official part of speech code (품사): 명, 동, 형, 부, 대, 수, 관, 의, 보, 감 …</summary>
    public string Pos { get; set; } = "";

    /// <summary>The official short gloss or hanja (풀이), when the list has one.</summary>
    public string? Gloss { get; set; }

    public CatalogLevel Level { get; set; }

    /// <summary>Frequency rank (순위): most used first.</summary>
    public int FreqRank { get; set; }

    /// <summary>False for parts of speech that do not work as cards (보, 의, 관); kept for a later grammar section.</summary>
    public bool IsStudyable { get; set; }

    public Guid? UnitId { get; set; }

    public int? OrderInUnit { get; set; }

    public string? TranslationUz { get; set; }

    public string? Romanization { get; set; }

    public string? ExampleKo { get; set; }

    public string? ExampleUz { get; set; }

    /// <summary>A real trap for an Uzbek speaker; null when there is none (never invented, D-46).</summary>
    public string? TrapUz { get; set; }

    public string? Theme { get; set; }

    public string Source { get; set; } = DefaultSource;

    public CatalogWordStatus Status { get; set; }

    /// <summary>Raised whenever the content changes; progress and statistics are kept (spec DATA02, AU05).</summary>
    public int ContentVersion { get; set; } = 1;

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Who wrote the draft, e.g. "Claude (Anthropic)"; null when no AI draft was made.</summary>
    public string? AiProvider { get; set; }

    public string? AiModel { get; set; }

    public DateTimeOffset? DraftedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public uint Version { get; set; }

    public static string Key(CatalogLevel level, string word, string? homonym, string pos) => $"{level}:{word}{homonym}:{pos}";

    /// <summary>
    /// Auxiliary verbs/adjectives (보), bound nouns (의) and determiners (관) are not studied as cards (spec §2.2).
    /// A word listed under several parts of speech ("명/의") is studied when one of them can be.
    /// </summary>
    public static bool StudyablePos(string pos) =>
        pos.Split(['/', ',', '·'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(p => p is not ("보" or "의" or "관"));
}

/// <summary>Official part-of-speech codes (품사) and their Uzbek names.</summary>
public static class CatalogPos
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["명"] = "ot",
        ["동"] = "fe’l",
        ["형"] = "sifat",
        ["부"] = "ravish",
        ["대"] = "olmosh",
        ["수"] = "son",
        ["감"] = "undov",
        ["관"] = "aniqlovchi",
        ["의"] = "qaram ot",
        ["보"] = "yordamchi fe’l",
        ["조"] = "ko‘makchi",
    };

    /// <summary>"fe’l" for 동, "ot / qaram ot" for 명/의; unknown codes as they are.</summary>
    public static string Uzbek(string pos) => string.Join(" / ",
        pos.Split(['/', ',', '·'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => Names.GetValueOrDefault(p, p)));

    /// <summary>The code for an Uzbek name ("fe'l" → 동), for content files written in Uzbek; null when unknown.</summary>
    public static string? FromUzbek(string name)
    {
        var key = name.Trim().Replace('\'', '’').Replace('‘', '’').Replace('ʻ', '’').Replace('`', '’').ToLowerInvariant();
        return Names.FirstOrDefault(n => n.Value.Replace('‘', '’') == key).Key;
    }
}

/// <summary>
/// The shared "which word is right?" questions of a catalog word (spec §5.1: the owner-less <see cref="WordUsage"/>).
/// Written by the AI with the draft and checked by a person with the word.
/// </summary>
public sealed class CatalogUsage
{
    public Guid Id { get; set; }

    public Guid CatalogWordId { get; set; }

    /// <summary>The word's content version the questions were written or last checked for.</summary>
    public int ContentVersion { get; set; }

    public List<UsageQuestion> Questions { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>25 words of one level in frequency order (spec §2.2). Numbers are stable once published.</summary>
public sealed class Unit
{
    public Guid Id { get; set; }

    public CatalogLevel Level { get; set; }

    public int Number { get; set; }

    public UnitStatus Status { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int WordCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public uint Version { get; set; }
}
